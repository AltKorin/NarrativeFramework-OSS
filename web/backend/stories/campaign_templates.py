"""Import a portable campaign template as a new, private campaign.

Templates contain local page IDs and initial state, never memberships,
publications, arbitrary saved history or commands to execute against other data.
"""
import copy
import json
import uuid
from django.contrib.auth import get_user_model
from django.db import IntegrityError, transaction
from . import narrative_engine as engine, wiki
from .domain_errors import DomainError
from .models import Campaign, WikiPage, WikiRelation

SCHEMA = "narrative-campaign-template/1"


def sequence(value, maximum, label):
    if not isinstance(value, list) or len(value) > maximum:
        raise DomainError(f"Invalid template {label}")
    return value


def mapping(value):
    if not isinstance(value, dict):
        raise DomainError("Expected a template object")
    return value


def prepare(document, campaign_id):
    wiki.body_object(document)
    if document.get("schema") != SCHEMA:
        raise DomainError("Unsupported campaign template schema")
    if len(json.dumps(document, ensure_ascii=False).encode()) > 450_000:
        raise DomainError("Campaign template exceeds limits")
    title = wiki.text(document.get("title"), 200, True)
    summary = wiki.text(document.get("summary", ""), 4000)
    pages, ids, actors = [], {}, set()
    for item in sequence(document.get("pages"), 100, "pages"):
        mapping(item)
        key = engine.text(item.get("id"), 80)
        if key in ids:
            raise DomainError("Duplicate template page ID")
        page_id = uuid.uuid5(campaign_id, "page/" + key)
        ids[key] = str(page_id)
        values = wiki.page_values(item)
        pages.append(WikiPage(id=page_id, campaign_id=campaign_id,
                             creation_fingerprint=wiki.fingerprint(values), **values))
        if values["kind"] in {"pc", "npc", "organization"}:
            actors.add(key)

    def reference(key, actor=False):
        if not isinstance(key, str) or key not in (actors if actor else ids):
            raise DomainError("Unknown template character" if actor else "Unknown template page")
        return ids[key]

    relations, seen = [], set()
    for item in sequence(document.get("relations", []), 200, "relations"):
        mapping(item)
        source, target = reference(item.get("source")), reference(item.get("target"))
        label = wiki.text(item.get("label"), 80, True)
        if source == target or (source, target, label) in seen:
            raise DomainError("Invalid or duplicate template relationship")
        seen.add((source, target, label))
        relations.append(WikiRelation(id=uuid.uuid5(campaign_id, f"relation/{len(relations)}"),
                                     campaign_id=campaign_id, source_id=source, target_id=target, label=label))
    initial = mapping(document.get("initial", {}))
    state = engine.initial_state()
    state["day"] = engine.day(initial.get("day", 1))
    state["flags"] = engine.strings(initial.get("flags", []))
    for scope in ("resources", "variables"):
        for key, value in mapping(initial.get(scope, {})).items():
            state[scope][engine.text(key)] = engine.number(value)
    for actor, stats in mapping(initial.get("actors", {})).items():
        state["actors"][reference(actor, True)] = {
            engine.text(key): engine.number(value) for key, value in mapping(stats).items()}
    for source, targets in mapping(initial.get("relationships", {})).items():
        state["relationships"][reference(source, True)] = {
            reference(target, True): {engine.text(key): engine.number(value)
                                     for key, value in mapping(values).items()}
            for target, values in mapping(targets).items()}
    event_ids = set()
    for item in sequence(document.get("events", []), 100, "events"):
        # Normalization rejects malformed fields before resolving page references.
        event = engine.event(copy.deepcopy(item))
        if event["id"] in event_ids:
            raise DomainError("Duplicate template event ID")
        event_ids.add(event["id"])
        event["id"] = str(uuid.uuid5(campaign_id, "event/" + event["id"]))
        event["InvolvedActors"] = [reference(key, True) for key in event["InvolvedActors"]]
        for condition in event["conditions"]:
            if condition["scope"] == "actors":
                condition["actorId"] = reference(condition["actorId"], True)
        for choice in event["choices"]:
            for effect in choice["outcomes"]:
                for field in ("actorId", "otherActorId"):
                    if field in effect:
                        effect[field] = reference(effect[field], True)
        state = engine.execute(state, {"action": "add_event", "event": event})
    state["history"] = []  # Preparing an example is not a history of played decisions.
    return title, summary, pages, relations, state


@transaction.atomic
def create(user, data):
    wiki.body_object(data)
    campaign_id = wiki.identifier(data.get("id"))
    document = data.get("document")
    title, summary, pages, relations, state = prepare(document, campaign_id)
    receipt = wiki.fingerprint({"template": document})
    get_user_model().objects.select_for_update().get(pk=user.pk)
    existing = Campaign.objects.filter(pk=campaign_id).first()
    if existing:
        if existing.owner_id != user.pk or existing.creation_fingerprint != receipt:
            raise DomainError("Creation conflict", 409)
        return wiki.campaign_json(existing), False
    if Campaign.objects.filter(owner=user).count() >= 20:
        raise DomainError("Campaign limit reached", 409)
    try:
        with transaction.atomic():
            campaign = Campaign.objects.create(id=campaign_id, owner=user, title=title,
                                               summary=summary, runtime=state,
                                               creation_fingerprint=receipt)
            WikiPage.objects.bulk_create(pages)
            WikiRelation.objects.bulk_create(relations)
    except IntegrityError:
        raise DomainError("Creation conflict", 409) from None
    wiki.touch(campaign, user, "campaign.template_imported", campaign.id)
    return wiki.campaign_json(campaign), True
