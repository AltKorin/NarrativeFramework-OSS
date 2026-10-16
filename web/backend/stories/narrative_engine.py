"""Deterministic, engine-independent port of the Unity narrative core (MIT).

Sources: SimEventSO.IsAvailable, StoryArcCondition.Evaluate, Outcome and
SimulationManager. Unity objects become IDs; the human GM selects outcomes.
This is a supported subset, not a loader for arbitrary Unity ScriptableObjects.
"""

import copy
import math
import json

from .domain_errors import DomainError
from .transport import bounded_json

SCHEMA = "narrative-runtime/1"
OUTCOMES = {"AddFlag", "RemoveFlag", "ModifyResource", "ModifyNarrativeVariable",
            "SetNarrativeBoolTrue", "SetNarrativeBoolFalse", "SetRelationship",
            "ModifyActorAttribute"}


def initial_state():
    return {"schema": SCHEMA, "day": 1, "flags": [], "resources": {},
            "variables": {}, "bools": {}, "actors": {}, "relationships": {},
            "events": [], "completed": [], "history": []}


def text(value, maximum=200):
    if not isinstance(value, str) or not value.strip() or len(value) > maximum:
        raise DomainError("Invalid narrative text")
    return value.strip()


def number(value):
    if type(value) not in (int, float) or abs(value) > 1e12 or not math.isfinite(value):
        raise DomainError("Invalid narrative number")
    return value


def day(value):
    if type(value) is not int or not 1 <= value <= 99999:
        raise DomainError("Invalid narrative day")
    return value


def strings(value):
    if not isinstance(value, list) or len(value) > 100:
        raise DomainError("Invalid narrative list")
    return list(dict.fromkeys(text(x) for x in value))


def outcome(data):
    if not isinstance(data, dict) or not isinstance(data.get("type"), str) or data["type"] not in OUTCOMES:
        raise DomainError("Unsupported narrative outcome")
    # Never silently turn a probabilistic Unity outcome into a guaranteed effect.
    if type(data.get("probability", 1)) not in (int, float) or data.get("probability", 1) != 1:
        raise DomainError("Only deterministic outcomes are supported")
    result = {"type": data["type"], "targetId": text(data.get("targetId")),
              "value": number(data.get("value", 0)), "probability": 1}
    if data["type"] in {"SetRelationship", "ModifyActorAttribute"}:
        result["actorId"] = text(data.get("actorId"))
    if data["type"] == "SetRelationship":
        result["otherActorId"] = text(data.get("otherActorId"))
    return result


def event(data):
    if not isinstance(data, dict):
        raise DomainError("Invalid narrative event")
    probability = data.get("Probability", data.get("probability", 1))
    if type(probability) not in (int, float) or probability != 1:
        raise DomainError("Only deterministic events are supported")
    result = {"id": text(data.get("id"), 80), "title": text(data.get("title")),
              "description": data.get("description", ""),
              "MinTimeUnit": day(data.get("MinTimeUnit", 1)),
              "MaxTimeUnit": day(data.get("MaxTimeUnit", 30)),
              "requiredFlags": strings(data.get("requiredFlags", [])),
              "forbiddenFlags": strings(data.get("forbiddenFlags", [])),
              "InvolvedActors": strings(data.get("InvolvedActors", []))}
    if not isinstance(result["description"], str) or len(result["description"]) > 4000:
        raise DomainError("Invalid narrative description")
    if result["MinTimeUnit"] > result["MaxTimeUnit"]:
        raise DomainError("Narrative time window is reversed")
    conditions = data.get("conditions", [])
    if not isinstance(conditions, list) or len(conditions) > 50:
        raise DomainError("Invalid narrative conditions")
    result["conditions"] = []
    for c in conditions:
        if not isinstance(c, dict) or c.get("scope") not in ("resources", "variables", "actors"):
            raise DomainError("Unsupported narrative condition")
        if c.get("comparison") not in ("GreaterThan", "LessThan", "Equal"):
            raise DomainError("Invalid narrative comparison")
        item = {"scope": c["scope"], "key": text(c.get("key")),
                "comparison": c["comparison"], "value": number(c.get("value"))}
        if c["scope"] == "actors":
            item["actorId"] = text(c.get("actorId"))
        result["conditions"].append(item)
    choices = data.get("choices")
    if not isinstance(choices, list) or not 1 <= len(choices) <= 20:
        raise DomainError("Use 1–20 narrative choices")
    result["choices"] = []
    for c in choices:
        if not isinstance(c, dict) or not isinstance(c.get("outcomes"), list) or len(c["outcomes"]) > 50:
            raise DomainError("Invalid narrative choice")
        if c.get("goToNextDay") or c.get("nextEventId"):
            raise DomainError("Automatic choice transitions are not supported")
        result["choices"].append({"text": text(c.get("text")),
                                  "outcomes": [outcome(o) for o in c["outcomes"]]})
    return result


