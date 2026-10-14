"""Campaign wiki invariants, shared by REST and author MCP.

All campaign mutations serialize on the campaign row. Pages additionally require
an expected version. Import is an explicit scenario snapshot, never a live alias.
"""

import copy
import hashlib
import json
import uuid

from django.contrib.auth import get_user_model
from django.db import IntegrityError, transaction
from django.db.models import Q

from .campaign_access import (
    campaign_access,
    projection,
    public_page,
    public_relation,
    visible_records,
)
from .domain import save_document, validate_document
from .domain_errors import DomainError
from .models import Campaign, CampaignAuditEvent, Scenario, WikiPage, WikiRelation
from .transport import bounded_json, storage_string

KINDS = {"npc", "location", "organization", "item", "lore", "journal", "scene", "clue"}


def identifier(value):
    try:
        return uuid.UUID(str(value))
    except (ValueError, TypeError, AttributeError):
        raise DomainError("Invalid UUID") from None


def text(value, limit, required=False):
    if not isinstance(value, str):
        raise DomainError("Expected text")
    storage_string(value)
    value = value.strip() if required else value
    if len(value) > limit or required and not value:
        raise DomainError("Invalid wiki text length")
    return value


def body_object(data):
    if not isinstance(data, dict):
        raise DomainError("Expected a JSON object")
    bounded_json(data)
    return data


def fingerprint(data):
    return hashlib.sha256(
        json.dumps(data, sort_keys=True, ensure_ascii=False).encode()
    ).hexdigest()


def campaign_for(user, campaign_id, lock=False):
    return campaign_access(user, campaign_id, lock=lock, minimum="gm")


def page_for(campaign, page_id):
    page = WikiPage.objects.filter(pk=identifier(page_id), campaign=campaign).first()
    if not page:
        raise DomainError("Wiki page not found", 404)
    return page


def campaign_json(c):
    return {
        "id": str(c.id),
        "title": c.title,
        "summary": c.summary,
        "version": 0 if getattr(c, "wiki_role", "owner") == "player" else c.version,
        "role": getattr(c, "wiki_role", "owner"),
    }


def page_json(p, full=True):
    result = {
        "id": str(p.id),
        "campaign_id": str(p.campaign_id),
        "title": p.title,
        "kind": p.kind,
        "version": p.version,
    }
    if full:
        result.update(
            body=p.body,
            gm_notes=p.gm_notes,
            audience=p.audience,
            published=p.published,
            recipients=[str(x.id) for x in p.recipients.all()],
        )
    return result


def list_campaigns(user, author_only=False):
    qs = (
        Campaign.objects.filter(Q(owner=user) | Q(members__user=user))
        .distinct()
        .order_by("title", "id")
    )
    result = []
    for c in qs:
        from .campaign_access import role_for

        c.wiki_role, c.wiki_member = role_for(c, user)
        if c.wiki_role and (not author_only or c.wiki_role != "player"):
            result.append(campaign_json(c))
    return result


def relation_json(r):
    return {
        "id": str(r.id),
        "source": str(r.source_id),
        "target": str(r.target_id),
        "label": r.label,
        "version": r.version,
        "audience": r.audience,
        "published": r.published,
        "recipients": [str(x.id) for x in r.recipients.all()],
    }


def projected_records(c, preview=None, full=False):
    role, member = projection(c, preview)
    pages_qs = (
        visible_records(c.pages.all(), role, member)
        .prefetch_related("recipients")
        .order_by("title", "id")
    )
    if not full:
        pages_qs = pages_qs.defer("body", "gm_notes")
    pages = list(pages_qs)
    ids = [p.id for p in pages]
    relations = list(
        visible_records(
            c.relations.filter(source_id__in=ids, target_id__in=ids), role, member
        )
        .prefetch_related("recipients")
        .order_by("id")
    )
    # Order players by the published title, not the hidden draft.
    if role == "player":
        pages.sort(key=lambda p: (p.published["title"].casefold(), str(p.id)))
    return role, member, pages, relations


@transaction.atomic
def create_campaign(user, data):
    body_object(data)
    creation_id = identifier(data.get("id"))
    values = {
        "title": text(data.get("title"), 200, True),
        "summary": text(data.get("summary", ""), 4000),
    }
    receipt = fingerprint(values)
    get_user_model().objects.select_for_update().get(pk=user.pk)
    existing = Campaign.objects.filter(pk=creation_id).first()
    if existing:
        if existing.owner_id != user.pk or existing.creation_fingerprint != receipt:
            raise DomainError("Creation conflict", 409)
        return campaign_json(existing), False
    if Campaign.objects.filter(owner=user).count() >= 20:
        raise DomainError("Campaign limit reached", 409)
    try:
        with transaction.atomic():
            c = Campaign.objects.create(
                id=creation_id, owner=user, creation_fingerprint=receipt, **values
            )
    except IntegrityError:
        raise DomainError("Creation conflict", 409) from None
    return campaign_json(c), True


