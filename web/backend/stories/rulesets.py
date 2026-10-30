"""Engine-agnostic character rules: validates narrative-ruleset/1 packs and computes sheets.

The engine knows stats, skills, formulas, pools, catalog items, tables, roles and methods;
any concrete RPG system is data. Formulas are parsed with a node whitelist, never eval'd.
Design: web/docs/design/kreator-postaci/.
"""

import ast
import copy
import json
import math
import re

from .domain_errors import DomainError
from .transport import bounded_json

SCHEMA = "narrative-ruleset/1"
LIMITS = {"stats": 30, "skills": 200, "derived": 30, "pools": 10, "catalog": 1000,
          "tables": 100, "roles": 30, "methods": 10}
STEPS = {"role", "template", "stats_points", "stats_role_roll", "skills_points",
         "skills_package", "tables", "gear_package", "shop", "name"}
FUNCTIONS = {"ceil": math.ceil, "floor": math.floor, "round": round, "min": min, "max": max}
OPERATORS = {ast.Add: lambda a, b: a + b, ast.Sub: lambda a, b: a - b,
             ast.Mult: lambda a, b: a * b, ast.Div: lambda a, b: a / b if b else 0}
SYMBOL = re.compile(r"[A-Za-z][A-Za-z0-9_]{0,29}")
DICE = re.compile(r"([1-9]\d?)d([1-9]\d{0,2})(?:/([1-9]))?")  # 2d6, 1d6/2 (halved, rounded down)


def fail(message):
    raise DomainError(f"Ruleset: {message}")


def text(value, maximum, label, required=True):
    if value is None and not required:
        return ""
    if not isinstance(value, str) or (required and not value.strip()) or len(value) > maximum:
        fail(f"invalid {label}")
    return value.strip()


def integer(value, label, low=-10**6, high=10**6):
    if type(value) is not int or not low <= value <= high:
        fail(f"invalid {label}")
    return value


def dice(value, label):
    match = DICE.fullmatch(value) if isinstance(value, str) else None
    if not match:
        fail(f"invalid dice in {label}")
    return int(match[1]), int(match[2]), int(match[3] or 1)


def entries(document, key, label_limit=120):
    items = document.get(key, [])
    if not isinstance(items, list) or len(items) > LIMITS[key]:
        fail(f"invalid {key} list")
    seen = {}
    for item in items:
        if not isinstance(item, dict):
            fail(f"invalid {key} entry")
        key_id = text(item.get("id"), 60, f"{key} id")
        if key_id in seen:
            fail(f"duplicate {key} id {key_id}")
        text(item.get("label"), label_limit, f"{key} label")
        seen[key_id] = item
    return seen


# ── Formulas ────────────────────────────────────────────────────────────────

def parse(source, label):
    if not isinstance(source, str) or not source.strip() or len(source) > 200:
        fail(f"invalid formula for {label}")
    try:
        tree = ast.parse(source, mode="eval").body
    except SyntaxError:
        fail(f"invalid formula for {label}")
    names = set()

    def check(node):
        if isinstance(node, ast.Constant) and type(node.value) in (int, float):
            return
        if isinstance(node, ast.Name):
            names.add(node.id)
            return
        if isinstance(node, ast.BinOp) and type(node.op) in OPERATORS:
            check(node.left)
            check(node.right)
            return
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.USub, ast.UAdd)):
            check(node.operand)
            return
        if (isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
                and node.func.id in FUNCTIONS and not node.keywords and 1 <= len(node.args) <= 10):
            for arg in node.args:
                check(arg)
            return
        fail(f"unsupported expression in formula for {label}")

    check(tree)
    return tree, names


def evaluate(tree, values):
    if isinstance(tree, ast.Constant):
        return tree.value
    if isinstance(tree, ast.Name):
        return values[tree.id]
    if isinstance(tree, ast.BinOp):
        return OPERATORS[type(tree.op)](evaluate(tree.left, values), evaluate(tree.right, values))
    if isinstance(tree, ast.UnaryOp):
        value = evaluate(tree.operand, values)
        return -value if isinstance(tree.op, ast.USub) else value
    return FUNCTIONS[tree.func.id](*(evaluate(arg, values) for arg in tree.args))


