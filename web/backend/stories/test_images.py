"""Campaign images: re-encoding, private serving and context-derived player visibility."""
import io
import uuid
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.core.files.uploadedfile import SimpleUploadedFile
from django.test import Client, TestCase
from PIL import Image
from rest_framework.test import APIClient

from . import images
from .domain_errors import DomainError
from .models import Campaign, CampaignMember, WikiPage
from .test_rulesets import mini


def png(size=(40, 30), exif=False, fmt="PNG"):
    out = io.BytesIO()
    image = Image.new("RGB", size, (200, 30, 60))
    kwargs = {}
    if exif:
        data = Image.Exif()
        data[0x010F] = "SECRET_CAMERA"  # Make
        kwargs["exif"] = data.tobytes()
    image.save(out, fmt, **kwargs)
    return out.getvalue()


class ProcessTests(TestCase):
    def test_reencodes_to_webp_without_metadata(self):
        data, width, height = images.process(png(exif=True, fmt="JPEG"))
        self.assertEqual((width, height), (40, 30))
        self.assertEqual(data[:4], b"RIFF")
        self.assertNotIn(b"SECRET_CAMERA", data)
        self.assertFalse(Image.open(io.BytesIO(data)).getexif())

    def test_large_images_are_downscaled(self):
        _, width, height = images.process(png((3200, 800)))
        self.assertEqual((width, height), (1600, 400))

    def test_rejections(self):
        bmp = io.BytesIO()
        Image.new("RGB", (4, 4)).save(bmp, "BMP")
        for raw in (b"", None, b"not an image", b"<svg xmlns='http://www.w3.org/2000/svg'/>", bmp.getvalue(),
                    png()[:40], b"\x89PNG\r\n\x1a\n" + b"x" * 100):
            with self.subTest(raw=raw[:12] if raw else raw), self.assertRaises(DomainError):
                images.process(raw)
        with self.assertRaisesRegex(DomainError, "8 MB"):
            images.process(b"x" * (images.MAX_INPUT + 1))
        with patch.object(images, "MAX_PIXELS", 100), self.assertRaisesRegex(DomainError, "too many pixels"):
            images.process(png((20, 20)))


