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
          "tables": 100, "roles": 30, "methods": 10, "axes": 10, "lists": 50, "variables": 30,
          "progressions": 100, "options": 3000}
STEPS = {"role", "template", "stats_points", "stats_role_roll", "skills_points",
         "skills_package", "tables", "gear_package", "shop", "name",
         "axes", "choices", "stats_array", "stats_roll_assign"}
RESERVED = {"LEVEL", "RANK", "STAT"}  # plus the LV_ prefix: per-option levels of repeatable axes
GRANT_KEYS = {"level", "when", "values", "stats", "skills", "items", "features", "choice"}
CHOICE_KINDS = ("options", "list", "skills", "stats_bonus")
MAX_DEPTH = 4  # each nesting adds 5 JSON levels; transport caps documents at 32
FUNCTIONS = {"ceil": math.ceil, "floor": math.floor, "round": round, "min": min, "max": max}
OPERATORS = {ast.Add: lambda a, b: a + b, ast.Sub: lambda a, b: a - b,
             ast.Mult: lambda a, b: a * b, ast.Div: lambda a, b: a / b if b else 0}
COMPARISONS = {ast.Gt: lambda a, b: a > b, ast.GtE: lambda a, b: a >= b, ast.Lt: lambda a, b: a < b,
               ast.LtE: lambda a, b: a <= b, ast.Eq: lambda a, b: a == b, ast.NotEq: lambda a, b: a != b}
SYMBOL = re.compile(r"[A-Za-z][A-Za-z0-9_]{0,29}")
DICE = re.compile(r"([1-9]\d?)d([1-9]\d{0,2})(?:/([1-9]))?")  # 2d6, 1d6/2 (halved, rounded down)
DICE_KEEP = re.compile(r"([1-9]\d?)d([1-9]\d{0,2})(?:kh([1-9]\d?))?")  # 4d6kh3 (keep the highest three)


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


def dice_keep(value, label):
    match = DICE_KEEP.fullmatch(value) if isinstance(value, str) else None
    count = int(match[1]) if match else 0
    keep = int(match[3] or count) if match else 0
    if not match or not 1 <= keep <= count:
        fail(f"invalid dice in {label}")
    return count, int(match[2]), keep


def symbol(key, label):
    if not isinstance(key, str) or not SYMBOL.fullmatch(key):
        fail(f"{label} id {key} must be a formula symbol")
    if key in RESERVED or key.startswith("LV_"):
        fail(f"{label} id {key} is reserved")
    return key


def plain(document, key):
    """Entries keyed by a formula symbol, label optional (variables, progressions)."""
    items = document.setdefault(key, [])
    if not isinstance(items, list) or len(items) > LIMITS[key]:
        fail(f"invalid {key} list")
    seen = {}
    for item in items:
        if not isinstance(item, dict):
            fail(f"invalid {key} entry")
        key_id = symbol(item.get("id"), key)
        if key_id in seen:
            fail(f"duplicate {key} id {key_id}")
        text(item.get("label"), 80, f"{key} label", required=False)
        seen[key_id] = item
    return seen