def order(formulas, known):
    """Topological order of {id: names}; formulas may use known symbols and each other."""
    result, state = [], {}

    def visit(key, path):
        if state.get(key) == "done":
            return
        if state.get(key) == "active":
            fail("formula cycle: " + " -> ".join(path + [key]))
        state[key] = "active"
        for name in formulas[key]:
            if name in formulas:
                visit(name, path + [key])
            elif name not in known:
                fail(f"unknown symbol {name} in formula for {key}")
        state[key] = "done"
        result.append(key)

    for key in formulas:
        visit(key, [])
    return result


# ── Pack loading ────────────────────────────────────────────────────────────

def load(document):
    """Validate a whole pack and return a normalized private copy (never the input)."""
    bounded_json(document)
    if not isinstance(document, dict) or document.get("schema") != SCHEMA:
        fail("unsupported schema; expected narrative-ruleset/1")
    if len(json.dumps(document, ensure_ascii=False).encode()) > 450_000:
        fail("pack exceeds 450 KB")
    pack = copy.deepcopy(document)
    text(pack.get("id"), 80, "pack id")
    integer(pack.get("version"), "pack version", 1)
    text(pack.get("title"), 200, "pack title")
    if pack.get("language") not in ("pl", "en"):
        fail("language must be pl or en")
    text(pack.get("source"), 1000, "source", required=False)

    stats = entries(pack, "stats", 80)
    for key, stat in stats.items():
        if not SYMBOL.fullmatch(key):
            fail(f"stat id {key} must be a formula symbol")
        if integer(stat.get("min"), f"{key} min") > integer(stat.get("max"), f"{key} max"):
            fail(f"stat {key} min exceeds max")
    skills = entries(pack, "skills", 80)
    for key, skill in skills.items():
        if skill.get("stat") not in stats:
            fail(f"skill {key} references unknown stat")
        integer(skill.setdefault("cost", 1), f"{key} cost", 1, 10)
        integer(skill.setdefault("basic_min", 0), f"{key} basic_min", 0, 20)
        integer(skill.setdefault("max", 10), f"{key} max", 0, 20)
    pools = entries(pack, "pools", 80)
    derived = entries(pack, "derived", 80)
    formulas, trees = {}, {}
    for key, pool in pools.items():
        if not SYMBOL.fullmatch(key) or key in stats:
            fail(f"invalid pool id {key}")
        integer(pool.setdefault("floor", 0), f"{key} floor")
        if pool.get("warning") is not None:
            integer(pool["warning"], f"{key} warning")
        trees[key + "_MAX"], formulas[key + "_MAX"] = parse(pool.get("max"), key)
        formulas[key] = {key + "_MAX"}
    for key, item in derived.items():
        if not SYMBOL.fullmatch(key) or key in stats or key in formulas:
            fail(f"invalid derived id {key}")
        trees[key], formulas[key] = parse(item.get("formula"), key)
    pack["_order"] = order(formulas, set(stats))

    catalog = entries(pack, "catalog")
    targets = set(stats) | set(skills) | set(derived)
    for key, item in catalog.items():
        integer(item.setdefault("price", 0), f"{key} price", 0, 10**9)
        integer(item.setdefault("slots", 0), f"{key} slots", 0, 50)
        for pool, cost in item.setdefault("costs", {}).items():
            if pool not in pools or not isinstance(cost, dict):
                fail(f"item {key} costs unknown pool")
            dice(cost.get("dice"), key)
            integer(cost.get("fixed"), f"{key} fixed cost", 0, 1000)
        integer(item.setdefault("max", 1 if item["costs"] else 99), f"{key} max", 1, 99)
        for group in item.setdefault("requires", []):
            if not isinstance(group, list) or not group or any(x not in catalog for x in group):
                fail(f"item {key} requires unknown items")
        if any(x not in catalog for x in item.setdefault("uses_slot_of", [])):
            fail(f"item {key} uses slots of an unknown item")
        integer(item.setdefault("slot_cost", 1), f"{key} slot_cost", 1, 50)
        if item.setdefault("paired", False) not in (True, False) or (item["paired"] and not item["uses_slot_of"]):
            fail(f"item {key} paired needs uses_slot_of")
        for modifier in item.setdefault("modifiers", []):
            if not isinstance(modifier, dict) or modifier.get("target") not in targets:
                fail(f"item {key} modifies an unknown value")
            integer(modifier.get("value"), f"{key} modifier", -100, 100)
    for key, item in catalog.items():
        if any(not catalog[x]["slots"] for x in item["uses_slot_of"]):
            fail(f"item {key} uses slots of an item without slots")

    tables = entries(pack, "tables")
    for key, table in tables.items():
        count, sides, divisor = dice(table.get("dice"), key)
        if divisor != 1:
            fail(f"table {key} dice cannot be divided")
        if not isinstance(table.get("entries"), list) or len(table["entries"]) > 100:
            fail(f"table {key} needs an entries list")
        covered = []
        for entry in table["entries"]:
            span = entry.get("range") if isinstance(entry, dict) else None
            if not isinstance(span, list) or len(span) != 2:
                fail(f"table {key} entry needs a range")
            low, high = integer(span[0], f"{key} range"), integer(span[1], f"{key} range")
            text(entry.get("text"), 500, f"{key} entry text")
            if entry.get("next") is not None and entry["next"] not in tables:
                fail(f"table {key} links to unknown table")
            repeat = entry.setdefault("repeat", 1)
            if isinstance(repeat, str):
                dice(repeat, f"{key} repeat")
            else:
                integer(repeat, f"{key} repeat", 1, 10)
            covered.extend(range(low, high + 1))
        if sorted(covered) != list(range(count, count * sides + 1)):
            fail(f"table {key} ranges must cover {count}-{count * sides} exactly once")
    order({k: {e["next"] for e in t["entries"] if e.get("next")} for k, t in tables.items()}, set())

    roles = entries(pack, "roles")
    for key, role in roles.items():
        for row in role.setdefault("stat_rows", []):
            stat_values(pack, row, f"role {key} stat row")
        for skill, level in role.setdefault("skill_package", {}).items():
            if skill not in skills:
                fail(f"role {key} package uses unknown skill")
            integer(level, f"{key} package level", 0, 20)
        for table in role.setdefault("lifepath", []):
            if table not in tables:
                fail(f"role {key} uses unknown table")
        for group in role.setdefault("gear_package", []):
            if (not isinstance(group, dict) or not isinstance(group.get("items"), list)
                    or any(x not in catalog for x in group["items"])):
                fail(f"role {key} gear uses unknown items")
            integer(group.setdefault("choose", len(group["items"])), f"{key} choose", 1, len(group["items"]))
        for template in role.setdefault("templates", []):
            stat_values(pack, template.get("stats", {}), f"role {key} template")
            if any(s not in skills for s in template.setdefault("skills", {})):
                fail(f"role {key} template uses unknown skill")
            if any(x not in catalog for x in template.setdefault("items", [])):
                fail(f"role {key} template uses unknown items")

    methods = entries(pack, "methods")
    if not methods:
        fail("at least one method is required")
    for key, method in methods.items():
        steps = method.get("steps")
        if not isinstance(steps, list) or not steps or len(steps) > 20:
            fail(f"method {key} needs 1-20 steps")
        for step in steps:
            if not isinstance(step, dict) or step.get("type") not in STEPS:
                fail(f"method {key} has an unknown step")
            for table in step.get("tables", []) if step["type"] == "tables" else []:
                if table not in tables:
                    fail(f"method {key} uses unknown table")
            for budget in step.get("budgets", []) if step["type"] == "shop" else []:
                integer(budget.get("amount"), f"{key} budget", 0, 10**9)
            if step["type"] in ("gear_package", "shop") and step.setdefault("loss_mode", "fixed") not in ("fixed", "roll"):
                fail(f"method {key} loss_mode must be fixed or roll")
    pack["_trees"] = trees
    return pack


