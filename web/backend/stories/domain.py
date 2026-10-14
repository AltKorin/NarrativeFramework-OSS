import json, math, copy, hashlib
from collections import deque
from django.db import transaction
from django.db.models import F
from .models import Scenario


from .domain_errors import DomainError
from .transport import bounded_json, storage_string


KINDS = {"scene", "npc", "location", "clue"}
LINKS = {"leads_to", "reveals", "involves", "located_at"}


def validate_document(doc):
    if not isinstance(doc, dict) or doc.get("schema") != "narrative-web/1":
        raise DomainError("Unsupported schema; expected narrative-web/1")
    bounded_json(doc)
    if len(json.dumps(doc, ensure_ascii=False, allow_nan=False).encode()) > 256_000:
        raise DomainError("Scenario exceeds 256 KB")
    nodes, edges = doc.get("nodes"), doc.get("edges")
    if (
        not isinstance(nodes, list)
        or not isinstance(edges, list)
        or not 1 <= len(nodes) <= 250
        or len(edges) > 1000
    ):
        raise DomainError("Use 1–250 nodes and at most 1000 links")
    doc = copy.deepcopy(doc)
    nodes, edges = doc["nodes"], doc["edges"]
    ids = set()
    for n in nodes:
        if (
            not isinstance(n, dict)
            or not isinstance(n.get("id"), str)
            or not 1 <= len(n["id"]) <= 80
            or n["id"] in ids
        ):
            raise DomainError("Nodes need unique string IDs (1–80 characters)")
        ids.add(n["id"])
        if (
            not isinstance(n.get("kind"), str)
            or n["kind"] not in KINDS
            or not isinstance(n.get("title"), str)
            or not n["title"].strip()
            or len(n["title"]) > 200
        ):
            raise DomainError("Invalid node kind or title")
        for coord in ("x", "y"):
            value = n.get(coord, 0)
            if (
                not isinstance(value, (int, float))
                or isinstance(value, bool)
                or abs(value) > 100000
                or not math.isfinite(value)
            ):
                raise DomainError("Coordinates must be finite numbers within ±100000")
        for field in ("text", "secret"):
            if not isinstance(n.get(field, ""), str) or len(n.get(field, "")) > 12000:
                raise DomainError("Text fields must contain at most 12000 characters")
        for field, default in (("text", ""), ("secret", ""), ("x", 0), ("y", 0)):
            n.setdefault(field, default)
    edge_ids = set()
    for e in edges:
        if (
            not isinstance(e, dict)
            or not isinstance(e.get("id"), str)
            or not 1 <= len(e["id"]) <= 80
            or e["id"] in edge_ids
            or not all(isinstance(e.get(k), str) for k in ("source", "target", "kind"))
            or e["source"] not in ids
            or e["target"] not in ids
            or e["kind"] not in LINKS
        ):
            raise DomainError("Invalid or duplicate link, or missing endpoint")
        edge_ids.add(e["id"])
    if not isinstance(doc.get("start"), str) or doc["start"] not in {
        n["id"] for n in nodes if n["kind"] == "scene"
    }:
        raise DomainError("Starting node must be a scene")
    if len(json.dumps(doc, ensure_ascii=False, allow_nan=False).encode()) > 256_000:
        raise DomainError("Scenario exceeds 256 KB")
    return doc


def inspect_document(doc, language="pl"):
    validate_document(doc)
    nodes = {n["id"]: n for n in doc["nodes"]}
    graph = {k: [] for k in nodes}
    for e in doc["edges"]:
        if (
            e["kind"] == "leads_to"
            and nodes[e["source"]]["kind"] == nodes[e["target"]]["kind"] == "scene"
        ):
            graph[e["source"]].append(e["target"])
    seen = set()
    todo = deque([doc["start"]])
    while todo:
        k = todo.popleft()
        if k in seen:
            continue
        seen.add(k)
        todo.extend(graph[k])
    issues = []
    for n in nodes.values():
        if n["kind"] == "scene" and n["id"] not in seen:
            issues.append(
                {
                    "node": n["id"],
                    "code": "unreachable_scene",
                    "message": "Scene is unreachable from the start through leads_to links."
                    if language == "en"
                    else "Scena nieosiągalna ze sceny początkowej przez leads_to.",
                }
            )
        if n["kind"] == "clue":
            sources = {
                e["source"]
                for e in doc["edges"]
                if e["target"] == n["id"] and e["kind"] == "reveals"
            }
            if len(sources) < 3:
                issues.append(
                    {
                        "node": n["id"],
                        "code": "clue_sources",
                        "count": len(sources),
                        "message": f"This clue has {len(sources)} sources. Consider three independent ways to discover it."
                        if language == "en"
                        else f"Wskazówka ma {len(sources)} źródła. Rozważ trzy niezależne drogi odkrycia.",
                    }
                )
    return issues