def entries(document, key, label_limit=120):
    # Optional `image` on any entry: a campaign image ID, checked against the campaign on upload.
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
        text(item.get("image"), 40, f"{key} image", required=False)
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
        if isinstance(node, ast.Compare) and all(type(op) in COMPARISONS for op in node.ops):
            for part in [node.left, *node.comparators]:
                check(part)
            return
        if isinstance(node, ast.BoolOp) and isinstance(node.op, (ast.And, ast.Or)):
            for part in node.values:
                check(part)
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
    if isinstance(tree, ast.Compare):
        parts = [evaluate(part, values) for part in [tree.left, *tree.comparators]]
        return int(all(COMPARISONS[type(op)](a, b) for op, a, b in zip(tree.ops, parts, parts[1:])))
    if isinstance(tree, ast.BoolOp):
        parts = (bool(evaluate(part, values)) for part in tree.values)
        return int(all(parts) if isinstance(tree.op, ast.And) else any(parts))
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
        symbol(key, "stat")
        if integer(stat.get("min"), f"{key} min") > integer(stat.get("max"), f"{key} max"):
            fail(f"stat {key} min exceeds max")
    skills = entries(pack, "skills", 80)
    for key, skill in skills.items():
        if skill.get("stat") not in stats:
            fail(f"skill {key} references unknown stat")
        integer(skill.setdefault("cost", 1), f"{key} cost", 1, 10)
        integer(skill.setdefault("basic_min", 0), f"{key} basic_min", 0, 20)
        integer(skill.setdefault("max", 10), f"{key} max", 0, 20)
    symbols = levels(pack, stats)
    pools = entries(pack, "pools", 80)
    derived = entries(pack, "derived", 80)
    formulas, trees = {}, {}
    taken = set(stats) | set(symbols["variables"]) | set(symbols["progressions"])
    for key, pool in pools.items():
        if not SYMBOL.fullmatch(key) or key in taken or key in RESERVED or key.startswith("LV_"):
            fail(f"invalid pool id {key}")
        integer(pool.setdefault("floor", 0), f"{key} floor")
        if pool.get("warning") is not None:
            integer(pool["warning"], f"{key} warning")
        trees[key + "_MAX"], formulas[key + "_MAX"] = parse(pool.get("max"), key)
        formulas[key] = {key + "_MAX"}
    for key, item in derived.items():
        if (not SYMBOL.fullmatch(key) or key in taken or key in formulas
                or key in RESERVED or key.startswith("LV_")):
            fail(f"invalid derived id {key}")
        trees[key], formulas[key] = parse(item.get("formula"), key)
    pack["_order"] = order(formulas, symbols["all"])

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
            if cost.setdefault("round", "down") not in ("down", "up"):
                fail(f"item {key} round must be up or down")
        integer(item.setdefault("max", 1 if item["costs"] else 99), f"{key} max", 1, 99)
        for group in item.setdefault("requires", []):
            if not isinstance(group, list) or not group or any(x not in catalog for x in group):
                fail(f"item {key} requires unknown items")
        if any(x not in catalog for x in item.setdefault("uses_slot_of", [])):
            fail(f"item {key} uses slots of an unknown item")
        integer(item.setdefault("slot_cost", 1), f"{key} slot_cost", 1, 50)
        text(item.setdefault("exclusive", ""), 40, f"{key} exclusive", required=False)
        if item.setdefault("paired", False) not in (True, False) or (item["paired"] and not item["uses_slot_of"]):
            fail(f"item {key} paired needs uses_slot_of")
        for modifier in item.setdefault("modifiers", []):
            if not isinstance(modifier, dict) or modifier.get("target") not in targets:
                fail(f"item {key} modifies an unknown value")
            integer(modifier.get("value"), f"{key} modifier", -100, 100)
    for key, item in catalog.items():
        if any(not catalog[x]["slots"] for x in item["uses_slot_of"]):
            fail(f"item {key} uses slots of an item without slots")
    tree_of = Grants(pack, stats, skills, catalog, symbols).check()
    if pack.get("skill_value") is not None:
        tree, names = parse(pack["skill_value"], "skill_value")
        for name in names - symbols["all"] - {"RANK", "STAT"}:
            fail(f"unknown symbol {name} in skill_value")
        tree_of[pack["skill_value"]] = tree

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
            method_step(pack, key, step, stats)
    pack["_trees"] = trees
    pack["_formulas"] = tree_of
    return pack