def stat_values(pack, values, label):
    stats = {s["id"]: s for s in pack["stats"]}
    if not isinstance(values, dict) or set(values) != set(stats):
        fail(f"{label} must set every stat")
    for key, value in values.items():
        integer(value, f"{label} {key}", stats[key]["min"], stats[key]["max"])
    return values


# ── Sheet computation ───────────────────────────────────────────────────────

def item_problems(pack, items):
    """Requirements, slots and counts for a list of catalog IDs (duplicates = several copies)."""
    catalog = {i["id"]: i for i in pack["catalog"]}
    problems = []
    if any(x not in catalog for x in items):
        return ["unknown item"]
    counts = {x: items.count(x) for x in items}
    for key, count in counts.items():
        if count > catalog[key]["max"]:
            problems.append(f"{catalog[key]['label']}: max {catalog[key]['max']}")
        for group in catalog[key]["requires"]:
            if not any(x in counts for x in group):
                names = ", ".join(catalog[x]["label"] for x in group)
                problems.append(f"{catalog[key]['label']}: requires {names}")
    # ponytail: slots pooled per host type, not per physical copy; an option could straddle two
    # eyes. Track host instances if a pack ever needs it.
    free = {key: catalog[key]["slots"] * count for key, count in counts.items() if catalog[key]["slots"]}
    for key, count in counts.items():
        item = catalog[key]
        if item["paired"] and (count % 2 or not any(counts.get(h, 0) >= 2 for h in item["uses_slot_of"])):
            problems.append(f"{item['label']}: install as a pair in two hosts")
    for key in items:
        hosts, cost = catalog[key]["uses_slot_of"], catalog[key]["slot_cost"]
        if hosts:
            host = next((h for h in hosts if free.get(h, 0) >= cost), None)
            if host is None:
                problems.append(f"{catalog[key]['label']}: no free slot")
            else:
                free[host] -= cost
    return problems


