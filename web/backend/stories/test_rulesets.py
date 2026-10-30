"""Contract for engine-agnostic ruleset packs; uses an invented mini-system, never commercial rules."""
import copy

from django.test import SimpleTestCase

from . import rulesets
from .domain_errors import DomainError


def mini():
    return {
        "schema": "narrative-ruleset/1", "id": "mini", "version": 1, "title": "Mini", "language": "pl",
        "stats": [{"id": "SIL", "label": "Siła", "min": 2, "max": 8},
                  {"id": "WOL", "label": "Wola", "min": 2, "max": 8}],
        "skills": [{"id": "walka", "label": "Walka", "stat": "SIL", "cost": 2, "basic_min": 2, "max": 6}],
        "pools": [{"id": "DUSZA", "label": "Dusza", "max": "WOL * 10", "floor": 1}],
        "derived": [{"id": "ZYCIE", "label": "Życie", "formula": "10 + 5 * ceil((SIL + WOL) / 2)"},
                    {"id": "WOL_AKT", "label": "Wola teraz", "formula": "min(WOL, ceil(DUSZA / 10))"}],
        "catalog": [
            {"id": "ramie", "label": "Ramię", "price": 500, "slots": 2,
             "costs": {"DUSZA": {"dice": "2d6", "fixed": 7}}},
            {"id": "ostrze", "label": "Ostrze", "price": 200, "requires": [["ramie"]],
             "uses_slot_of": ["ramie"], "costs": {"DUSZA": {"dice": "1d6", "fixed": 3}},
             "modifiers": [{"target": "walka", "value": 1}]},
            {"id": "plaszcz", "label": "Płaszcz", "price": 50}],
        "tables": [
            {"id": "dom", "label": "Skąd jesteś?", "dice": "1d6", "entries": [
                {"range": [1, 3], "text": "Z portu."},
                {"range": [4, 6], "text": "Z gór.", "next": "wrog"}]},
            {"id": "wrog", "label": "Kto cię ściga?", "dice": "1d2", "entries": [
                {"range": [1, 1], "text": "Celnik."}, {"range": [2, 2], "text": "Brat."}]}],
        "roles": [{"id": "wojownik", "label": "Wojownik", "skill_package": {"walka": 4},
                   "stat_rows": [{"SIL": 7, "WOL": 4}],
                   "gear_package": [{"choose": 1, "items": ["ostrze", "plaszcz"]}]}],
        "methods": [{"id": "pelna", "label": "Pełna", "steps": [
            {"type": "role"}, {"type": "stats_points", "pool": 10},
            {"type": "shop", "budgets": [{"amount": 1000, "label": "Sprzęt"}]},
            {"type": "tables", "tables": ["dom"]}, {"type": "name"}]}],
    }


def rejects(test, document, message):
    with test.assertRaisesRegex(DomainError, message):
        rulesets.load(document)


class FormulaTests(SimpleTestCase):
    def test_whitelisted_arithmetic_and_functions(self):
        tree, names = rulesets.parse("10 + 5 * ceil((SIL + WOL) / 2) - max(1, -WOL)", "x")
        self.assertEqual(names, {"SIL", "WOL"})
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 5, "WOL": 6}), 10 + 5 * 6 - 1)

    def test_code_and_unsupported_syntax_are_rejected(self):
        for source in ["__import__('os').system('id')", "SIL.real", "(lambda: 1)()", "SIL ** 99",
                       "[1][0]", "'a' * 3", "open('x')", "min(SIL, key=1)", "SIL if WOL else 1",
                       "", "   ", None, "1 +", "x" * 201, "True", "SIL // 2"]:
            with self.subTest(source=source), self.assertRaises(DomainError):
                rulesets.parse(source, "x")

    def test_division_by_zero_is_zero_not_crash(self):
        tree, _ = rulesets.parse("SIL / (WOL - WOL)", "x")
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 4, "WOL": 4}), 0)


