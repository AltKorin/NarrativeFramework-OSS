"""Character creation service: campaign ruleset packs, event-sourced drafts and GM approval.

A draft's sheet is always recomputed from its events; dice are rolled here, never sent by
clients. Approval writes through existing wiki/runtime code paths. Design:
web/docs/design/kreator-postaci/.
"""

import secrets
import uuid

from django.db import transaction

from . import narrative_engine as engine
from . import rulesets, wiki
from .campaign_access import campaign_access
from .domain_errors import DomainError
from .models import CampaignRuleset, CharacterDraft, WikiPage

ACTIVE = ("draft", "submitted", "returned")
PLAYER_ACTIONS = {"choose_role", "choose_template", "set_stats", "roll_stats", "set_skills",
                  "set_gear", "set_items", "roll_table", "choose_table", "set_name", "submit",
                  "set_axis", "choose", "assign_stats", "roll_stat_set"}
MAX_PICKS = 200
GM_ACTIONS = {"approve", "return", "grant_reroll"}
MAX_EVENTS = 500


def roll(count, sides, divisor=1, up=False):
    total = sum(secrets.randbelow(sides) + 1 for _ in range(count))
    return -(-total // divisor) if up else total // divisor


def roll_keep(count, sides, keep):
    dice = [secrets.randbelow(sides) + 1 for _ in range(count)]
    kept = sorted(dice, reverse=True)[:keep]
    return dice, kept, sum(kept)


def access(user, campaign_id):
    c = campaign_access(user, campaign_id, minimum="player")
    return c, c.wiki_role in ("owner", "gm")


def expected(data, current, label="Character"):
    if type(data.get("version")) is not int or data["version"] != current:
        raise DomainError(f"{label} version conflict; reload before saving", 409)


def public_pack(document):
    return {k: v for k, v in document.items() if k != "source"}


# ── Ruleset ─────────────────────────────────────────────────────────────────

@transaction.atomic
def read_ruleset(user, campaign_id):
    c, gm = access(user, campaign_id)
    item = CampaignRuleset.objects.filter(campaign=c).first()
    if not item:
        raise DomainError("Campaign has no ruleset", 404)
    return {"revision": item.revision, "document": item.document if gm else public_pack(item.document)}


@transaction.atomic
def set_ruleset(user, campaign_id, data):
    wiki.body_object(data)
    c, gm = access(user, campaign_id)
    if not gm:
        raise DomainError("Campaign not found", 404)
    rulesets.load(data.get("document"))  # rejects the whole pack on any error
    from .images import check_ruleset_images
    check_ruleset_images(c, data["document"])
    item = CampaignRuleset.objects.filter(campaign=c).first()
    expected(data, item.revision if item else 0, "Ruleset")
    if item:
        item.document, item.revision = data["document"], item.revision + 1
        item.save()
    else:
        item = CampaignRuleset.objects.create(campaign=c, document=data["document"])
    wiki.touch(c, user, "ruleset.saved", c.id)
    return {"revision": item.revision, "title": item.document["title"]}


@transaction.atomic
def delete_ruleset(user, campaign_id, data):
    wiki.body_object(data)
    c, gm = access(user, campaign_id)
    item = CampaignRuleset.objects.filter(campaign=c).first() if gm else None
    if not item:
        raise DomainError("Campaign has no ruleset", 404)
    expected(data, item.revision, "Ruleset")
    item.delete()  # drafts keep their own snapshot and can still be finished
    wiki.touch(c, user, "ruleset.deleted", c.id)
    return {"deleted": True, "drafts_in_progress": c.character_drafts.filter(status__in=ACTIVE).count()}


# ── Draft state ─────────────────────────────────────────────────────────────

def fold(events):
    state = {"role": None, "template": None, "stats": None, "stats_roll": None, "skills": {},
             "gear": [], "items": [], "tables": {}, "name": None, "look": "", "losses": {},
             "rerolls": set(), "level": None, "axes": {}, "choices": {}, "stat_set": None}
    for e in events:
        kind = e["type"]
        if kind == "role_chosen":
            state.update(role=e["role"], template=None, stats_roll=None, gear=[])
        elif kind == "template_chosen":
            state["template"] = e["index"]
        elif kind in ("stats_set", "stats_assigned"):
            state["stats"] = e["values"]
        elif kind == "stat_set_rolled":
            state.update(stat_set=e["values"], stats=None)
            state["rerolls"].discard("stats")
        elif kind == "level_set":
            state["level"] = e["level"]
        elif kind == "axis_set":
            state["axes"][e["axis"]] = e["picks"]
        elif kind == "choice_set":
            state["choices"][e["key"]] = {k: e[k] for k in ("picks", "values") if k in e}
        elif kind == "stats_rolled":
            state["stats_roll"] = e["row"]
            state["rerolls"].discard("stats")
        elif kind == "skills_set":
            state["skills"] = e["values"]
        elif kind == "gear_set":
            state["gear"] = e["items"]
        elif kind == "items_set":
            state["items"] = e["items"]
        elif kind in ("table_rolled", "table_chosen"):
            state["tables"][e["table"]] = e["results"]
            state["rerolls"].discard(e["table"])
        elif kind == "name_set":
            state.update(name=e["name"], look=e["look"])
        elif kind == "submitted":
            state["losses"] = e["losses"]
        elif kind == "reroll_granted":
            state["rerolls"].add(e["target"])
    return state


def lookup(pack, key):
    return {item["id"]: item for item in pack.get(key, [])}


def method_of(pack, draft):
    return lookup(pack, "methods")[draft.method]


def compute(pack, draft, state):
    """Sheet plus every rule problem; submission requires an empty problem list."""
    steps = {s["type"]: s for s in method_of(pack, draft)["steps"]}
    if "axes" in steps:
        return compute_axes(pack, state, steps)
    roles, skills_def = lookup(pack, "roles"), lookup(pack, "skills")
    role = roles.get(state["role"])
    template = role["templates"][state["template"]] if role and state["template"] is not None else None
    problems = []
    if ("role" in steps or "template" in steps) and not role:
        problems.append("Choose a role")
    if "template" in steps and not template:
        problems.append("Choose a template")

    stats = None
    if template:
        stats = template["stats"]
    elif "stats_role_roll" in steps:
        stats = role["stat_rows"][state["stats_roll"]] if role and state["stats_roll"] is not None else None
    elif state["stats"] is not None:
        stats = state["stats"]
    if stats is None:
        problems.append("Set statistics")
    else:
        problems += stat_problems(steps, stats, state)

    base = dict(template["skills"]) if template else {}
    if "skills_package" in steps and role:
        base.update(role["skill_package"])
    levels = dict(base)
    if "skills_points" in steps:
        step = steps["skills_points"]
        levels.update(state["skills"])
        spent = 0
        for key, level in levels.items():
            skill = skills_def[key]
            if level > min(skill["max"], step.get("max", skill["max"])):
                problems.append(f"{skill['label']}: too high")
            if level < base.get(key, 0):
                problems.append(f"{skill['label']}: below the role package")
            spent += max(0, level - base.get(key, 0)) * skill["cost"]
        if step.get("require_basic"):
            for key, skill in skills_def.items():
                if levels.get(key, 0) < skill["basic_min"]:
                    problems.append(f"{skill['label']}: at least {skill['basic_min']}")
        if spent != step["pool"]:
            problems.append(f"Skill points spent {spent} of {step['pool']}")

    items = list(template["items"]) if template else []
    if "gear_package" in steps and role:
        chosen = state["gear"]
        for group in role["gear_package"]:
            if len([x for x in chosen if x in group["items"]]) != group["choose"]:
                problems.append(f"Choose {group['choose']} from the role package")
        items += chosen
    shop = state["items"]
    items += shop
    if "shop" in steps:
        budgets = steps["shop"].get("budgets", [])
        catalog = lookup(pack, "catalog")
        price = sum(catalog[x]["price"] for x in shop if x in catalog)
        if price > sum(b["amount"] for b in budgets):
            problems.append("Purchases exceed the budget")
        allowed = [c for b in budgets for c in b.get("categories", [])]
        if allowed and any(catalog[x].get("category") not in allowed for x in shop if x in catalog):
            problems.append("Item outside the allowed categories")

    if "tables" in steps:
        required = list(steps["tables"].get("tables", []))
        if steps["tables"].get("include_role_lifepath") and role:
            required += role["lifepath"]
        problems += [f"Roll {lookup(pack, 'tables')[t]['label']}" for t in required if t not in state["tables"]]
    if not state["name"]:
        problems.append("Name the character")

    sheet = None
    if stats is not None:
        loss_mode = (steps.get("shop") or steps.get("gear_package") or {}).get("loss_mode", "fixed")
        losses = state["losses"] if loss_mode == "roll" and state["losses"] else None
        sheet = rulesets.sheet(pack, stats, levels, items, losses)
        problems += sheet["problems"]
    return {"sheet": sheet, "problems": problems, "items": items, "skills": levels, "stats": stats}


def stat_problems(steps, stats, state):
    if "stats_points" in steps:
        step = steps["stats_points"]
        costs = step.get("costs")
        spent = sum(costs[str(v)] for v in stats.values()) if costs else sum(stats.values())
        if spent != step["pool"]:
            return [f"Spend exactly {step['pool']} statistic points"]
    if "stats_array" in steps and sorted(stats.values()) != sorted(steps["stats_array"]["values"]):
        return ["Assign the listed statistic values"]
    if "stats_roll_assign" in steps and sorted(stats.values()) != sorted(state["stat_set"] or []):
        return ["Assign the rolled statistic values"]
    return []


def axes_of(state, steps):
    return {key: state["axes"].get(key, []) for key in steps["axes"]["axes"]}


def compute_axes(pack, state, steps):
    """Levelled characters: axis options and choices resolved by the ruleset engine."""
    stats = state["stats"]
    resolution = rulesets.resolve(pack, state["level"], axes_of(state, steps), state["choices"], stats)
    problems = []
    if stats is None:
        problems.append("Roll statistics" if "stats_roll_assign" in steps and state["stat_set"] is None else "Set statistics")
    else:
        problems += stat_problems(steps, stats, state)
    problems += resolution["problems"]
    if not state["name"]:
        problems.append("Name the character")
    ranks, items = resolution["grants"]["ranks"], resolution["grants"]["items"] + state["items"]
    sheet = None
    if stats is not None:
        sheet = rulesets.sheet(pack, stats, ranks, items, None, resolution)
        problems += sheet["problems"]
    return {"sheet": sheet, "problems": problems, "items": items, "skills": ranks, "stats": stats,
            "open": resolution["open"]}


def view(draft, gm):
    pack = rulesets.load(draft.ruleset)
    state = fold(draft.events)
    result = compute(pack, draft, state)
    return {"id": str(draft.id), "method": draft.method, "status": draft.status, "version": draft.version,
            "member": str(draft.member_id) if draft.member_id else None, "review_note": draft.review_note,
            "page": str(draft.page_id) if draft.page_id else None,
            "state": {**{k: v for k, v in state.items() if k != "rerolls"}, "rerolls": sorted(state["rerolls"])},
            "ruleset": draft.ruleset if gm else public_pack(draft.ruleset),
            **result, "events": draft.events}


# ── Drafts ──────────────────────────────────────────────────────────────────

def draft_for(c, gm, draft_id, lock=True):
    qs = CharacterDraft.objects.select_for_update() if lock else CharacterDraft.objects
    draft = qs.filter(pk=wiki.identifier(draft_id), campaign=c).first()
    if not draft or not (gm or draft.member_id == getattr(c.wiki_member, "pk", None)):
        raise DomainError("Character not found", 404)
    return draft


@transaction.atomic
def list_drafts(user, campaign_id):
    c, gm = access(user, campaign_id)
    qs = c.character_drafts.all() if gm else c.character_drafts.filter(member=c.wiki_member)
    return [{"id": str(d.id), "status": d.status, "method": d.method, "version": d.version,
             "member": str(d.member_id) if d.member_id else None,
             "name": fold(d.events)["name"], "page": str(d.page_id) if d.page_id else None,
             "updated_at": d.updated_at.isoformat()}
            for d in qs.order_by("-updated_at")[:200]]


@transaction.atomic
def read_draft(user, campaign_id, draft_id):
    c, gm = access(user, campaign_id)
    return view(draft_for(c, gm, draft_id, lock=False), gm)


@transaction.atomic
def create_draft(user, campaign_id, data):
    wiki.body_object(data)
    c, gm = access(user, campaign_id)
    draft_id = wiki.identifier(data.get("id"))
    existing = CharacterDraft.objects.filter(pk=draft_id).first()
    if existing:
        if existing.campaign_id != c.id or existing.created_by_id != user.pk or existing.method != data.get("method"):
            raise DomainError("Creation conflict", 409)
        return view(existing, gm), False
    ruleset = CampaignRuleset.objects.filter(campaign=c).first()
    if not ruleset:
        raise DomainError("Campaign has no ruleset", 409)
    if data.get("method") not in lookup(ruleset.document, "methods"):
        raise DomainError("Unknown creation method")
    member = None if gm else c.wiki_member
    if not gm and c.character_drafts.filter(member=member, status__in=ACTIVE).count() >= 3:
        raise DomainError("You already have 3 characters in progress", 409)
    events = []
    span = ruleset.document.get("level")
    if span:
        level = data.get("level", span["min"]) if gm else span["min"]
        if type(level) is not int or not span["min"] <= level <= span["max"]:
            raise DomainError(f"Level must be {span['min']}-{span['max']}")
        events.append({"type": "level_set", "level": level})
    draft = CharacterDraft.objects.create(id=draft_id, campaign=c, member=member, created_by=user,
                                          ruleset=ruleset.document, method=data["method"], events=events)
    return view(draft, gm), True


@transaction.atomic
def delete_draft(user, campaign_id, draft_id, data):
    wiki.body_object(data)
    c, gm = access(user, campaign_id)
    draft = draft_for(c, gm, draft_id)
    expected(data, draft.version)
    if draft.status == "approved":
        raise DomainError("Approved characters live in the wiki; delete the page there", 409)
    draft.delete()
    return {"deleted": True}


@transaction.atomic
def command(user, campaign_id, draft_id, data):
    wiki.body_object(data)
    c, gm = access(user, campaign_id)
    draft = draft_for(c, gm, draft_id)
    expected(data, draft.version)
    action = data.get("action")
    own = draft.member_id is None or draft.member_id == getattr(c.wiki_member, "pk", None)
    if action in PLAYER_ACTIONS:
        if not own or draft.status not in ("draft", "returned"):
            raise DomainError("This character cannot be edited now", 409)
    elif action in GM_ACTIONS:
        if not gm:
            raise DomainError("Only the GM can do this", 403)
    else:
        raise DomainError("Unknown character action")
    pack = rulesets.load(draft.ruleset)
    state = fold(draft.events)
    events = handle(pack, draft, state, action, data, user)
    if len(draft.events) + len(events) > MAX_EVENTS:
        raise DomainError("Character history limit reached", 409)
    draft.events = draft.events + events
    if action == "submit":
        problems = compute(pack, draft, fold(draft.events))["problems"]
        if problems:
            raise DomainError("Character is not complete: " + "; ".join(problems[:5]), 409)
        draft.status = "submitted"
        if draft.member_id is None:  # GM characters (NPCs, pregens) are approved at once
            approve(c, user, pack, draft)
    elif action == "approve":
        approve(c, user, pack, draft)
    elif action == "return":
        draft.status, draft.review_note = "returned", events[0]["note"]
    draft.version += 1
    draft.save()
    return view(draft, gm)


def handle(pack, draft, state, action, data, user):
    """Validate one action and return the events it appends (pure apart from dice)."""
    steps = {s["type"]: s for s in method_of(pack, draft)["steps"]}
    roles, catalog, tables = lookup(pack, "roles"), lookup(pack, "catalog"), lookup(pack, "tables")
    role = roles.get(state["role"])

    def step(kind):
        if kind not in steps:
            raise DomainError("This method has no such step")
        return steps[kind]

    if action == "choose_role":
        if not ({"role", "template"} & set(steps)) or data.get("role") not in roles:
            raise DomainError("Choose a role from the ruleset")
        return [{"type": "role_chosen", "role": data["role"]}]
    if action == "choose_template":
        step("template")
        index = data.get("index")
        if not role or type(index) is not int or not 0 <= index < len(role["templates"]):
            raise DomainError("Choose a template of your role")
        return [{"type": "template_chosen", "index": index}]
    if action == "set_stats":
        point_step = step("stats_points")
        values = rulesets.stat_values(pack, data.get("values"), "statistics")
        for key, value in values.items():
            stat = lookup(pack, "stats")[key]
            if not point_step.get("min", stat["min"]) <= value <= point_step.get("max", stat["max"]):
                raise DomainError(f"{stat['label']} out of range")
        return [{"type": "stats_set", "values": values}]
    if action == "roll_stats":
        step("stats_role_roll")
        if not role or not role["stat_rows"]:
            raise DomainError("Choose a role with statistic rows first")
        if state["stats_roll"] is not None and "stats" not in state["rerolls"]:
            raise DomainError("Statistics already rolled; ask the GM for a reroll", 409)
        result = roll(1, len(role["stat_rows"]))
        return [{"type": "stats_rolled", "dice": f"1d{len(role['stat_rows'])}", "result": result, "row": result - 1}]
    if action == "set_skills":
        points = step("skills_points")
        values = data.get("values")
        skills = lookup(pack, "skills")
        if not isinstance(values, dict) or len(values) > len(skills) or any(k not in skills for k in values):
            raise DomainError("Choose skills from the ruleset")
        for key, level in values.items():
            if type(level) is not int or not 0 <= level <= points.get("max", skills[key]["max"]):
                raise DomainError(f"{skills[key]['label']}: invalid level")
        return [{"type": "skills_set", "values": values}]
    if action == "set_gear":
        step("gear_package")
        items = data.get("items")
        allowed = {x for g in (role or {}).get("gear_package", []) for x in g["items"]}
        if not isinstance(items, list) or any(x not in allowed for x in items) or len(items) > len(allowed):
            raise DomainError("Choose items from your role package")
        return [{"type": "gear_set", "items": items}]
    if action == "set_items":
        step("shop")
        items = data.get("items")
        if not isinstance(items, list) or len(items) > 200 or any(x not in catalog for x in items):
            raise DomainError("Choose items from the catalog")
        return [{"type": "items_set", "items": items}]
    if action in ("roll_table", "choose_table"):
        tables_step = step("tables")
        required = list(tables_step.get("tables", [])) + (role["lifepath"] if role and tables_step.get("include_role_lifepath") else [])
        key = data.get("table")
        if key not in required:
            raise DomainError("This table is not part of your character")
        if key in state["tables"] and key not in state["rerolls"]:
            raise DomainError("Already rolled; ask the GM for a reroll", 409)
        if action == "choose_table":
            entry = data.get("entry")
            if not tables[key].get("choose") or type(entry) is not int or not 0 <= entry < len(tables[key]["entries"]):
                raise DomainError("This table must be rolled")
            first = [{"table": key, "entry": entry, "text": tables[key]["entries"][entry]["text"]}]
            return [{"type": "table_chosen", "table": key, "results": first + chain(tables, tables[key]["entries"][entry])}]
        return [{"type": "table_rolled", "table": key, "results": roll_chain(tables, key)}]
    if action == "set_axis":
        return [axis_event(pack, draft, state, steps, data)]
    if action == "choose":
        return [choice_event(pack, state, steps, data)]
    if action == "assign_stats":
        if "stats_array" in steps:
            allowed = steps["stats_array"]["values"]
        elif "stats_roll_assign" in steps and state["stat_set"] is not None:
            allowed = state["stat_set"]
        else:
            raise DomainError("This method has no statistics to assign")
        values = rulesets.stat_values(pack, data.get("values"), "statistics")
        if sorted(values.values()) != sorted(allowed):
            raise DomainError("Assign exactly the listed values")
        return [{"type": "stats_assigned", "values": values}]
    if action == "roll_stat_set":
        dice = step("stats_roll_assign")["dice"]
        if state["stat_set"] is not None and "stats" not in state["rerolls"]:
            raise DomainError("Statistics already rolled; ask the GM for a reroll", 409)
        rolls = [dict(zip(("dice", "kept", "result"), roll_keep(*rulesets.dice_keep(dice, "stats"))))
                 for _ in pack["stats"]]
        return [{"type": "stat_set_rolled", "dice": dice, "rolls": rolls, "values": [r["result"] for r in rolls]}]
    if action == "set_name":
        name = engine.text(data.get("name"), 120)
        look = data.get("look", "")
        if not isinstance(look, str) or len(look) > 2000:
            raise DomainError("Description is too long")
        return [{"type": "name_set", "name": name, "look": look}]
    if action == "submit":
        loss_mode = (steps.get("shop") or steps.get("gear_package") or {}).get("loss_mode", "fixed")
        if loss_mode != "roll":
            return [{"type": "submitted", "losses": {}, "rolls": []}]
        return [losses_event(pack, draft, state)]
    if action == "approve":
        if draft.status != "submitted":
            raise DomainError("Only submitted characters can be approved", 409)
        return []  # approve() appends the event with the page ID
    if action == "return":
        if draft.status != "submitted":
            raise DomainError("Only submitted characters can be returned", 409)
        note = data.get("note")
        if not isinstance(note, str) or not note.strip() or len(note) > 2000:
            raise DomainError("Explain what to change")
        return [{"type": "returned", "note": note.strip()}]
    if action == "grant_reroll":
        target = data.get("target")
        if target != "stats" and target not in tables:
            raise DomainError("Choose statistics or a table")
        return [{"type": "reroll_granted", "target": target}]


def axis_event(pack, draft, state, steps, data):
    if "axes" not in steps or data.get("axis") not in steps["axes"]["axes"]:
        raise DomainError("Choose an axis of this method")
    axis = lookup(pack, "axes")[data["axis"]]
    options = {o["id"] for o in axis["options"]}
    picks = data.get("picks")
    if not isinstance(picks, list) or not picks or len(picks) > len(options):
        raise DomainError(f"Choose options of {axis['label']}")
    result = []
    for pick in picks:
        if not isinstance(pick, dict) or not isinstance(pick.get("option"), str) or pick["option"] not in options:
            raise DomainError(f"Choose options of {axis['label']}")
        if axis["repeatable"]:
            levels = pick.get("levels")
            if type(levels) is not int or not 1 <= levels <= pack["level"]["max"]:
                raise DomainError("Levels must be a whole number within the level range")
            result.append({"option": pick["option"], "levels": levels})
        else:
            result.append({"option": pick["option"]})
    if not axis["repeatable"] and len(result) != 1:
        raise DomainError(f"Choose one {axis['label']}")
    if len({r["option"] for r in result}) != len(result):
        raise DomainError("Each option can be chosen once")
    return {"type": "axis_set", "axis": axis["id"], "picks": result}


def choice_event(pack, state, steps, data):
    key = data.get("key")
    if "axes" not in steps or not isinstance(key, str):
        raise DomainError("Choose an open choice")
    opened = rulesets.resolve(pack, state["level"], axes_of(state, steps), state["choices"], state["stats"])["open"]
    if key not in opened:
        raise DomainError("This choice is not open for this character")
    if opened[key]["kind"] == "stats_bonus":
        values = data.get("values")
        if (not isinstance(values, dict) or not set(values) <= {x["id"] for x in pack["stats"]}
                or any(type(v) is not int or not 1 <= v <= 10 for v in values.values())):
            raise DomainError("Give whole-number bonuses per statistic")
        return {"type": "choice_set", "key": key, "values": values}
    picks = data.get("picks")
    if not isinstance(picks, list) or len(picks) > MAX_PICKS or any(not isinstance(x, str) or len(x) > 60 for x in picks):
        raise DomainError("Choose from the listed options")
    return {"type": "choice_set", "key": key, "picks": picks}


def chain(tables, entry, depth=0):
    """Follow `next` links of a chosen/rolled entry; depth and count bounded by the pack loader."""
    results = []
    if entry.get("next") and depth < 5:
        repeat = entry.get("repeat", 1)
        times = roll(*rulesets.dice(repeat, "repeat")) if isinstance(repeat, str) else min(int(repeat), 10)
        for _ in range(times):
            results += roll_chain(tables, entry["next"], depth + 1)
    return results[:30]


def roll_chain(tables, key, depth=0):
    table = tables[key]
    count, sides, _ = rulesets.dice(table["dice"], key)
    result = roll(count, sides)
    entry = next(e for e in table["entries"] if e["range"][0] <= result <= e["range"][1])
    return [{"table": key, "dice": table["dice"], "result": result, "text": entry["text"]}] + chain(tables, entry, depth)


def losses_event(pack, draft, state):
    """Roll pool costs once per item copy; a resubmission reuses earlier rolls for the same copies."""
    items = compute(pack, draft, state)["items"]
    catalog = lookup(pack, "catalog")
    previous = {r["copy"]: r for e in draft.events if e["type"] == "submitted" for r in e["rolls"]}
    rolls, seen = [], {}
    for key in items:
        seen[key] = seen.get(key, 0) + 1
        for pool, cost in catalog.get(key, {}).get("costs", {}).items():
            copy = f"{key}#{seen[key]}:{pool}"
            rolls.append(previous.get(copy) or {"copy": copy, "pool": pool, "dice": cost["dice"],
                                               "result": roll(*rulesets.dice(cost["dice"], key), up=cost.get("round") == "up")})
    losses = {}
    for r in rolls:
        losses[r["pool"]] = losses.get(r["pool"], 0) + r["result"]
    return {"type": "submitted", "losses": losses, "rolls": rolls}


# ── Approval ────────────────────────────────────────────────────────────────

def approve(c, user, pack, draft):
    state = fold(draft.events)
    result = compute(pack, draft, state)
    if result["problems"]:
        raise DomainError("Character is not complete", 409)
    sheet = result["sheet"]
    page_id = uuid.uuid5(draft.id, "page")
    page, _ = wiki.create_page(user, c.id, {"id": str(page_id), "kind": "pc", "title": state["name"],
                                            "body": markdown(pack, state, result), "gm_notes": roll_log(draft)})
    values = {**sheet["stats"], **sheet["derived"]}
    if sheet.get("level") is not None:
        values.update(LEVEL=sheet["level"], **sheet["variables"])
    for key, pool in sheet["pools"].items():
        values[key], values[key + "_MAX"] = pool["current"], pool["max"]
    c.refresh_from_db()
    runtime = c.runtime
    for key, value in values.items():
        runtime = engine.execute(runtime, {"action": "set_value", "scope": "actors",
                                           "actorId": page["id"], "key": key, "value": value})
    c.runtime = runtime
    c.save(update_fields=["runtime"])
    wiki.touch(c, user, "character.approved", page["id"])
    if draft.member_id:  # the player can find their own sheet in World material right away
        from . import wiki_sharing
        wiki_sharing.publish(user, c.id, page["id"], {"version": WikiPage.objects.get(pk=page["id"]).version,
                                                       "audience": "selected", "recipients": [str(draft.member_id)]})
    draft.page = WikiPage.objects.get(pk=page["id"])
    draft.status = "approved"
    draft.events = draft.events + [{"type": "approved", "page": page["id"]}]


def markdown(pack, state, result):
    sheet, labels = result["sheet"], {**{s["id"]: s["label"] for s in pack["stats"]},
                                      **{d["id"]: d["label"] for d in pack["derived"]}}
    role = lookup(pack, "roles").get(state["role"])
    catalog, skills = lookup(pack, "catalog"), lookup(pack, "skills")
    pl = pack["language"] == "pl"
    titles = {"skills": "Umiejętności" if pl else "Skills", "items": "Sprzęt i wszczepy" if pl else "Gear and cyberware",
              "history": "Historia" if pl else "Background", "features": "Zdolności" if pl else "Features",
              **pack.get("sheet_labels", {})}
    lines = []  # the page title already carries the character's name
    if sheet.get("level") is not None:
        lines.append(" · ".join([f"**{pack['level']['label']} {sheet['level']}**"] + axis_lines(pack, state)))
    if role:
        ability = role.get("ability")
        lines.append(f"**{role['label']}**" + (f" · {ability['label']} {ability['rank']}" if ability else ""))
    if state["look"]:
        lines += ["", state["look"]]
    lines += ["", "| " + " | ".join(sheet["stats"]) + " |", "|" + "---|" * len(sheet["stats"]),
              "| " + " | ".join(str(v) for v in sheet["stats"].values()) + " |", ""]
    lines += [f"- **{labels[k]}:** {v}" for k, v in sheet["derived"].items()]
    lines += [f"- **{p['label']}:** {p['current']} / {p['max']}" for p in sheet["pools"].values()]
    if sheet.get("skill_values"):
        lines += ["", f"### {titles['skills']}"]
        lines += [f"- {skills[k]['label']} {v:+d}" for k, v in sorted(sheet["skill_values"].items(), key=lambda x: skills[x[0]]["label"])]
    elif result["skills"]:
        lines += ["", f"### {titles['skills']}"]
        lines += [f"- {skills[k]['label']} {v}" for k, v in sorted(result["skills"].items(), key=lambda x: skills[x[0]]["label"]) if v]
    if sheet.get("features"):
        lines += ["", f"### {titles['features']}"]
        lines += [f"- **{f['label']}** ({f['source']})" + (f": {f['summary']}" if f.get("summary") else "") for f in sheet["features"]]
    for key, labels in (sheet.get("choices") or {}).items():
        lines += ["", f"### {result['open'][key]['label']}"] + [f"- {label}" for label in labels]
    if result["items"]:
        lines += ["", f"### {titles['items']}"]
        lines += [f"- {catalog[x]['label']}" for x in result["items"]]
    history = [r["text"] for results in state["tables"].values() for r in results]
    if history:
        lines += ["", f"### {titles['history']}"] + [f"- {t}" for t in history]
    return "\n".join(lines)[:12000]


def axis_lines(pack, state):
    lines = []
    for axis in pack.get("axes", []):
        options = {o["id"]: o["label"] for o in axis["options"]}
        picks = state["axes"].get(axis["id"], [])
        if picks:
            names = [options[p["option"]] + (f" {p['levels']}" if "levels" in p else "") for p in picks]
            lines.append(f"{axis['label']}: {' / '.join(names)}")
    return lines


def roll_log(draft):
    lines = []
    for e in draft.events:
        if e["type"] == "stats_rolled":
            lines.append(f"STATS {e['dice']} = {e['result']}")
        elif e["type"] == "stat_set_rolled":
            lines += [f"STATS {e['dice']} {r['dice']} = {r['result']}" for r in e["rolls"]]
        elif e["type"] == "table_rolled":
            lines += [f"{r['table']} {r['dice']} = {r['result']}" for r in e["results"] if "dice" in r]
        elif e["type"] == "submitted":
            lines += [f"{r['copy']} {r['dice']} = {r['result']}" for r in e["rolls"]]
        elif e["type"] == "reroll_granted":
            lines.append(f"Reroll granted: {e['target']}")
    return "\n".join(lines)[:12000]