class VisibilityTests(TestCase):
    def setUp(self):
        users = get_user_model().objects
        self.gm, self.player, self.other, self.outsider = (users.create_user(n, password="pw-123456") for n in ("gm", "pl", "pl2", "out"))
        self.campaign = Campaign.objects.create(owner=self.gm, title="World")
        self.member = CampaignMember.objects.create(campaign=self.campaign, user=self.player, role="player")
        CampaignMember.objects.create(campaign=self.campaign, user=self.other, role="player")
        self.base = f"/api/campaigns/{self.campaign.id}/"

    def api(self, user):
        client = APIClient()
        client.force_authenticate(user)
        return client

    def upload(self, user=None, **extra):
        data = {"id": str(uuid.uuid4()), "file": SimpleUploadedFile("a.png", png(), "image/png"), **extra}
        return self.api(user or self.gm).post(self.base + "images/", data, format="multipart")

    def file(self, user, image_id):
        client = Client()
        client.force_login(user)
        return client.get(f"{self.base}images/{image_id}/file/")

    def test_upload_is_gm_only_and_idempotent(self):
        self.assertEqual(self.upload(self.player).status_code, 404)
        self.assertEqual(self.upload(self.outsider).status_code, 404)
        image_id = str(uuid.uuid4())
        raw = png()
        first = self.api(self.gm).post(self.base + "images/", {"id": image_id, "file": SimpleUploadedFile("a.png", raw)}, format="multipart")
        self.assertEqual(first.status_code, 201, first.data)
        retry = self.api(self.gm).post(self.base + "images/", {"id": image_id, "file": SimpleUploadedFile("a.png", raw)}, format="multipart")
        self.assertEqual(retry.status_code, 200)
        other = self.api(self.gm).post(self.base + "images/", {"id": image_id, "file": SimpleUploadedFile("b.png", png((9, 9)))}, format="multipart")
        self.assertEqual(other.status_code, 409)
        self.assertEqual(self.upload(gallery="everyone").status_code, 400)
        self.assertEqual(self.api(self.gm).post(self.base + "images/", {"id": str(uuid.uuid4())}, format="multipart").status_code, 400)

    def test_private_headers_and_anonymous_access(self):
        image_id = self.upload().data["id"]
        response = self.file(self.gm, image_id)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response["Content-Type"], "image/webp")
        self.assertEqual(response["Cache-Control"], "private, no-store")
        self.assertIn("noindex", response["X-Robots-Tag"])
        self.assertEqual(response["X-Content-Type-Options"], "nosniff")
        self.assertEqual(Client().get(f"{self.base}images/{image_id}/file/").status_code, 404)
        self.assertEqual(self.file(self.outsider, image_id).status_code, 404)

    def test_player_sees_images_only_through_shared_context(self):
        image_id = self.upload().data["id"]
        self.assertEqual(self.file(self.player, image_id).status_code, 404)
        self.assertEqual(self.api(self.player).get(self.base + "images/").data, [])
        # Gallery for players.
        self.api(self.gm).patch(f"{self.base}images/{image_id}/", {"gallery": "players"}, format="json")
        self.assertEqual(self.file(self.player, image_id).status_code, 200)
        self.api(self.gm).patch(f"{self.base}images/{image_id}/", {"gallery": "gm"}, format="json")
        self.assertEqual(self.file(self.player, image_id).status_code, 404)
        # Cover.
        self.api(self.gm).put(self.base + "cover/", {"image": image_id}, format="json")
        self.assertEqual(self.file(self.player, image_id).status_code, 200)
        self.assertEqual(self.api(self.player).get(self.base).data["cover"], image_id)
        self.api(self.gm).put(self.base + "cover/", {"image": None}, format="json")
        self.assertEqual(self.file(self.player, image_id).status_code, 404)
        # Page illustration: draft attachment alone does not share; publication does; withdrawal hides.
        page = WikiPage.objects.create(campaign=self.campaign, title="Klinika", kind="location")
        attached = self.api(self.gm).put(f"{self.base}pages/{page.id}/image/", {"version": 1, "image": image_id}, format="json").data
        self.assertEqual(self.file(self.player, image_id).status_code, 404)
        published = self.api(self.gm).post(f"{self.base}pages/{page.id}/publication/",
                                           {"version": attached["version"], "audience": "selected", "recipients": [str(self.member.id)]}, format="json").data
        self.assertEqual(self.file(self.player, image_id).status_code, 200)
        self.assertEqual(self.file(self.other, image_id).status_code, 404)  # not a recipient
        self.assertEqual(self.api(self.player).get(f"{self.base}pages/{page.id}/").data["image"], image_id)
        self.assertEqual(self.api(self.player).get(self.base + "images/").data, [])  # not added to the gallery
        self.api(self.gm).post(f"{self.base}pages/{page.id}/publication/", {"version": published["version"], "audience": "gm"}, format="json")
        self.assertEqual(self.file(self.player, image_id).status_code, 404)
        self.assertEqual(self.api(self.gm).put(f"{self.base}pages/{page.id}/image/", {"version": 1, "image": image_id}, format="json").status_code, 409)

    def test_ruleset_images_are_validated_shared_and_protected(self):
        image_id = self.upload().data["id"]
        document = mini()
        document["roles"][0]["image"] = str(uuid.uuid4())
        put = lambda doc, rev: self.api(self.gm).put(self.base + "ruleset/", {"version": rev, "document": doc}, format="json")
        self.assertEqual(put(document, 0).status_code, 400)
        document["roles"][0]["image"] = "not-a-uuid"
        self.assertEqual(put(document, 0).status_code, 400)
        document["roles"][0]["image"] = image_id
        self.assertEqual(put(document, 0).status_code, 200)
        self.assertEqual(self.file(self.player, image_id).status_code, 200)
        self.assertEqual(self.api(self.gm).delete(f"{self.base}images/{image_id}/").status_code, 409)
        listed = self.api(self.gm).get(self.base + "images/").data[0]
        self.assertTrue(listed["usage"]["ruleset"])

    def test_delete_detaches_cover_and_pages(self):
        image_id = self.upload().data["id"]
        page = WikiPage.objects.create(campaign=self.campaign, title="Bar", kind="location")
        self.api(self.gm).put(self.base + "cover/", {"image": image_id}, format="json")
        self.api(self.gm).put(f"{self.base}pages/{page.id}/image/", {"version": 1, "image": image_id}, format="json")
        self.assertEqual(self.api(self.player).delete(f"{self.base}images/{image_id}/").status_code, 404)
        result = self.api(self.gm).delete(f"{self.base}images/{image_id}/").data
        self.assertEqual(result, {"deleted": True, "detached": {"cover": True, "pages": 1}})
        self.campaign.refresh_from_db()
        page.refresh_from_db()
        self.assertIsNone(self.campaign.cover_id)
        self.assertIsNone(page.image_id)
        self.assertEqual(self.api(self.gm).put(self.base + "cover/", {"image": image_id}, format="json").status_code, 404)