def serialize(s):
    return {
        "id": str(s.id),
        "title": s.title,
        "version": s.version,
        "document": s.document,
        "is_demo": s.is_demo,
    }


def scenario_creation_fingerprint(title, document):
    return hashlib.sha256(
        json.dumps(
            {"title": title, "document": document},
            sort_keys=True,
            ensure_ascii=True,
            allow_nan=False,
        ).encode()
    ).hexdigest()


def create_scenario(user, creation_id, title, document):
    document = validate_document(document)
    storage_string(title)
    if not title.strip() or len(title) > 200:
        raise DomainError("Invalid title")
    signature = scenario_creation_fingerprint(title, document)

    def replay(existing):
        if existing.owner_id != user.pk or existing.is_demo:
            raise DomainError("ID unavailable", 409)
        # Legacy records have no creation receipt; compare their current content.
        stored = existing.creation_fingerprint or scenario_creation_fingerprint(
            existing.title, validate_document(existing.document)
        )
        if stored != signature:
            raise DomainError("Operation ID reused with different content", 409)
        return serialize(existing), False

    from django.contrib.auth import get_user_model
    from django.db import IntegrityError

    with transaction.atomic():
        # Serialize the per-owner quota and same-owner duplicate requests.
        get_user_model().objects.select_for_update().get(pk=user.pk)
        existing = Scenario.objects.filter(pk=creation_id).first()
        if existing:
            return replay(existing)
        if Scenario.objects.filter(owner=user, is_demo=False).count() >= 50:
            raise DomainError("Scenario quota reached (50)")
        try:
            with transaction.atomic():
                result = Scenario.objects.create(
                    id=creation_id,
                    owner=user,
                    title=title,
                    document=document,
                    creation_fingerprint=signature,
                )
        except IntegrityError:
            return replay(Scenario.objects.get(pk=creation_id))
        return serialize(result), True


def save_document(user, scenario_id, version, title, document):
    document = validate_document(document)
    storage_string(title)
    if not isinstance(title, str) or not title.strip() or len(title) > 200:
        raise DomainError("Title must contain 1–200 characters")
    if not isinstance(version, int) or isinstance(version, bool):
        raise DomainError("Expected integer version")
    with transaction.atomic():
        count = Scenario.objects.filter(
            pk=scenario_id, owner=user, is_demo=False, version=version
        ).update(title=title, document=document, version=F("version") + 1)
        if not count:
            if not Scenario.objects.filter(
                pk=scenario_id, owner=user, is_demo=False
            ).exists():
                raise DomainError("Scenario not found", 404)
            raise DomainError(
                "Version conflict. Reload before saving; your changes were not applied.",
                409,
            )
        return serialize(Scenario.objects.get(pk=scenario_id, owner=user))


def markdown(s, language="pl"):
    english = language == "en"
    labels = (
        {"scene": "Scene", "npc": "NPC", "location": "Location", "clue": "Clue"}
        if english
        else {
            "scene": "Scena",
            "npc": "Postać",
            "location": "Miejsce",
            "clue": "Wskazówka",
        }
    )
    links = (
        {
            "leads_to": "leads to",
            "reveals": "reveals",
            "involves": "involves",
            "located_at": "located at",
        }
        if english
        else {
            "leads_to": "prowadzi do",
            "reveals": "ujawnia",
            "involves": "angażuje",
            "located_at": "mieści się w",
        }
    )
    lines = [
        f"# {s.title}",
        "",
        "GM material — includes secrets."
        if english
        else "Materiały dla MG — zawierają sekrety.",
        "",
    ]
    for n in s.document["nodes"]:
        lines.extend(
            [f"## {n['title']} ({labels[n['kind']]})", "", n.get("text", ""), ""]
        )
        if n.get("secret"):
            lines.extend(
                ["### GM secret" if english else "### Sekret MG", n["secret"], ""]
            )
        for e in s.document["edges"]:
            if e["source"] == n["id"]:
                target = next(
                    x["title"] for x in s.document["nodes"] if x["id"] == e["target"]
                )
                lines.append(f"- {links[e['kind']]}: {target}")
        lines.append("")
    return "\n".join(lines)