def dependents(pack, items, removed):
    """Items that would lose a requirement or slot host if `removed` is taken off."""
    remaining = list(items)
    remaining.remove(removed)
    catalog = {i["id"]: i for i in pack["catalog"]}
    lost = []
    for key in dict.fromkeys(remaining):
        item = catalog[key]
        if any(not any(x in remaining for x in g) for g in item["requires"]) or (
                item["uses_slot_of"] and not any(h in remaining for h in item["uses_slot_of"])):
            lost.append(key)
    return lost


def sheet(pack, stats, skills=None, items=(), losses=None):
    """Compute a character sheet. `losses` maps pool -> total spent; defaults to fixed costs."""
    stat_values(pack, stats, "character")
    catalog = {i["id"]: i for i in pack["catalog"]}
    items = list(items)
    problems = item_problems(pack, items)
    if losses is None:
        losses = {}
        for key in items:
            for pool, cost in catalog.get(key, {}).get("costs", {}).items():
                losses[pool] = losses.get(pool, 0) + cost["fixed"]
    values = dict(stats)
    for key in pack["_order"]:
        if key in pack["_trees"]:
            values[key] = math.floor(evaluate(pack["_trees"][key], values))
        else:  # current pool value = its maximum minus what items spent
            values[key] = values[key + "_MAX"] - losses.get(key, 0)
    pools, warnings = {}, []
    for pool in pack["pools"]:
        current = values[pool["id"]]
        pools[pool["id"]] = {"label": pool["label"], "max": values[pool["id"] + "_MAX"], "current": current}
        if current < pool["floor"]:
            problems.append(f"{pool['label']}: below {pool['floor']}")
        elif pool.get("warning") is not None and current < pool["warning"]:
            warnings.append(f"{pool['label']}: below {pool['warning']}")
    modifiers = {}
    for key in dict.fromkeys(items):
        for modifier in catalog.get(key, {}).get("modifiers", []):
            modifiers[modifier["target"]] = modifiers.get(modifier["target"], 0) + modifier["value"]
    return {"stats": stats, "derived": {d["id"]: values[d["id"]] for d in pack["derived"]},
            "pools": pools, "skills": dict(skills or {}), "items": items,
            "price": sum(catalog[x]["price"] for x in items if x in catalog),
            "modifiers": modifiers, "problems": problems, "warnings": warnings}