def levels(pack, stats):
    """Validate level, variables, axis outlines and progressions; return the symbol sets formulas may use."""
    level = pack.get("level")
    if level is not None:
        if not isinstance(level, dict):
            fail("invalid level")
        text(level.get("label"), 40, "level label")
        low = integer(level.get("min"), "level min", 0, 100)
        integer(level.get("max"), "level max", low, 100)
    variables = plain(pack, "variables")
    axes = entries(pack, "axes", 80)
    per_option = set()
    for key, axis in axes.items():
        if axis.setdefault("repeatable", False) not in (True, False):
            fail(f"axis {key} repeatable must be true or false")
        if axis["repeatable"] and level is None:
            fail(f"repeatable axis needs level ({key})")
        if not isinstance(axis.get("options"), list) or not axis["options"]:
            fail(f"axis {key} needs options")
        for item in axis["options"]:
            if axis["repeatable"]:
                if not isinstance(item, dict) or not isinstance(item.get("id"), str) or not SYMBOL.fullmatch(item["id"]):
                    fail(f"option id {item.get('id') if isinstance(item, dict) else item} of axis {key} must be a formula symbol")
                per_option.add("LV_" + item["id"])
    early = ({"LEVEL"} if level is not None else set()) | per_option
    progressions = plain(pack, "progressions")
    for key, item in progressions.items():
        if item.get("by") not in early | set(variables):
            fail(f"progression {key} by unknown symbol {item.get('by')}")
        values = item.get("values")
        if not isinstance(values, list) or not 1 <= len(values) <= 100:
            fail(f"progression {key} needs 1-100 values")
        for value in values:
            integer(value, f"progression {key} value")
    early |= {k for k, p in progressions.items() if p["by"] not in variables}
    return {"early": early, "variables": variables, "progressions": progressions,
            "all": set(stats) | early | set(variables) | set(progressions)}


