"""Campaign images: re-encoded, campaign-private, visibility derived from where they are used.

Design: web/docs/design/obrazy-kampanii.md. Images never get a public path; every read
re-checks membership. Players see an image only through something already shared with them.
"""

import hashlib
import io
import uuid

from django.db import transaction
from django.db.models import Sum
from PIL import Image, ImageOps

from . import wiki
from .campaign_access import campaign_access, visible_records
from .domain_errors import DomainError
from .models import CampaignImage, CampaignRuleset, WikiPage

MAX_INPUT = 8 * 1024 * 1024
MAX_PIXELS = 40_000_000
MAX_SIDE = 1600
MAX_IMAGES = 300
MAX_TOTAL = 200 * 1024 * 1024
FORMATS = {"PNG", "JPEG", "WEBP", "GIF"}


def process(raw):
    """Decode and re-encode as WebP without metadata; the original bytes are never stored."""
    if not isinstance(raw, (bytes, bytearray)) or not raw:
        raise DomainError("Choose an image file")
    if len(raw) > MAX_INPUT:
        raise DomainError("Image exceeds 8 MB", 413)
    Image.MAX_IMAGE_PIXELS = MAX_PIXELS
    try:
        with Image.open(io.BytesIO(raw)) as source:
            if source.format not in FORMATS:
                raise DomainError("Use a PNG, JPEG, WebP or GIF image")
            if source.width * source.height > MAX_PIXELS:
                raise DomainError("Image has too many pixels")
            source.seek(0)
            image = ImageOps.exif_transpose(source)  # keep orientation, drop EXIF itself
            image = image.convert("RGBA" if "A" in image.getbands() or image.mode == "P" else "RGB")
            image.thumbnail((MAX_SIDE, MAX_SIDE))
            out = io.BytesIO()
            image.save(out, "WEBP", quality=82, method=4)
    except DomainError:
        raise
    except (Image.DecompressionBombError, Image.DecompressionBombWarning):
        raise DomainError("Image has too many pixels") from None
    except Exception:
        raise DomainError("Not a readable image") from None
    return out.getvalue(), image.width, image.height


def image_json(img, usage=None):
    result = {"id": str(img.id), "caption": img.caption, "gallery": img.gallery,
              "width": img.width, "height": img.height, "size": img.size,
              "created_at": img.created_at.isoformat()}
    if usage is not None:
        result["usage"] = usage
    return result


def ruleset_image_ids(document):
    """IDs referenced by `image` fields of roles, catalog items and methods in a pack."""
    found = set()
    for key in ("roles", "catalog", "methods"):
        for item in (document or {}).get(key, []) if isinstance(document, dict) else []:
            if isinstance(item, dict) and isinstance(item.get("image"), str):
                found.add(item["image"])
    return found


def check_ruleset_images(campaign, document):
    ids = ruleset_image_ids(document)
    try:
        wanted = {uuid.UUID(x) for x in ids}
    except ValueError:
        raise DomainError("Ruleset: image must be a campaign image ID") from None
    if wanted and campaign.images.filter(id__in=wanted).count() != len(wanted):
        raise DomainError("Ruleset: unknown campaign image")


def visible_ids(c, member):
    """Image IDs a player may load: player gallery, cover, published page images, ruleset."""
    ids = set(c.images.filter(gallery="players").values_list("id", flat=True))
    if c.cover_id:
        ids.add(c.cover_id)
    for page in visible_records(c.pages.all(), "player", member):
        image = (page.published or {}).get("image")
        if image:
            ids.add(uuid.UUID(image))
    ruleset = CampaignRuleset.objects.filter(campaign=c).first()
    if ruleset:
        ids |= {uuid.UUID(x) for x in ruleset_image_ids(ruleset.document)}
    return ids


def usage(c, img):
    ruleset = CampaignRuleset.objects.filter(campaign=c).first()
    return {"cover": c.cover_id == img.id,
            "pages": [str(p) for p in img.pages.values_list("id", flat=True)],
            "ruleset": bool(ruleset and str(img.id) in ruleset_image_ids(ruleset.document))}


def gm_campaign(user, campaign_id):
    return campaign_access(user, campaign_id, minimum="gm")


@transaction.atomic
def list_images(user, campaign_id):
    c = campaign_access(user, campaign_id, minimum="player")
    if c.wiki_role in ("owner", "gm"):
        return [image_json(i, usage(c, i)) for i in c.images.order_by("-created_at")]
    # The player gallery lists only images the GM shared to it; covers, page illustrations and
    # ruleset images stay loadable (read_image) but appear where they are used, not here.
    return [image_json(i) for i in c.images.filter(gallery="players").order_by("-created_at")]