class LoadTests(SimpleTestCase):
    def test_valid_pack_loads_as_private_copy(self):
        document = mini()
        pack = rulesets.load(document)
        self.assertEqual(document, mini())  # input untouched
        pack["stats"][0]["max"] = 99
        self.assertEqual(document["stats"][0]["max"], 8)
        self.assertEqual(pack["_order"].index("DUSZA_MAX") < pack["_order"].index("DUSZA"), True)

    def test_dangling_references_reject_whole_pack(self):
        cases = [
            (lambda d: d["skills"][0].update(stat="ZRE"), "unknown stat"),
            (lambda d: d["derived"][0].update(formula="SIL + ZRE"), "unknown symbol ZRE"),
            (lambda d: d["catalog"][1].update(requires=[["noga"]]), "requires unknown"),
            (lambda d: d["catalog"][1].update(uses_slot_of=["plaszcz"]), "without slots"),
            (lambda d: d["catalog"][1]["costs"].update(MANA={"dice": "1d6", "fixed": 1}), "unknown pool"),
            (lambda d: d["catalog"][1]["modifiers"].append({"target": "magia", "value": 1}), "unknown value"),
            (lambda d: d["tables"][0]["entries"][1].update(next="brak"), "unknown table"),
            (lambda d: d["roles"][0]["skill_package"].update(magia=2), "unknown skill"),
            (lambda d: d["roles"][0]["gear_package"][0]["items"].append("tarcza"), "unknown items"),
            (lambda d: d["methods"][0]["steps"][3].update(tables=["brak"]), "unknown table"),
        ]
        for change, message in cases:
            with self.subTest(message=message):
                document = mini()
                change(document)
                rejects(self, document, message)

    def test_structural_errors(self):
        cases = [
            (lambda d: d.update(schema="narrative-ruleset/2"), "unsupported schema"),
            (lambda d: d.update(version=0), "pack version"),
            (lambda d: d.update(language="de"), "language"),
            (lambda d: d["stats"].append(dict(d["stats"][0])), "duplicate stats"),
            (lambda d: d["stats"][0].update(min=9), "min exceeds max"),
            (lambda d: d["stats"][0].update(id="sil a"), "formula symbol"),
            (lambda d: d["derived"].append({"id": "SIL", "label": "x", "formula": "1"}), "invalid derived id"),
            (lambda d: d["derived"].extend([{"id": "A", "label": "a", "formula": "B"},
                                            {"id": "B", "label": "b", "formula": "A"}]), "cycle"),
            (lambda d: d["tables"][0]["entries"].pop(), "cover 1-6"),
            (lambda d: d["tables"][0]["entries"][0].update(range=[1, 4]), "cover 1-6"),
            (lambda d: d["tables"][0].update(dice="d6"), "invalid dice"),
            (lambda d: d["tables"][1]["entries"][0].update(next="dom"), "cycle"),
            (lambda d: d["catalog"][0]["costs"]["DUSZA"].update(dice="7"), "invalid dice"),
            (lambda d: d["catalog"][0].update(price=-1), "price"),
            (lambda d: d["roles"][0]["stat_rows"].append({"SIL": 7}), "every stat"),
            (lambda d: d["roles"][0]["stat_rows"].append({"SIL": 9, "WOL": 4}), "SIL"),
            (lambda d: d["roles"][0]["gear_package"][0].update(choose=3), "choose"),
            (lambda d: d.update(methods=[]), "at least one method"),
            (lambda d: d["methods"][0]["steps"].append({"type": "eval"}), "unknown step"),
            (lambda d: d["methods"][0]["steps"][2].update(loss_mode="max"), "loss_mode"),
            (lambda d: d.update(title="  "), "pack title"),
        ]
        for change, message in cases:
            with self.subTest(message=message):
                document = mini()
                change(document)
                rejects(self, document, message)
        for bad in [None, [], "x", {"schema": "narrative-ruleset/1"}]:
            with self.subTest(bad=bad), self.assertRaises(DomainError):
                rulesets.load(bad)