class Grants:
    """Validates options, grants and choices of axes and lists; returns parsed formulas by source."""

    def __init__(self, pack, stats, skills, catalog, symbols):
        self.pack, self.stats, self.skills, self.catalog, self.symbols = pack, stats, skills, catalog, symbols
        self.trees, self.choices, self.options = {}, {}, 0

    def check(self):
        self.lists = entries(self.pack, "lists", 80)
        for key, listing in self.lists.items():
            items = listing.get("entries")
            if not isinstance(items, list) or not items:
                fail(f"list {key} needs entries")
            seen = set()
            for entry in items:
                self.option(entry, 1, f"list {key}")
                if entry["id"] in seen:
                    fail(f"duplicate entry {entry['id']} in list {key}")
                seen.add(entry["id"])
                integer(entry.setdefault("rank", 0), f"{entry['id']} rank", 0, 20)
                tags = entry.setdefault("tags", [])
                if not isinstance(tags, list) or len(tags) > 20 or any(not isinstance(t, str) or not t or len(t) > 40 for t in tags):
                    fail(f"invalid tags of {entry['id']}")
                if entry.setdefault("repeatable", False) not in (True, False):
                    fail(f"{entry['id']} repeatable must be true or false")
        for key, axis in entries(self.pack, "axes", 80).items():
            seen = set()
            for item in axis["options"]:
                self.option(item, 0, f"axis {key}")
                if item["id"] in seen:
                    fail(f"duplicate option {item['id']} in axis {key}")
                seen.add(item["id"])
                if item.get("requires_multi") is not None:
                    self.formula(item["requires_multi"], set(self.stats), f"requires_multi of {item['id']}")
        return self.trees

    def formula(self, source, allowed, label):
        tree, names = parse(source, label)
        for name in sorted(names - allowed):
            fail(f"unknown symbol {name} in {label}")
        self.trees[source] = tree
        return tree

    def option(self, item, depth, where):
        if depth > MAX_DEPTH:
            fail(f"choices nested too deep in {where}")
        self.options += 1
        if self.options > LIMITS["options"]:
            fail("too many options and entries")
        if not isinstance(item, dict):
            fail(f"invalid option in {where}")
        text(item.get("id"), 60, f"option id in {where}")
        text(item.get("label"), 120, f"option label in {where}")
        text(item.get("summary"), 500, f"summary of {item['id']}", required=False)
        text(item.get("ref"), 200, f"ref of {item['id']}", required=False)
        text(item.get("image"), 40, f"image of {item['id']}", required=False)
        grants = item.setdefault("grants", [])
        if not isinstance(grants, list):
            fail(f"invalid grants of {item['id']}")
        for grant in grants:
            self.grant(grant, depth, item["id"])

    def grant(self, grant, depth, owner):
        if not isinstance(grant, dict) or set(grant) - GRANT_KEYS:
            fail(f"unknown grant key in {owner}")
        integer(grant.setdefault("level", 1), f"grant level in {owner}", 1, 100)
        if grant.setdefault("when", "any") not in ("any", "first", "later"):
            fail(f"grant when must be any, first or later in {owner}")
        early = set(self.stats) | self.symbols["early"]
        values = grant.setdefault("values", {})
        if not isinstance(values, dict):
            fail(f"invalid values in {owner}")
        for variable, source in values.items():
            if variable not in self.symbols["variables"]:
                fail(f"unknown variable {variable} in {owner}")
            self.formula(source, early, f"{variable} in {owner}")
        bonuses = grant.setdefault("stats", {})
        if not isinstance(bonuses, dict) or any(k not in self.stats for k in bonuses):
            fail(f"grant uses unknown stat in {owner}")
        for key, value in bonuses.items():
            integer(value, f"{key} bonus in {owner}", -10, 10)
        ranks = grant.setdefault("skills", {})
        if not isinstance(ranks, dict) or any(k not in self.skills for k in ranks):
            fail(f"grant uses unknown skill in {owner}")
        for key, value in ranks.items():
            integer(value, f"{key} rank in {owner}", 0, self.skills[key]["max"])
        items = grant.setdefault("items", [])
        if not isinstance(items, list) or any(x not in self.catalog for x in items):
            fail(f"grant uses unknown item in {owner}")
        features = grant.setdefault("features", [])
        if not isinstance(features, list):
            fail(f"invalid features in {owner}")
        for feature in features:
            if not isinstance(feature, dict):
                fail(f"invalid feature in {owner}")
            text(feature.get("id"), 60, f"feature id in {owner}")
            text(feature.get("label"), 120, f"feature label in {owner}")
            text(feature.get("summary"), 500, f"feature summary in {owner}", required=False)
            text(feature.get("ref"), 200, f"feature ref in {owner}", required=False)
        if grant.get("choice") is not None:
            self.choice(grant["choice"], depth)

    def choice(self, spec, depth):
        if not isinstance(spec, dict):
            fail("invalid choice")
        key = text(spec.get("id"), 60, "choice id")
        if key in self.choices:
            fail(f"duplicate choice id {key}")
        self.choices[key] = spec
        text(spec.get("label"), 120, f"choice {key} label")
        kind = spec.get("kind")
        if kind not in CHOICE_KINDS:
            fail(f"choice {key} kind must be options, list, skills or stats_bonus")
        early, where = self.symbols["early"], f"choice {key} count"
        tree, names = parse(spec.setdefault("count", "1"), where)
        if names - early:
            fail(f"{where} may only use early symbols (level, per-option levels, progressions by them)")
        self.trees[spec["count"]] = tree
        if kind == "options":
            options = spec.get("options")
            if not isinstance(options, list) or not options:
                fail(f"choice {key} needs options")
            seen = set()
            for item in options:
                self.option(item, depth + 1, f"choice {key}")
                if item["id"] in seen:
                    fail(f"duplicate option {item['id']} in choice {key}")
                seen.add(item["id"])
        elif kind == "list":
            if spec.get("list") not in self.lists:
                fail(f"choice {key} uses unknown list")
            tags = spec.setdefault("tags_any", [])
            if not isinstance(tags, list) or any(not isinstance(t, str) for t in tags):
                fail(f"invalid tags_any in choice {key}")
            for bound in ("rank_min", "rank_max"):
                if spec.get(bound) is not None:
                    tree, names = parse(spec[bound], f"choice {key} {bound}")
                    if names - early:
                        fail(f"choice {key} {bound} may only use early symbols")
                    self.trees[spec[bound]] = tree
        elif kind == "skills":
            among = spec.setdefault("among", list(self.skills))
            if not isinstance(among, list) or not among or any(x not in self.skills for x in among):
                fail(f"choice {key} uses unknown skill")
            integer(spec.get("rank"), f"choice {key} rank", 1, 20)
            integer(spec.setdefault("requires_rank", 0), f"choice {key} requires_rank", 0, 20)
        else:
            if spec.get("among") == "all":
                spec["among"] = list(self.stats)
            among = spec.get("among")
            if not isinstance(among, list) or not among or any(x not in self.stats for x in among):
                fail(f"choice {key} uses unknown stat")
            patterns = spec.get("patterns")
            if (not isinstance(patterns, list) or not 1 <= len(patterns) <= 5
                    or any(not isinstance(p, list) or not 1 <= len(p) <= len(among)
                           or any(type(v) is not int or not 1 <= v <= 10 for v in p) for p in patterns)):
                fail(f"choice {key} has an invalid pattern")
            integer(spec.get("cap"), f"choice {key} cap")


