"""Real access boundaries: publication, recipient projection, revocation and races."""

import uuid
from concurrent.futures import ThreadPoolExecutor
from datetime import timedelta
from threading import Barrier, Event
from unittest import skipUnless

from django.contrib.auth import get_user_model
from django.core.cache import cache
from django.db import close_old_connections, connection, transaction
from django.test import TestCase, TransactionTestCase
from django.utils import timezone
from rest_framework.test import APIClient

from . import wiki
from . import wiki_sharing as sharing
from .domain_errors import DomainError
from .models import (
    Campaign,
    CampaignInvitation,
    CampaignMember,
    WikiPage,
    WikiRelation,
)


class SharingTests(TestCase):
    def setUp(self):
        cache.clear()
        self.owner, self.gm, self.alice, self.bob, self.other = [
            get_user_model().objects.create_user(n)
            for n in ["owner", "gm", "alice", "bob", "other"]
        ]
        self.c = Campaign.objects.create(owner=self.owner, title="Coast")
        self.g = CampaignMember.objects.create(campaign=self.c, user=self.gm, role="gm")
        self.a = CampaignMember.objects.create(
            campaign=self.c, user=self.alice, role="player"
        )
        self.b = CampaignMember.objects.create(
            campaign=self.c, user=self.bob, role="player"
        )
        self.port = WikiPage.objects.create(
            campaign=self.c, title="Port", body="PUBLIC_HARBOR", gm_notes="SECRET_PORT"
        )
        self.debt = WikiPage.objects.create(
            campaign=self.c, title="ALICE_DEBT", body="DEBT_BODY"
        )
        self.secret = WikiPage.objects.create(
            campaign=self.c, title="SECRET_MAYOR", body="MAYOR_BODY"
        )
        self.r = WikiRelation.objects.create(
            campaign=self.c, source=self.port, target=self.debt, label="SECRET_RELATION"
        )
        self.client = APIClient()
        self.root = f"/api/campaigns/{self.c.id}/"

    def as_user(self, user):
        self.client.force_authenticate(user)
        return self.client

    def publish(self, page, audience="all", recipients=None):
        page.refresh_from_db()
        return sharing.publish(
            self.owner,
            self.c.id,
            page.id,
            {
                "version": page.version,
                "audience": audience,
                "recipients": recipients or [],
            },
        )

    def test_old_material_stays_unpublished(self):
        data = self.as_user(self.alice).get(self.root).json()
        self.assertEqual(data["pages"], [])
        self.assertEqual(data["relations"], [])
        self.assertEqual(data["version"], 0)
        self.assertEqual(
            self.client.get(self.root + f"pages/{self.port.id}/").status_code, 404
        )

    def test_every_projection_filters_secrets_and_selected_recipients(self):
        self.publish(self.port)
        self.publish(self.debt, "selected", [str(self.a.id)])
        sharing.publish(
            self.owner,
            self.c.id,
            self.r.id,
            {"version": 1, "audience": "all"},
            relation=True,
        )
        for user, expected in [(self.alice, 2), (self.bob, 1)]:
            client = self.as_user(user)
            for suffix in ["", "pages/?q=", "export/"]:
                response = client.get(self.root + suffix)
                self.assertEqual(response.status_code, 200)
                text = response.content.decode()
                self.assertNotIn("SECRET_PORT", text)
                self.assertNotIn("SECRET_MAYOR", text)
                self.assertNotIn("gm_notes", text)
                if user == self.bob:
                    self.assertNotIn("ALICE_DEBT", text)
                    self.assertNotIn("SECRET_RELATION", text)
                self.assertIn("no-store", response["Cache-Control"])
            detail = client.get(self.root).json()
            self.assertEqual(len(detail["pages"]), expected)
            self.assertEqual(len(detail["relations"]), 1 if user == self.alice else 0)
            self.assertEqual(
                client.get(self.root + "export/").json()["schema"],
                "narrative-wiki-player/1",
            )

    def test_secret_edge_between_visible_pages_is_hidden(self):
        self.publish(self.port)
        self.publish(self.debt)
        self.assertEqual(wiki.campaign_detail(self.alice, self.c.id)["relations"], [])
        sharing.publish(
            self.owner,
            self.c.id,
            self.r.id,
            {"version": 1, "audience": "selected", "recipients": [str(self.a.id)]},
            relation=True,
        )
        self.assertEqual(
            len(wiki.read_page(self.alice, self.c.id, self.port.id)["relations"]), 1
        )
        self.assertEqual(
            wiki.read_page(self.bob, self.c.id, self.port.id)["relations"], []
        )

    def test_draft_edits_do_not_publish_or_affect_search_sort_or_version(self):
        published = self.publish(self.port)
        wiki.save_page(
            self.gm,
            self.c.id,
            self.port.id,
            {
                "version": published["version"],
                "title": "HIDDEN_NEW_TITLE",
                "body": "DRAFT_SENTINEL",
                "gm_notes": "NEW_SECRET",
            },
        )
        visible = wiki.read_page(self.alice, self.c.id, self.port.id)
        self.assertEqual(visible["title"], "Port")
        self.assertEqual(visible["body"], "PUBLIC_HARBOR")
        self.assertEqual(visible["version"], published["published"]["version"])
        self.assertEqual(wiki.search_pages(self.alice, self.c.id, "DRAFT_SENTINEL"), [])
        self.assertEqual(wiki.search_pages(self.alice, self.c.id, "NEW_SECRET"), [])
        self.assertEqual(
            len(wiki.search_pages(self.gm, self.c.id, "DRAFT_SENTINEL")), 1
        )
        self.publish(self.port)
        self.assertEqual(
            wiki.read_page(self.alice, self.c.id, self.port.id)["body"],
            "DRAFT_SENTINEL",
        )

    def test_withdraw_and_audience_change_take_effect(self):
        self.publish(self.port)
        self.publish(self.port, "selected", [str(self.a.id)])
        self.assertEqual(
            self.as_user(self.bob)
            .get(self.root + f"pages/{self.port.id}/")
            .status_code,
            404,
        )
        self.publish(self.port, "gm")
        self.assertEqual(wiki.campaign_detail(self.alice, self.c.id)["pages"], [])

    def test_player_cannot_mutate_or_promote_or_inspect_members(self):
        client = self.as_user(self.alice)
        paths = [
            ("post", "pages/", {"id": str(uuid.uuid4()), "title": "bad"}),
            ("put", f"pages/{self.port.id}/", {"version": 1, "title": "bad"}),
            ("delete", f"pages/{self.port.id}/", {"version": 1}),
            (
                "post",
                f"pages/{self.port.id}/publication/",
                {"version": 1, "audience": "all"},
            ),
            ("post", "invitations/", {"role": "gm"}),
            ("patch", f"members/{self.a.id}/", {"role": "gm", "version": 1}),
            ("delete", f"members/{self.b.id}/", {}),
            ("put", "", {"version": 1, "title": "bad"}),
            ("post", f"pages/{self.port.id}/use/", {}),
            ("delete", "", {"version": 1}),
        ]
        for method, path, data in paths:
            self.assertEqual(
                getattr(client, method)(
                    self.root + path, data, format="json"
                ).status_code,
                404,
                (method, path),
            )
        self.assertEqual(client.get(self.root + "members/").status_code, 404)
        self.assertEqual(client.get(self.root + "audit/").status_code, 404)

    def test_gm_can_edit_but_cannot_manage_members_or_campaign(self):
        self.assertEqual(
            wiki.read_page(self.gm, self.c.id, self.port.id)["gm_notes"], "SECRET_PORT"
        )
        client = self.as_user(self.gm)
        self.assertEqual(
            client.put(
                self.root + f"pages/{self.port.id}/",
                {"version": 1, "title": "Coedited"},
                format="json",
            ).status_code,
            200,
        )
        self.assertEqual(
            client.post(
                self.root + "invitations/", {"role": "player"}, format="json"
            ).status_code,
            404,
        )
        self.assertEqual(
            client.delete(self.root, {"version": 2}, format="json").status_code, 404
        )

    def test_owner_preview_matches_player_projection_in_every_view(self):
        self.publish(self.port)
        client = self.as_user(self.owner)
        preview = f"preview={self.a.id}"
        self.assertEqual(
            client.get(self.root + "?" + preview).json()["pages"],
            wiki.campaign_detail(self.alice, self.c.id)["pages"],
        )
        data = client.get(self.root + f"pages/{self.port.id}/?" + preview).json()
        self.assertNotIn("gm_notes", data)
        self.assertEqual(
            client.get(self.root + "export/?" + preview).json()["schema"],
            "narrative-wiki-player/1",
        )
        self.assertEqual(
            self.as_user(self.bob).get(self.root + "?" + preview).status_code, 404
        )
        other = Campaign.objects.create(owner=self.other, title="Other")
        m = CampaignMember.objects.create(
            campaign=other, user=self.alice, role="player"
        )
        self.assertEqual(
            self.as_user(self.owner).get(self.root + f"?preview={m.id}").status_code,
            404,
        )

    def test_revocation_and_rejoining_do_not_restore_grants(self):
        self.publish(self.port, "selected", [str(self.a.id)])
        sharing.change_member(self.owner, self.c.id, self.a.id)
        self.assertEqual(self.as_user(self.alice).get(self.root).status_code, 404)
        token = sharing.invitations(self.owner, self.c.id, {"role": "player"})["token"]
        sharing.invitation(self.alice, {"token": token}, accept=True)
        self.assertEqual(wiki.campaign_detail(self.alice, self.c.id)["pages"], [])

    def test_demotion_removes_private_reads_and_writes_and_old_grants(self):
        self.publish(self.port)
        self.c.refresh_from_db()
        sharing.change_member(
            self.owner,
            self.c.id,
            self.g.id,
            {"version": self.c.version, "role": "player"},
        )
        self.assertNotIn("gm_notes", wiki.read_page(self.gm, self.c.id, self.port.id))
        with self.assertRaises(DomainError):
            wiki.read_page(self.gm, self.c.id, self.port.id, author_only=True)
        with self.assertRaises(DomainError):
            wiki.search_pages(self.gm, self.c.id, "Port", author_only=True)
        self.assertEqual(wiki.list_campaigns(self.gm, author_only=True), [])

    def test_invitation_is_digest_only_explicit_single_use_and_role_safe(self):
        result = sharing.invitations(self.owner, self.c.id, {"role": "player"})
        token = result["token"]
        i = CampaignInvitation.objects.get(pk=result["id"])
        self.assertNotIn(token, i.digest)
        self.assertEqual(len(i.digest), 64)
        sharing.invitation(self.other, {"token": token})
        self.assertFalse(self.c.members.filter(user=self.other).exists())
        sharing.invitation(self.other, {"token": token}, accept=True)
        self.assertEqual(self.c.members.get(user=self.other).role, "player")
        with self.assertRaises(DomainError):
            sharing.invitation(self.bob, {"token": token}, accept=True)
        listed = sharing.invitations(self.owner, self.c.id)
        self.assertNotIn(token, str(listed))
        self.assertEqual(listed[0]["accepted_by"], "other")
        another = sharing.invitations(self.owner, self.c.id, {"role": "gm"})["token"]
        with self.assertRaises(DomainError):
            sharing.invitation(self.other, {"token": another}, accept=True)
        self.assertEqual(self.c.members.get(user=self.other).role, "player")

    def test_invalid_expired_revoked_invitation(self):
        for values in ({}, {"token": None}, {"token": []}, {"token": "not-a-token"}):
            self.assertIn(
                self.as_user(self.other)
                .post("/api/campaign-invitations/accept/", values, format="json")
                .status_code,
                [400, 404],
            )
        for expired in (True, False):
            x = sharing.invitations(self.owner, self.c.id, {"role": "gm"})
            if expired:
                CampaignInvitation.objects.filter(pk=x["id"]).update(
                    expires_at=timezone.now() - timedelta(seconds=1)
                )
            else:
                sharing.revoke_invitation(self.owner, self.c.id, x["id"])
            with self.assertRaises(DomainError):
                sharing.invitation(self.other, {"token": x["token"]}, accept=True)

    def test_bad_roles_and_recipients_and_stale_publication_are_rejected(self):
        client = self.as_user(self.owner)
        for role in (None, [], {}, "owner"):
            self.assertEqual(
                client.post(
                    self.root + "invitations/", {"role": role}, format="json"
                ).status_code,
                400,
            )
        foreign = CampaignMember.objects.create(
            campaign=Campaign.objects.create(owner=self.other, title="Elsewhere"),
            user=self.alice,
            role="player",
        )
        for recipients in (
            None,
            {},
            ["bad"],
            [str(foreign.id)],
            [str(self.g.id)],
            [str(self.a.id)] * 2,
            [],
        ):
            self.assertEqual(
                client.post(
                    self.root + f"pages/{self.port.id}/publication/",
                    {"version": 1, "audience": "selected", "recipients": recipients},
                    format="json",
                ).status_code,
                400,
            )
        self.publish(self.port)
        self.assertEqual(
            client.post(
                self.root + f"pages/{self.port.id}/publication/",
                {"version": 1, "audience": "all"},
                format="json",
            ).status_code,
            409,
        )

    def test_leave_owner_guard_and_private_audit(self):
        sharing.leave(self.alice, self.c.id)
        self.assertFalse(self.c.members.filter(user=self.alice).exists())
        with self.assertRaises(DomainError):
            sharing.leave(self.owner, self.c.id)
        self.assertEqual(
            sharing.audit(self.owner, self.c.id)[0]["action"], "member.removed"
        )
        self.assertNotIn("SECRET", str(sharing.audit(self.owner, self.c.id)))

    def test_cogm_can_copy_into_own_adventure_only(self):
        import copy

        from .demo import DOCUMENT
        from .models import Scenario

        own = Scenario.objects.create(
            owner=self.gm, title="Co-GM adventure", document=copy.deepcopy(DOCUMENT)
        )
        other = Scenario.objects.create(
            owner=self.owner, title="Owner adventure", document=copy.deepcopy(DOCUMENT)
        )
        copied = wiki.import_to_scenario(
            self.gm,
            self.c.id,
            self.port.id,
            {"scenario_id": str(own.id), "version": 1, "source_version": 1},
        )
        self.assertEqual(copied["version"], 2)
        own.refresh_from_db()
        self.assertEqual(own.document["nodes"][-1]["secret"], "SECRET_PORT")
        with self.assertRaises(DomainError):
            wiki.import_to_scenario(
                self.gm,
                self.c.id,
                self.port.id,
                {"scenario_id": str(other.id), "version": 1, "source_version": 1},
            )
        other.refresh_from_db()
        self.assertEqual(other.version, 1)

    def test_legacy_inserts_get_database_defaults_during_code_rollback(self):
        page_id, relation_id = uuid.uuid4(), uuid.uuid4()
        with connection.cursor() as cursor:
            cursor.execute(
                "INSERT INTO stories_wikipage (id,campaign_id,title,kind,body,gm_notes,version,creation_fingerprint,updated_at) VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s)",
                [
                    page_id.hex,
                    self.c.id.hex,
                    "Legacy",
                    "lore",
                    "Body",
                    "Private",
                    1,
                    "",
                    timezone.now(),
                ],
            )
            cursor.execute(
                "INSERT INTO stories_wikirelation (id,campaign_id,source_id,target_id,label) VALUES (%s,%s,%s,%s,%s)",
                [
                    relation_id.hex,
                    self.c.id.hex,
                    self.port.id.hex,
                    page_id.hex,
                    "Legacy edge",
                ],
            )
        page = WikiPage.objects.get(pk=page_id)
        edge = WikiRelation.objects.get(pk=relation_id)
        self.assertEqual(page.audience, "gm")
        self.assertIsNone(page.published)
        self.assertEqual(edge.audience, "gm")
        self.assertEqual(edge.version, 1)
        self.assertIsNone(edge.published)

    def test_invitation_member_limits(self):
        for _ in range(20):
            sharing.invitations(self.owner, self.c.id, {"role": "player"})
        with self.assertRaises(DomainError):
            sharing.invitations(self.owner, self.c.id, {"role": "player"})