@transaction.atomic
def campaign_detail(user, campaign_id, preview=None):
    c = campaign_access(user, campaign_id, minimum="player")
    role, _member, pages, relations = projected_records(c, preview)
    result = campaign_json(c)
    result.update(
        role=role,
        version=0 if role == "player" else c.version,
        preview=bool(preview),
        actual_role=c.wiki_role,
    )
    result["pages"] = [
        public_page(p, False) if role == "player" else page_json(p, False)
        for p in pages
    ]
    result["relations"] = [
        public_relation(r) if role == "player" else relation_json(r) for r in relations
    ]
    return result


def page_values(data):
    body_object(data)
    kind = data.get("kind", "lore")
    if not isinstance(kind, str) or kind not in KINDS:
        raise DomainError("Invalid wiki category")
    return {
        "title": text(data.get("title"), 200, True),
        "kind": kind,
        "body": text(data.get("body", ""), 12000),
        "gm_notes": text(data.get("gm_notes", ""), 12000),
    }


def touch(c, user, action="wiki.changed", resource=""):
    c.version += 1
    c.save(update_fields=["version", "updated_at"])
    CampaignAuditEvent.objects.create(
        campaign=c,
        actor=user,
        action=action,
        resource_id=str(resource),
        version=c.version,
    )


@transaction.atomic
def create_page(user, campaign_id, data):
    values = page_values(data)
    page_id = identifier(data.get("id"))
    receipt = fingerprint(values)
    c = campaign_for(user, campaign_id, True)
    existing = WikiPage.objects.filter(pk=page_id).first()
    if existing:
        if existing.campaign_id != c.id or existing.creation_fingerprint != receipt:
            raise DomainError("Creation conflict", 409)
        return page_json(existing), False
    if c.pages.count() >= 1000:
        raise DomainError("Wiki page limit reached", 409)
    try:
        with transaction.atomic():
            p = WikiPage.objects.create(
                id=page_id, campaign=c, creation_fingerprint=receipt, **values
            )
    except IntegrityError:
        raise DomainError("Creation conflict", 409) from None
    touch(c, user, "page.created", p.id)
    return page_json(p), True


@transaction.atomic
def read_page(user, campaign_id, page_id, preview=None, author_only=False):
    c = campaign_access(user, campaign_id, minimum="gm" if author_only else "player")
    role, member = projection(c, preview)
    pages = visible_records(c.pages.all(), role, member)
    p = pages.filter(pk=identifier(page_id)).prefetch_related("recipients").first()
    if not p:
        raise DomainError("Wiki page not found", 404)
    relations = (
        visible_records(
            c.relations.filter(
                source_id__in=pages.values("id"), target_id__in=pages.values("id")
            ),
            role,
            member,
        )
        .filter(Q(source=p) | Q(target=p))
        .prefetch_related("recipients")
        .order_by("id")
    )
    result = public_page(p) if role == "player" else page_json(p)
    result["relations"] = [
        (public_relation(r) if role == "player" else relation_json(r))
        for r in relations
    ]
    return result


@transaction.atomic
def save_page(user, campaign_id, page_id, data):
    values = page_values(data)
    c = campaign_for(user, campaign_id, True)
    p = page_for(c, page_id)
    version = data.get("version")
    if type(version) is not int or version != p.version:
        raise DomainError("Wiki version conflict; reload before saving", 409)
    for key, value in values.items():
        setattr(p, key, value)
    p.version += 1
    p.save()
    touch(c, user, "page.saved", p.id)
    return page_json(p)


@transaction.atomic
def delete_page(user, campaign_id, page_id, version):
    c = campaign_for(user, campaign_id, True)
    p = page_for(c, page_id)
    if type(version) is not int or version != p.version:
        raise DomainError("Wiki version conflict; reload before saving", 409)
    p.delete()
    touch(c, user, "page.deleted", page_id)
    return {"deleted": True}