def method_step(pack, key, step, stats):
    axes = {a["id"] for a in pack.get("axes", [])}
    if step["type"] == "axes":
        chosen = step.get("axes")
        if not isinstance(chosen, list) or not chosen or any(a not in axes for a in chosen):
            fail(f"method {key} uses unknown axis")
    elif step["type"] == "stats_array":
        values = step.get("values")
        if not isinstance(values, list) or len(values) != len(stats):
            fail(f"method {key} stats_array must give a value for every stat")
        for value in values:
            if type(value) is not int or any(not s["min"] <= value <= s["max"] for s in stats.values()):
                fail(f"method {key} stats_array value {value} out of range")
    elif step["type"] == "stats_roll_assign":
        dice_keep(step.get("dice"), f"method {key}")
    elif step["type"] == "stats_points" and step.get("costs") is not None:
        costs, low, high = step["costs"], step.get("min"), step.get("max")
        if not isinstance(costs, dict) or type(low) is not int or type(high) is not int or low > high:
            fail(f"method {key} costs need min and max")
        for value in range(low, high + 1):
            if type(costs.get(str(value))) is not int or costs[str(value)] < 0:
                fail(f"method {key} needs a cost for every value {low}-{high}")


def stat_values(pack, values, label):
    stats = {s["id"]: s for s in pack["stats"]}
    if not isinstance(values, dict) or set(values) != set(stats):
        fail(f"{label} must set every stat")
    for key, value in values.items():
        integer(value, f"{label} {key}", stats[key]["min"], stats[key]["max"])
    return values


# ── Character axes and choices ──────────────────────────────────────────────

def progression(item, values):
    by = values.get(item["by"], 0)
    return item["values"][min(by, len(item["values"])) - 1] if by >= 1 else 0


def resolve(pack, level, axes, choices, stats):
    """Walk the chosen axis options and their choices: early symbols, open choices, grants, problems.

    `axes` maps axis id -> [{"option", "levels"}]; only the listed axes are checked. `choices` maps a
    choice instance key -> {"picks": [...]} or {"values": {...}}; keys outside the current tree are ignored.
    """
    return Resolution(pack, level, axes, choices, stats).run()