def blockers(state, item):
    """Inclusive Unity time window and all required / no forbidden flags."""
    reasons = []
    if item["id"] in state["completed"]:
        reasons.append({"code": "completed"})
    if state["day"] < item["MinTimeUnit"]:
        reasons.append({"code": "early", "day": item["MinTimeUnit"]})
    if state["day"] > item["MaxTimeUnit"]:
        reasons.append({"code": "expired", "day": item["MaxTimeUnit"]})
    for flag in item["requiredFlags"]:
        if flag not in state["flags"]:
            reasons.append({"code": "missing_flag", "key": flag})
    for flag in item["forbiddenFlags"]:
        if flag in state["flags"]:
            reasons.append({"code": "forbidden_flag", "key": flag})
    for c in item["conditions"]:
        values = state["actors"].get(c["actorId"], {}) if c["scope"] == "actors" else state[c["scope"]]
        current = values.get(c["key"])
        passed = current is not None and {
            "GreaterThan": lambda: current > c["value"],
            "LessThan": lambda: current < c["value"],
            "Equal": lambda: math.isclose(current, c["value"], rel_tol=1e-6, abs_tol=1e-6),
        }[c["comparison"]]()
        if not passed:
            reasons.append({"code": "condition", **c, "current": current})
    return reasons


def apply_outcome(state, effect):
    kind, key, value = effect["type"], effect["targetId"], effect["value"]
    if kind == "AddFlag":
        if key not in state["flags"]:
            state["flags"].append(key)
    elif kind == "RemoveFlag":
        state["flags"] = [f for f in state["flags"] if f != key]
    elif kind in {"SetNarrativeBoolTrue", "SetNarrativeBoolFalse"}:
        state["bools"][key] = kind == "SetNarrativeBoolTrue"
    elif kind == "SetRelationship":
        # Structured identity avoids ambiguous concatenated actor IDs.
        relations = state["relationships"].setdefault(effect["actorId"], {}).setdefault(effect["otherActorId"], {})
        relations[key] = value
    else:
        values = (state["actors"].setdefault(effect["actorId"], {}) if kind == "ModifyActorAttribute"
                  else state["resources" if kind == "ModifyResource" else "variables"])
        values[key] = number(values.get(key, 0) + value)


def execute(previous, command):
    """Copy-on-write: a failed command cannot leave half-applied consequences."""
    bounded_json(command)
    if not isinstance(command, dict):
        raise DomainError("Expected a narrative command")
    state = copy.deepcopy(previous or initial_state())
    action = command.get("action")
    record = {"day": state["day"], "action": action}
    if action in ("add_event", "update_event"):
        item = event(command.get("event"))
        exists = any(e["id"] == item["id"] for e in state["events"])
        if action == "update_event":
            if not exists or item["id"] in state["completed"]:
                raise DomainError("Cannot edit this narrative event", 409)
            state["events"] = [item if e["id"] == item["id"] else e for e in state["events"]]
        elif len(state["events"]) >= 250 or exists:
            raise DomainError("Narrative event limit or duplicate ID")
        else:
            state["events"].append(item)
        record["title"] = item["title"]
        record["event"] = item
    elif action == "set_value":
        scope, key = command.get("scope"), text(command.get("key"))
        if scope not in ("resources", "variables", "actors"):
            raise DomainError("Invalid narrative value scope")
        values = state["actors"].setdefault(text(command.get("actorId")), {}) if scope == "actors" else state[scope]
        value = number(command.get("value"))
        record.update(scope=scope, key=key, before=values.get(key), after=value)
        if scope == "actors":
            record["actorId"] = command["actorId"]
        values[key] = value
    elif action == "set_flags":
        flags = strings(command.get("flags"))
        record.update(before=state["flags"][:], after=flags)
        state["flags"] = flags
    elif action == "advance":
        state["day"] = day(state["day"] + 1)
        record["after"] = state["day"]
    elif action == "resolve":
        item = next((e for e in state["events"] if e["id"] == command.get("eventId")), None)
        if item is None:
            raise DomainError("Narrative event not found", 404)
        if blockers(state, item):
            raise DomainError("Narrative event is unavailable", 409)
        index = command.get("choice")
        if type(index) is not int or not 0 <= index < len(item["choices"]):
            raise DomainError("Invalid narrative choice")
        choice = item["choices"][index]
        for effect in choice["outcomes"]:
            apply_outcome(state, effect)
        state["completed"].append(item["id"])
        record.update(eventId=item["id"], title=item["title"], choice=choice["text"], outcomes=choice["outcomes"])
    else:
        raise DomainError("Unknown narrative command")
    if len(state["history"]) >= 2000:
        raise DomainError("Narrative history limit reached")
    state["history"].append(record)
    bounded_json(state)
    if len(json.dumps(state).encode()) > 1_000_000:
        raise DomainError("Narrative state exceeds 1 MB")
    return state


def view(state):
    result = copy.deepcopy(state or initial_state())
    result["events"] = [{**e, "blockers": blockers(result, e)} for e in result["events"]]
    return result


def actor_ids(state):
    state = state or initial_state()
    ids = set(state["actors"]) | set(state["relationships"])
    for relations in state["relationships"].values():
        ids.update(relations)
    for item in state["events"]:
        ids.update(item["InvolvedActors"])
        ids.update(c["actorId"] for c in item["conditions"] if c["scope"] == "actors")
        for choice in item["choices"]:
            for effect in choice["outcomes"]:
                ids.update(effect[k] for k in ("actorId", "otherActorId") if k in effect)
    return ids