class SheetTests(SimpleTestCase):
    def setUp(self):
        self.pack = rulesets.load(mini())

    def test_derived_values_pools_and_modifiers(self):
        result = rulesets.sheet(self.pack, {"SIL": 5, "WOL": 6}, {"walka": 4}, ["ramie", "ostrze"])
        self.assertEqual(result["derived"]["ZYCIE"], 40)
        self.assertEqual(result["pools"]["DUSZA"], {"label": "Dusza", "max": 60, "current": 50})
        self.assertEqual(result["derived"]["WOL_AKT"], 5)  # spent soul lowers current will
        self.assertEqual(result["modifiers"], {"walka": 1})
        self.assertEqual(result["price"], 700)
        self.assertEqual(result["problems"], [])

    def test_rolled_losses_override_fixed_costs(self):
        result = rulesets.sheet(self.pack, {"SIL": 5, "WOL": 6}, items=["ramie"], losses={"DUSZA": 12})
        self.assertEqual(result["pools"]["DUSZA"]["current"], 48)

    def test_requirements_slots_counts_and_floor(self):
        cases = [
            (["ostrze"], "requires Ramię"),
            (["ramie", "ostrze", "ostrze"], "max 1"),
            (["ramie", "ramie"], "max 1"),
        ]
        for items, problem in cases:
            with self.subTest(items=items):
                problems = rulesets.sheet(self.pack, {"SIL": 5, "WOL": 6}, items=items)["problems"]
                self.assertTrue(any(problem in p for p in problems), problems)
        pack = mini()
        pack["catalog"][1]["max"] = 3
        pack = rulesets.load(pack)
        problems = rulesets.sheet(pack, {"SIL": 5, "WOL": 6}, items=["ramie"] + ["ostrze"] * 3)["problems"]
        self.assertIn("Ostrze: no free slot", problems)
        low = rulesets.sheet(self.pack, {"SIL": 5, "WOL": 2}, items=["ramie"], losses={"DUSZA": 20})
        self.assertIn("Dusza: below 1", low["problems"])
        pack = mini()
        pack["pools"][0]["warning"] = 55
        pack = rulesets.load(pack)
        warned = rulesets.sheet(pack, {"SIL": 5, "WOL": 6}, items=["ramie"])
        self.assertEqual((warned["problems"], warned["warnings"]), ([], ["Dusza: below 55"]))
        document = mini()
        document["pools"][0]["warning"] = "dużo"
        rejects(self, document, "warning")
        self.assertEqual(rulesets.sheet(self.pack, {"SIL": 5, "WOL": 6}, items=["laser"])["problems"],
                         ["unknown item"])

    def test_invalid_stats_are_rejected(self):
        for stats in [{"SIL": 5}, {"SIL": 1, "WOL": 6}, {"SIL": 5, "WOL": 9}, {"SIL": "5", "WOL": 6},
                      {"SIL": 5, "WOL": 6, "ZRE": 3}, {"SIL": 5.5, "WOL": 6}]:
            with self.subTest(stats=stats), self.assertRaises(DomainError):
                rulesets.sheet(self.pack, stats)

    def test_multi_slot_and_paired_options(self):
        pack = mini()
        pack["catalog"] += [
            {"id": "dzialko", "label": "Działko", "price": 500, "requires": [["ramie"]],
             "uses_slot_of": ["ramie"], "slot_cost": 2, "costs": {"DUSZA": {"dice": "1d6/2", "fixed": 2}}},
            {"id": "noktowizja", "label": "Noktowizja", "price": 100, "max": 2, "paired": True,
             "uses_slot_of": ["ramie"]}]
        pack["catalog"][0]["max"] = 2
        pack = rulesets.load(pack)
        stats = {"SIL": 5, "WOL": 6}
        self.assertEqual(rulesets.sheet(pack, stats, items=["ramie", "dzialko"])["problems"], [])
        self.assertIn("Ostrze: no free slot",
                      rulesets.sheet(pack, stats, items=["ramie", "dzialko", "ostrze"])["problems"])
        self.assertEqual(rulesets.sheet(pack, stats, items=["ramie", "ramie", "noktowizja", "noktowizja"])["problems"], [])
        for items in (["ramie", "ramie", "noktowizja"], ["ramie", "noktowizja", "noktowizja"]):
            with self.subTest(items=items):
                self.assertIn("Noktowizja: install as a pair in two hosts", rulesets.sheet(pack, stats, items=items)["problems"])
        for bad in ({"slot_cost": 0}, {"paired": "yes"}, {"uses_slot_of": [], "paired": True}):
            document = mini()
            document["catalog"][1].update(bad)
            with self.subTest(bad=bad), self.assertRaises(DomainError):
                rulesets.load(document)
        document = mini()
        document["tables"][0]["dice"] = "1d6/2"
        rejects(self, document, "cannot be divided")

    def test_exclusive_groups_allow_one_item(self):
        pack = mini()
        pack["catalog"] += [{"id": "turbo", "label": "Turbo", "exclusive": "speed"},
                            {"id": "nitro", "label": "Nitro", "exclusive": "speed", "max": 2}]
        pack = rulesets.load(pack)
        stats = {"SIL": 5, "WOL": 6}
        self.assertEqual(rulesets.sheet(pack, stats, items=["turbo", "plaszcz"])["problems"], [])
        for items in (["turbo", "nitro"], ["nitro", "nitro"]):
            with self.subTest(items=items):
                self.assertTrue(any(p.startswith("Only one of:") for p in rulesets.sheet(pack, stats, items=items)["problems"]))
        for bad in ({"exclusive": 7}, {"exclusive": "x" * 41}):
            document = mini()
            document["catalog"][2].update(bad)
            with self.subTest(bad=bad), self.assertRaises(DomainError):
                rulesets.load(document)
        document = mini()
        document["catalog"][0]["costs"]["DUSZA"]["round"] = "sideways"
        rejects(self, document, "round must be")

    def test_removing_a_foundation_reports_dependent_items(self):
        self.assertEqual(rulesets.dependents(self.pack, ["ramie", "ostrze", "plaszcz"], "ramie"), ["ostrze"])
        self.assertEqual(rulesets.dependents(self.pack, ["ramie", "ostrze"], "ostrze"), [])
