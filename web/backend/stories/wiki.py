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

from .domain import save_document, validate_document
from .domain_errors import DomainError
from .models import Campaign, Scenario, WikiPage, WikiRelation
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
    qs = Campaign.objects.select_for_update() if lock else Campaign.objects
    result = qs.filter(pk=identifier(campaign_id), owner=user).first()
    if not result:
        raise DomainError("Campaign not found", 404)
    return result


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
        "version": c.version,
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
        result.update(body=p.body, gm_notes=p.gm_notes)
    return result


def list_campaigns(user):
    return [
        campaign_json(c)
        for c in Campaign.objects.filter(owner=user).order_by("title", "id")
    ]


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
def campaign_detail(user, campaign_id):
    c = campaign_for(user, campaign_id, True)
    result = campaign_json(c)
    result["pages"] = [page_json(p, False) for p in c.pages.order_by("title", "id")]
    result["relations"] = [
        {
            "id": str(r.id),
            "source": str(r.source_id),
            "target": str(r.target_id),
            "label": r.label,
        }
        for r in c.relations.order_by("id")
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


def touch(c):
    c.version += 1
    c.save(update_fields=["version", "updated_at"])


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
    touch(c)
    return page_json(p), True


@transaction.atomic
def read_page(user, campaign_id, page_id):
    c = campaign_for(user, campaign_id, True)
    p = page_for(c, page_id)
    result = page_json(p)
    result["relations"] = [
        {
            "id": str(r.id),
            "source": str(r.source_id),
            "target": str(r.target_id),
            "label": r.label,
        }
        for r in c.relations.filter(Q(source=p) | Q(target=p)).order_by("id")
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
    touch(c)
    return page_json(p)


@transaction.atomic
def delete_page(user, campaign_id, page_id, version):
    c = campaign_for(user, campaign_id, True)
    p = page_for(c, page_id)
    if type(version) is not int or version != p.version:
        raise DomainError("Wiki version conflict; reload before saving", 409)
    p.delete()
    touch(c)
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
    touch(c)
    return {"id": str(r.id)}, True


@transaction.atomic
def delete_relation(user, campaign_id, relation_id):
    c = campaign_for(user, campaign_id, True)
    c.relations.filter(pk=identifier(relation_id)).delete()
    touch(c)
    return {"deleted": True}


def search_pages(user, campaign_id, query):
    c = campaign_for(user, campaign_id)
    query = text(query, 200)
    return [
        page_json(p, False)
        for p in c.pages.filter(
            Q(title__icontains=query) | Q(body__icontains=query)
        ).order_by("title", "id")[:100]
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
def export_campaign(user, campaign_id):
    c = campaign_for(user, campaign_id, True)
    result = campaign_detail(user, campaign_id)
    result["schema"] = "narrative-wiki/1"
    result["pages"] = [page_json(p) for p in c.pages.order_by("title", "id")]
    return result


@transaction.atomic
def update_campaign(user, campaign_id, data):
    body_object(data)
    c = campaign_for(user, campaign_id, True)
    if type(data.get("version")) is not int or data["version"] != c.version:
        raise DomainError("Campaign version conflict; reload before saving", 409)
    c.title = text(data.get("title"), 200, True)
    c.summary = text(data.get("summary", ""), 4000)
    c.save(update_fields=["title", "summary"])
    touch(c)
    return campaign_json(c)


@transaction.atomic
def delete_campaign(user, campaign_id, data):
    body_object(data)
    c = campaign_for(user, campaign_id, True)
    if type(data.get("version")) is not int or data["version"] != c.version:
        raise DomainError("Campaign version conflict; reload before saving", 409)
    c.delete()
    return {"deleted": True}