@transaction.atomic
def create_relation(user, campaign_id, data):
    body_object(data)
    c = campaign_for(user, campaign_id, True)
    source = page_for(c, data.get("source"))
    target = page_for(c, data.get("target"))
    label = text(data.get("label"), 80, True)
    relation_id = identifier(data.get("id"))
    if source.id == target.id:
        raise DomainError("Choose another wiki page")
    existing = WikiRelation.objects.filter(pk=relation_id).first()
    if existing:
        if (
            existing.campaign_id,
            existing.source_id,
            existing.target_id,
            existing.label,
        ) != (c.id, source.id, target.id, label):
            raise DomainError("Creation conflict", 409)
        return {"id": str(existing.id)}, False
    if c.relations.count() >= 2000:
        raise DomainError("Wiki relation limit reached", 409)
    duplicate = c.relations.filter(source=source, target=target, label=label).first()
    if duplicate:
        return {"id": str(duplicate.id)}, False
    try:
        with transaction.atomic():
            r = WikiRelation.objects.create(
                id=relation_id, campaign=c, source=source, target=target, label=label
            )
    except IntegrityError:
        raise DomainError("Creation conflict", 409) from None
    touch(c, user, "relation.created", r.id)
    return {"id": str(r.id)}, True


@transaction.atomic
def delete_relation(user, campaign_id, relation_id):
    c = campaign_for(user, campaign_id, True)
    c.relations.filter(pk=identifier(relation_id)).delete()
    touch(c, user, "relation.deleted", relation_id)
    return {"deleted": True}


@transaction.atomic
def search_pages(user, campaign_id, query, preview=None, author_only=False):
    c = campaign_access(user, campaign_id, minimum="gm" if author_only else "player")
    query = text(query, 200)
    role, member = projection(c, preview)
    pages = visible_records(c.pages.all(), role, member)
    if role == "player":
        pages = pages.filter(
            Q(published__title__icontains=query) | Q(published__body__icontains=query)
        ).order_by("published__title", "id")
    else:
        pages = pages.filter(
            Q(title__icontains=query) | Q(body__icontains=query)
        ).order_by("title", "id")
    return [
        (public_page(p, False) if role == "player" else page_json(p, False))
        for p in pages[:100]
    ]


@transaction.atomic
def import_to_scenario(user, campaign_id, page_id, data):
    body_object(data)
    c = campaign_for(user, campaign_id, True)
    p = page_for(c, page_id)
    if (
        type(data.get("source_version")) is not int
        or data["source_version"] != p.version
    ):
        raise DomainError("Wiki version conflict; reload before saving", 409)
    s = (
        Scenario.objects.select_for_update()
        .filter(pk=identifier(data.get("scenario_id")), owner=user, is_demo=False)
        .first()
    )
    if not s:
        raise DomainError("Scenario not found", 404)
    doc = copy.deepcopy(s.document)
    node_id = "wiki-" + str(p.id)
    if any(n["id"] == node_id for n in doc["nodes"]):
        raise DomainError("Wiki page already copied to this adventure", 409)
    doc["nodes"].append(
        {
            "id": node_id,
            "kind": p.kind
            if p.kind in {"npc", "location", "clue", "scene"}
            else "clue",
            "title": p.title,
            "text": p.body,
            "secret": p.gm_notes,
            "x": 300,
            "y": 200,
            "wiki_origin": {
                "campaign_id": str(c.id),
                "page_id": str(p.id),
                "version": p.version,
            },
        }
    )
    validate_document(doc)
    return save_document(user, s.id, data.get("version"), s.title, doc)


@transaction.atomic
def export_campaign(user, campaign_id, preview=None):
    c = campaign_access(user, campaign_id, minimum="player")
    role, _member, pages, _ = projected_records(c, preview, full=True)
    result = campaign_detail(user, campaign_id, preview)
    result["schema"] = (
        "narrative-wiki-player/1" if role == "player" else "narrative-wiki/2"
    )
    result["pages"] = [
        public_page(p) if role == "player" else page_json(p) for p in pages
    ]
    return result


@transaction.atomic
def update_campaign(user, campaign_id, data):
    body_object(data)
    c = campaign_access(user, campaign_id, minimum="owner")
    if type(data.get("version")) is not int or data["version"] != c.version:
        raise DomainError("Campaign version conflict; reload before saving", 409)
    c.title = text(data.get("title"), 200, True)
    c.summary = text(data.get("summary", ""), 4000)
    c.save(update_fields=["title", "summary"])
    touch(c, user, "campaign.updated", c.id)
    return campaign_json(c)


@transaction.atomic
def delete_campaign(user, campaign_id, data):
    body_object(data)
    c = campaign_access(user, campaign_id, minimum="owner")
    if type(data.get("version")) is not int or data["version"] != c.version:
        raise DomainError("Campaign version conflict; reload before saving", 409)
    c.delete()
    return {"deleted": True}