class Resolution:
    def __init__(self, pack, level, axes, choices, stats):
        self.pack, self.choices, self.base = pack, choices, stats
        self.trees = pack["_formulas"]
        self.lists = {x["id"]: {e["id"]: e for e in x["entries"]} for x in pack.get("lists", [])}
        self.symbols = {"LEVEL": level} if pack.get("level") else {}
        self.axes = axes
        self.open, self.picked, self.problems, self.used, self.bonus_caps, self.rank_checks = {}, {}, [], {}, [], []
        self.grants = {"stats": {}, "ranks": {}, "items": [], "features": [], "values": []}
        self.rank_sources = {}

    def run(self):
        defs = {a["id"]: a for a in self.pack.get("axes", [])}
        chosen = {}
        for key, picks in self.axes.items():
            if key in defs:
                chosen[key] = self.axis(defs[key], picks)
        for item in self.pack.get("progressions", []):
            if item["by"] in self.symbols:
                self.symbols[item["id"]] = progression(item, self.symbols)
        for key, picks in chosen.items():
            repeatable = defs[key]["repeatable"]
            for index, (item, levels) in enumerate(picks):
                gate = levels if repeatable else self.symbols.get("LEVEL", 1)
                self.apply(item, gate, index == 0, item["label"], "")
        self.finish(defs, chosen)
        return {"symbols": self.symbols, "open": self.open, "grants": self.grants,
                "picked": self.picked, "problems": self.problems}

    def axis(self, axis, picks):
        options = {o["id"]: o for o in axis["options"]}
        if axis["repeatable"]:
            for item in axis["options"]:
                self.symbols["LV_" + item["id"]] = 0
        if not isinstance(picks, list) or not picks:
            self.problems.append(f"Choose {axis['label']}")
            return []
        result, seen = [], set()
        for pick in picks:
            item = options.get(pick.get("option")) if isinstance(pick, dict) else None
            if item is None or item["id"] in seen:
                self.problems.append(f"{axis['label']}: invalid or repeated option")
                continue
            seen.add(item["id"])
            levels = pick.get("levels") if axis["repeatable"] else None
            if axis["repeatable"]:
                if type(levels) is not int or levels < 1:
                    self.problems.append(f"{item['label']}: levels must be at least 1")
                    continue
                self.symbols["LV_" + item["id"]] = levels
            result.append((item, levels))
        if not axis["repeatable"] and len(picks) != 1:
            self.problems.append(f"Choose exactly one {axis['label']}")
        if axis["repeatable"]:
            total = sum(levels for _, levels in result)
            if total != self.symbols["LEVEL"]:
                self.problems.append(f"{axis['label']}: levels add up to {total} of {self.symbols['LEVEL']}")
        return result

    def apply(self, owner, gate, first, source, prefix):
        for grant in owner["grants"]:
            if grant["level"] > gate or (grant["when"] == "first" and not first) or (grant["when"] == "later" and first):
                continue
            for variable, formula in grant["values"].items():
                self.grants["values"].append((variable, self.trees[formula]))
            for key, value in grant["stats"].items():
                self.grants["stats"][key] = self.grants["stats"].get(key, 0) + value
            for key, rank in grant["skills"].items():
                self.rank(key, rank, None)
            self.grants["items"] += grant["items"]
            self.grants["features"] += [{**f, "source": source} for f in grant["features"]]
            if grant.get("choice"):
                self.choice(grant["choice"], gate, first, source, prefix)

    def rank(self, skill, rank, key):
        self.grants["ranks"][skill] = max(self.grants["ranks"].get(skill, 0), rank)
        self.rank_sources.setdefault(skill, []).append((key, rank))

    def number(self, formula):
        return math.floor(evaluate(self.trees[formula], self.symbols))

    def choice(self, spec, gate, first, source, prefix):
        key = f"{prefix}/{spec['id']}" if prefix else spec["id"]
        count = self.number(spec["count"])
        if count < 1:
            return
        kind = spec["kind"]
        if kind == "options":
            available = {o["id"]: o for o in spec["options"]}
        elif kind == "list":
            low = self.number(spec["rank_min"]) if spec.get("rank_min") is not None else None
            high = self.number(spec["rank_max"]) if spec.get("rank_max") is not None else None
            available = {k: e for k, e in self.lists[spec["list"]].items()
                         if (not spec["tags_any"] or set(spec["tags_any"]) & set(e["tags"]))
                         and (low is None or e["rank"] >= low) and (high is None or e["rank"] <= high)}
        elif kind == "skills":
            available = {k: None for k in spec["among"]}
        else:
            available = None
        self.open[key] = {"id": spec["id"], "label": spec["label"], "kind": kind, "count": count,
                          "available": list(available) if available is not None else None, "source": source}
        data = self.choices.get(key)
        if not isinstance(data, dict):
            self.problems.append(f"Choose: {spec['label']}")
            return
        if kind == "stats_bonus":
            self.stats_bonus(spec, data.get("values"), key)
            return
        picks = data.get("picks")
        if not isinstance(picks, list) or any(not isinstance(x, str) for x in picks):
            self.problems.append(f"Choose: {spec['label']}")
            return
        if len(picks) != count or len(set(picks)) != len(picks):
            self.problems.append(f"{spec['label']}: choose {count}")
        valid = [x for x in dict.fromkeys(picks) if x in available]
        if len(valid) != len(set(picks)):
            self.problems.append(f"{spec['label']}: choice outside the allowed options")
        labels = []
        for pick in valid:
            if kind == "skills":
                self.rank(pick, spec["rank"], key)
                if spec["requires_rank"]:
                    self.rank_checks.append((pick, spec["requires_rank"], key, spec["label"]))
                labels.append(next(s["label"] for s in self.pack["skills"] if s["id"] == pick))
                continue
            item = available[pick]
            if kind == "list":
                self.used.setdefault((spec["list"], pick), []).append(key)
            labels.append(item["label"])
            self.apply(item, gate, first, item["label"], f"{key}/{pick}")
        self.picked[key] = labels

    def stats_bonus(self, spec, values, key):
        if (not isinstance(values, dict) or any(k not in spec["among"] for k in values)
                or any(type(v) is not int or v < 1 for v in values.values())
                or sorted(values.values(), reverse=True) not in [sorted(p, reverse=True) for p in spec["patterns"]]):
            self.problems.append(f"{spec['label']}: invalid distribution")
            return
        for stat, value in values.items():
            self.grants["stats"][stat] = self.grants["stats"].get(stat, 0) + value
        self.bonus_caps.append((spec, values))
        labels = {s["id"]: s["label"] for s in self.pack["stats"]}
        self.picked[key] = [f"{labels[k]} +{v}" for k, v in values.items()]

    def finish(self, defs, chosen):
        self.collisions()
        if self.base is None:  # statistics not set yet: caps and combination rules wait for them
            return
        stats = {k: self.base.get(k, 0) + self.grants["stats"].get(k, 0) for k in self.base}
        for spec, values in self.bonus_caps:
            if any(stats.get(k, 0) > spec["cap"] for k in values):
                self.problems.append(f"{spec['label']}: above {spec['cap']}")
        for key, picks in chosen.items():
            if defs[key]["repeatable"] and len(picks) > 1:
                for item, _ in picks:
                    if item.get("requires_multi") and not evaluate(self.trees[item["requires_multi"]], stats):
                        self.problems.append(f"{item['label']}: requires {item['requires_multi']} to combine")

    def collisions(self):
        for (listing, entry), keys in self.used.items():
            if len(keys) > 1 and not self.lists[listing][entry]["repeatable"]:
                self.problems.append(f"{self.lists[listing][entry]['label']}: chosen more than once")
        for skill, needed, key, label in self.rank_checks:
            if not any(rank >= needed for source, rank in self.rank_sources.get(skill, []) if source != key):
                name = next(s["label"] for s in self.pack["skills"] if s["id"] == skill)
                self.problems.append(f"{label}: {name} needs rank {needed} first")


# ── Sheet computation ───────────────────────────────────────────────────────

def item_problems(pack, items):
    """Requirements, slots and counts for a list of catalog IDs (duplicates = several copies)."""
    catalog = {i["id"]: i for i in pack["catalog"]}
    problems = []
    if any(x not in catalog for x in items):
        return ["unknown item"]
    counts = {x: items.count(x) for x in items}
    groups = {}
    for key, count in counts.items():
        if catalog[key]["exclusive"]:
            groups.setdefault(catalog[key]["exclusive"], []).extend([catalog[key]["label"]] * count)
    for labels in groups.values():
        if len(labels) > 1:
            problems.append(f"Only one of: {', '.join(dict.fromkeys(labels))}")
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