@skipUnless(connection.vendor == "postgresql", "Requires PostgreSQL locks")
class SharingConcurrencyTests(TransactionTestCase):
    def setUp(self):
        self.owner, self.a, self.b = [
            get_user_model().objects.create_user(n) for n in ["owner", "a", "b"]
        ]
        self.c = Campaign.objects.create(owner=self.owner, title="Race")

    def test_invitation_has_one_consumer(self):
        token = sharing.invitations(self.owner, self.c.id, {"role": "player"})["token"]
        barrier = Barrier(2)

        def worker(user):
            close_old_connections()
            try:
                barrier.wait(timeout=10)
                sharing.invitation(user, {"token": token}, accept=True)
                return 200
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(worker, [self.a, self.b]))
        self.assertEqual(sorted(results), [200, 404])
        self.assertEqual(self.c.members.count(), 1)

    def test_revoked_writer_waiting_on_campaign_is_denied(self):
        m = CampaignMember.objects.create(campaign=self.c, user=self.a, role="gm")
        p = WikiPage.objects.create(campaign=self.c, title="Untouched")
        started = Event()

        def worker():
            close_old_connections()
            started.set()
            try:
                wiki.save_page(
                    self.a, self.c.id, p.id, {"version": 1, "title": "Bad late write"}
                )
                return 200
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=1) as pool:
            with transaction.atomic():
                Campaign.objects.select_for_update().get(pk=self.c.id)
                task = pool.submit(worker)
                self.assertTrue(started.wait(timeout=5))
                sharing.change_member(self.owner, self.c.id, m.id)
            self.assertEqual(task.result(timeout=10), 404)
        p.refresh_from_db()
        self.assertEqual(p.title, "Untouched")

    def test_publish_and_edit_share_one_version_gate(self):
        p = WikiPage.objects.create(campaign=self.c, title="Initial", body="Reviewed")
        barrier = Barrier(2)

        def worker(publish):
            close_old_connections()
            try:
                barrier.wait(timeout=10)
                if publish:
                    sharing.publish(
                        self.owner, self.c.id, p.id, {"version": 1, "audience": "all"}
                    )
                else:
                    wiki.save_page(
                        self.owner,
                        self.c.id,
                        p.id,
                        {"version": 1, "title": "Changed", "body": "Unreviewed"},
                    )
                return 200
            except DomainError as e:
                return e.status
            finally:
                close_old_connections()

        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(worker, [True, False]))
        self.assertEqual(sorted(results), [200, 409])
        p.refresh_from_db()
        if p.published:
            self.assertEqual(p.published["body"], "Reviewed")