@transaction.atomic
def read_image(user, campaign_id, image_id):
    c = campaign_access(user, campaign_id, minimum="player", lock=False)
    img = c.images.filter(pk=wiki.identifier(image_id)).first()
    if not img or (c.wiki_role not in ("owner", "gm") and img.id not in visible_ids(c, c.wiki_member)):
        raise DomainError("Image not found", 404)
    return bytes(img.data)


def caption_of(value):
    return wiki.text(value if value is not None else "", 200)


def gallery_of(value):
    if value not in ("gm", "players"):
        raise DomainError("Gallery must be gm or players")
    return value


@transaction.atomic
def upload(user, campaign_id, data):
    """`data`: id (client UUID), raw (bytes), caption, gallery. Identical retry returns the same image."""
    c = gm_campaign(user, campaign_id)
    image_id = wiki.identifier(data.get("id"))
    raw = data.get("raw")
    receipt = hashlib.sha256(bytes(raw or b"")).hexdigest()
    existing = CampaignImage.objects.filter(pk=image_id).first()
    if existing:
        if existing.campaign_id != c.id or existing.fingerprint != receipt:
            raise DomainError("Creation conflict", 409)
        return image_json(existing, usage(c, existing)), False
    caption, gallery = caption_of(data.get("caption")), gallery_of(data.get("gallery", "gm"))
    encoded, width, height = process(raw)
    if c.images.count() >= MAX_IMAGES:
        raise DomainError("Campaign image limit reached", 409)
    if (c.images.aggregate(total=Sum("size"))["total"] or 0) + len(encoded) > MAX_TOTAL:
        raise DomainError("Campaign image storage is full", 409)
    img = CampaignImage.objects.create(id=image_id, campaign=c, caption=caption, gallery=gallery,
                                       data=encoded, width=width, height=height, size=len(encoded),
                                       fingerprint=receipt)
    wiki.touch(c, user, "image.uploaded", img.id)
    return image_json(img, usage(c, img)), True


@transaction.atomic
def update_image(user, campaign_id, image_id, data):
    wiki.body_object(data)
    c = gm_campaign(user, campaign_id)
    img = c.images.filter(pk=wiki.identifier(image_id)).first()
    if not img:
        raise DomainError("Image not found", 404)
    img.caption = caption_of(data.get("caption", img.caption))
    img.gallery = gallery_of(data.get("gallery", img.gallery))
    img.save(update_fields=["caption", "gallery"])
    wiki.touch(c, user, "image.updated", img.id)
    return image_json(img, usage(c, img))


@transaction.atomic
def delete_image(user, campaign_id, image_id):
    c = gm_campaign(user, campaign_id)
    img = c.images.filter(pk=wiki.identifier(image_id)).first()
    if not img:
        raise DomainError("Image not found", 404)
    used = usage(c, img)
    if used["ruleset"]:
        raise DomainError("Image is used by the campaign rules; change the ruleset first", 409)
    img.delete()  # cover and page illustrations are detached by SET_NULL
    wiki.touch(c, user, "image.deleted", image_id)
    return {"deleted": True, "detached": {"cover": used["cover"], "pages": len(used["pages"])}}


@transaction.atomic
def set_cover(user, campaign_id, data):
    wiki.body_object(data)
    c = gm_campaign(user, campaign_id)
    image = data.get("image")
    c.cover = None if image is None else c.images.filter(pk=wiki.identifier(image)).first()
    if image is not None and not c.cover:
        raise DomainError("Image not found", 404)
    c.save(update_fields=["cover"])
    wiki.touch(c, user, "campaign.cover", c.id)
    return wiki.campaign_json(c)


@transaction.atomic
def set_page_image(user, campaign_id, page_id, data):
    """Attach a draft illustration; players see it after the next publication of the page."""
    wiki.body_object(data)
    c = gm_campaign(user, campaign_id)
    page = wiki.page_for(c, page_id)
    if type(data.get("version")) is not int or data["version"] != page.version:
        raise DomainError("Wiki version conflict; reload before saving", 409)
    image = data.get("image")
    page.image = None if image is None else c.images.filter(pk=wiki.identifier(image)).first()
    if image is not None and not page.image:
        raise DomainError("Image not found", 404)
    page.version += 1
    page.save(update_fields=["image", "version"])
    wiki.touch(c, user, "page.image", page.id)
    return wiki.page_json(WikiPage.objects.get(pk=page.id))
