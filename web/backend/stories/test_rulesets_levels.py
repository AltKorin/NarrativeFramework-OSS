"""Ruleset engine with levels, character axes and choices; an invented mini-system, never commercial rules."""
from django.test import SimpleTestCase

from . import rulesets
from .domain_errors import DomainError
from .test_rulesets import rejects


def stats_bonus(cid, patterns=([2, 1], [1, 1, 1]), among=("SIL", "ZRE", "WIE")):
    return {"id": cid, "label": "Premie do cech", "kind": "stats_bonus",
            "among": list(among) if isinstance(among, tuple) else among, "patterns": [list(x) for x in patterns], "cap": 20}


def spell(sid, label, rank):
    return {"id": sid, "label": label, "rank": rank, "tags": ["mag"], "summary": f"{label} — opis.", "ref": f"zasady/{sid}"}


def mini_levels():
    """Invented 'Mini-poziomy' system: three stats, five circles, a repeatable path axis and an origin axis."""
    return {
        "schema": "narrative-ruleset/1", "id": "mini-poziomy", "version": 1, "title": "Mini-poziomy", "language": "pl",
        "stats": [{"id": s, "label": s.title(), "min": 3, "max": 20} for s in ("SIL", "ZRE", "WIE")],
        "level": {"label": "Krąg", "min": 1, "max": 5},
        "variables": [{"id": "ZYCIE_KL", "label": "Życie ze ścieżek"}, {"id": "MAGIA"}],
        "progressions": [{"id": "BIEG", "by": "LEVEL", "values": [2, 2, 2, 3, 3]},
                         {"id": "SZTUCZKI_MAG", "by": "LV_mag", "values": [2, 2, 3, 3, 4]},
                         {"id": "KOMORKI", "by": "MAGIA", "values": [2, 3, 4, 5, 6]}],
        "skills": [{"id": "walka", "label": "Walka", "stat": "SIL", "max": 2},
                   {"id": "skradanie", "label": "Skradanie", "stat": "ZRE", "max": 2},
                   {"id": "wiedza", "label": "Wiedza", "stat": "WIE", "max": 2},
                   {"id": "odp_sil", "label": "Odporność siły", "stat": "SIL", "max": 2, "category": "Odporność"}],
        "skill_value": "floor((STAT - 10) / 2) + RANK * BIEG",
        "derived": [{"id": "ZYCIE", "label": "Życie", "formula": "ZYCIE_KL + floor((SIL - 10) / 2) * LEVEL"},
                    {"id": "KP", "label": "Obrona", "formula": "10 + floor((ZRE - 10) / 2)"},
                    {"id": "KOMORKI_TERAZ", "label": "Komórki", "formula": "KOMORKI"}],
        "catalog": [{"id": "siec", "label": "Sieć", "price": 1}],
        "axes": [
            {"id": "klasa", "label": "Ścieżka", "repeatable": True, "options": [
                {"id": "woj", "label": "Wojownik", "summary": "Walczy.", "requires_multi": "SIL >= 13", "grants": [
                    {"when": "first", "values": {"ZYCIE_KL": "10 + (LV_woj - 1) * 6"}, "skills": {"odp_sil": 1}},
                    {"when": "later", "values": {"ZYCIE_KL": "LV_woj * 6"}},
                    {"choice": {"id": "woj_umiej", "label": "Umiejętności wojownika", "kind": "skills",
                                "among": ["walka", "skradanie"], "rank": 1}},
                    {"level": 3, "choice": {"id": "woj_styl", "label": "Styl walki", "kind": "options", "options": [
                        {"id": "tarcza", "label": "Tarcza", "grants": [
                            {"features": [{"id": "mur", "label": "Mur tarcz", "summary": "Osłania sąsiada."}]}]},
                        {"id": "luk", "label": "Łuk", "grants": [{"stats": {"ZRE": 1}}]}]}},
                    {"level": 4, "choice": {"id": "woj_dar_4", "label": "Dar", "kind": "list", "list": "dary"}}]},
                {"id": "mag", "label": "Mag", "requires_multi": "WIE >= 13", "grants": [
                    {"when": "first", "values": {"ZYCIE_KL": "6 + (LV_mag - 1) * 4"}},
                    {"when": "later", "values": {"ZYCIE_KL": "LV_mag * 4"}},
                    {"values": {"MAGIA": "LV_mag"}},
                    {"choice": {"id": "mag_sztuczki", "label": "Sztuczki", "kind": "list", "list": "zaklecia",
                                "tags_any": ["mag"], "rank_max": "0", "count": "SZTUCZKI_MAG"}},
                    {"choice": {"id": "mag_zaklecia", "label": "Zaklęcia", "kind": "list", "list": "zaklecia",
                                "tags_any": ["mag"], "rank_min": "1", "rank_max": "1 + floor((LV_mag - 1) / 2)",
                                "count": "LV_mag + 1"}}]}]},
            {"id": "pochodzenie", "label": "Pochodzenie", "options": [
                {"id": "rybak", "label": "Rybak", "grants": [
                    {"skills": {"skradanie": 1}, "items": ["siec"], "choice": stats_bonus("rybak_cechy")}]},
                {"id": "uczony", "label": "Uczony", "grants": [
                    {"skills": {"wiedza": 1}, "choice": stats_bonus("uczony_cechy")}]}]}],
        "lists": [
            {"id": "zaklecia", "label": "Zaklęcia", "entries": [
                spell("iskra", "Iskra", 0), spell("swiatlo", "Światło", 0), spell("tarcza_m", "Tarcza maga", 1),
                spell("pocisk", "Pocisk", 1), spell("sen", "Sen", 1), spell("kula", "Kula", 2)]},
            {"id": "dary", "label": "Dary", "entries": [
                {"id": "wzmocnienie", "label": "Wzmocnienie cech", "repeatable": True,
                 "grants": [{"choice": stats_bonus("wzm", patterns=([2], [1, 1]), among="all")}]},
                {"id": "twardy", "label": "Twardy", "grants": [{"values": {"ZYCIE_KL": "2 * LEVEL"}}]}]}],
        "methods": [
            {"id": "zestaw", "label": "Zestaw", "steps": [
                {"type": "axes", "axes": ["klasa", "pochodzenie"]}, {"type": "stats_array", "values": [15, 14, 8]},
                {"type": "choices"}, {"type": "name"}]},
            {"id": "punkty", "label": "Punkty", "steps": [
                {"type": "axes", "axes": ["klasa", "pochodzenie"]},
                {"type": "stats_points", "pool": 10, "min": 8, "max": 15,
                 "costs": {"8": 0, "9": 1, "10": 2, "11": 3, "12": 4, "13": 5, "14": 7, "15": 9}},
                {"type": "choices"}, {"type": "name"}]},
            {"id": "rzut", "label": "Rzut", "steps": [
                {"type": "axes", "axes": ["klasa", "pochodzenie"]}, {"type": "stats_roll_assign", "dice": "4d6kh3"},
                {"type": "choices"}, {"type": "name"}]}],
    }


def axis(document, key):
    return next(a for a in document["axes"] if a["id"] == key)


def option(document, key):
    return next(o for a in document["axes"] for o in a["options"] if o["id"] == key)


def grant(document, key, index):
    return option(document, key)["grants"][index]


def choice(document, key):
    def walk(grants):
        for g in grants:
            c = g.get("choice")
            if c:
                if c["id"] == key:
                    return c
                found = walk([x for o in c.get("options", []) for x in o.get("grants", [])])
                if found:
                    return found
        return None
    sources = [o["grants"] for a in document["axes"] for o in a["options"]]
    sources += [e.get("grants", []) for lst in document["lists"] for e in lst["entries"]]
    return next(c for grants in sources if (c := walk(grants)))


class FormulaComparisonTests(SimpleTestCase):
    def test_comparisons_and_boolean_ops_yield_1_or_0(self):
        tree, names = rulesets.parse("SIL >= 13 and WIE >= 13", "x")
        self.assertEqual(names, {"SIL", "WIE"})
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 13, "WIE": 14}), 1)
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 12, "WIE": 14}), 0)
        tree, _ = rulesets.parse("SIL < 3 or SIL == 9", "x")
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 9}), 1)
        tree, _ = rulesets.parse("1 < SIL < 3", "x")
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 2}), 1)
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 3}), 0)

    def test_still_rejected(self):
        for source in ["True", "not SIL", "SIL is 1", "SIL in [1]", "SIL < 'a'"]:
            with self.subTest(source=source), self.assertRaises(DomainError):
                rulesets.parse(source, "x")

    def test_dice_keep(self):
        self.assertEqual(rulesets.dice_keep("4d6kh3", "x"), (4, 6, 3))
        self.assertEqual(rulesets.dice_keep("3d6", "x"), (3, 6, 3))
        for bad in ["4d6kh5", "4d6kh0", "4d6/2", "d6", "4d6kh", 7]:
            with self.subTest(bad=bad), self.assertRaises(DomainError):
                rulesets.dice_keep(bad, "x")
        with self.assertRaises(DomainError):
            rulesets.dice("4d6kh3", "x")


class LevelsLoadTests(SimpleTestCase):
    def test_valid_levels_pack_loads(self):
        document = mini_levels()
        pack = rulesets.load(document)
        self.assertEqual(pack["level"], {"label": "Krąg", "min": 1, "max": 5})
        self.assertEqual(document, mini_levels())  # input untouched
        self.assertEqual(choice(pack, "woj_umiej")["count"], "1")  # defaults filled in
        self.assertEqual(grant(pack, "woj", 2)["level"], 1)
        self.assertEqual(grant(pack, "woj", 2)["when"], "any")

    def test_invalid_shapes_reject_whole_pack(self):
        def add_stat(d):
            d["stats"].append({"id": "LEVEL", "label": "x", "min": 1, "max": 2})
        cases = [
            (lambda d: d.pop("level"), "repeatable axis needs level"),
            (add_stat, "reserved"),
            (lambda d: d["variables"].append({"id": "LV_x"}), "reserved"),
            (lambda d: d["progressions"][0].update(by="ZRE"), "progression BIEG"),
            (lambda d: d["progressions"][0].update(values=[]), "progression BIEG"),
            (lambda d: d["level"].update(min=6), "level"),
            (lambda d: option(d, "woj").update(id="woj-x"), "symbol"),
            (lambda d: option(d, "woj").update(requires_multi="LEVEL >= 1"), "unknown symbol LEVEL"),
            (lambda d: grant(d, "woj", 0).update(level=0), "level"),
            (lambda d: grant(d, "woj", 0).update(when="sometimes"), "when"),
            (lambda d: grant(d, "woj", 0)["values"].update(NIEMA="1"), "unknown variable"),
            (lambda d: grant(d, "woj", 0).update(magic=1), "unknown grant"),
            (lambda d: grant(d, "woj", 0)["skills"].update(odp_sil=3), "rank"),
            (lambda d: grant(d, "woj", 0).update(items=["miecz"]), "unknown item"),
            (lambda d: choice(d, "woj_umiej").update(kind="magic"), "kind"),
            (lambda d: choice(d, "woj_umiej").update(among=["latanie"]), "unknown skill"),
            (lambda d: choice(d, "mag_sztuczki").update(list="brak"), "unknown list"),
            (lambda d: choice(d, "rybak_cechy").update(patterns=[[0]]), "pattern"),
            (lambda d: choice(d, "uczony_cechy").update(id="rybak_cechy"), "duplicate choice"),
            (lambda d: d["lists"][0]["entries"].append(spell("iskra", "Iskra", 0)), "duplicate"),
            (lambda d: d["methods"][0]["steps"][1].update(values=[15, 14]), "every stat"),
            (lambda d: d["methods"][0]["steps"][1].update(values=[15, 14, 99]), "out of range"),
            (lambda d: d["methods"][1]["steps"][1]["costs"].pop("12"), "cost for every value"),
            (lambda d: d["methods"][2]["steps"][1].update(dice="4d6kh9"), "dice"),
            (lambda d: d["methods"][0]["steps"][0].update(axes=["rasa"]), "unknown axis"),
            (lambda d: d.update(skill_value="RANK + NIEMA"), "unknown symbol NIEMA"),
            (lambda d: d["derived"][0].update(formula="ZYCIE_KL + LV_nikt"), "unknown symbol LV_nikt"),
        ]
        for change, message in cases:
            with self.subTest(message=message):
                document = mini_levels()
                change(document)
                rejects(self, document, message)

    def test_choice_count_cannot_depend_on_variables(self):
        document = mini_levels()
        choice(document, "mag_sztuczki")["count"] = "KOMORKI"  # KOMORKI is by MAGIA, a variable fed by choices
        rejects(self, document, "count .* early")

    def test_nesting_depth_is_limited(self):
        document = mini_levels()
        nested = {"id": "o", "label": "O", "grants": []}
        current = nested
        for depth in range(5):  # 4 nested choices are allowed; the 5th still fits the 32-level JSON bound
            inner = {"id": f"o{depth}", "label": "O", "grants": []}
            current["grants"].append({"choice": {"id": f"c{depth}", "label": "C", "kind": "options", "options": [inner]}})
            current = inner
        axis(document, "pochodzenie")["options"].append(nested)
        rejects(self, document, "nested too deep")

    def test_levels_need_level_symbol_only_when_declared(self):
        document = mini_levels()
        document.pop("level")
        axis(document, "klasa")["repeatable"] = False
        for o in axis(document, "klasa")["options"]:
            o.pop("requires_multi")
        rejects(self, document, "unknown symbol")  # LV_woj / LEVEL no longer exist


WOJ3_MAG2 = {"klasa": [{"option": "woj", "levels": 3}, {"option": "mag", "levels": 2}], "pochodzenie": [{"option": "rybak"}]}
MAG5 = {"klasa": [{"option": "mag", "levels": 5}], "pochodzenie": [{"option": "rybak"}]}
WOJ4 = {"klasa": [{"option": "woj", "levels": 4}], "pochodzenie": [{"option": "uczony"}]}
BASE = {"SIL": 15, "ZRE": 8, "WIE": 14}
CHOICES_WOJ3_MAG2 = {"rybak_cechy": {"values": {"SIL": 2, "ZRE": 1}}, "woj_umiej": {"picks": ["walka"]},
                     "woj_styl": {"picks": ["tarcza"]}, "mag_sztuczki": {"picks": ["iskra", "swiatlo"]},
                     "mag_zaklecia": {"picks": ["tarcza_m", "pocisk", "sen"]}}


def resolve(level=5, axes=WOJ3_MAG2, choices=None, stats=BASE, document=None):
    pack = rulesets.load(document or mini_levels())
    return rulesets.resolve(pack, level, axes, choices or {}, stats)


class ResolveTests(SimpleTestCase):
    def test_multiclass_opens_choices_by_class_level(self):
        r = resolve()
        self.assertEqual(r["symbols"]["LV_woj"], 3)
        self.assertEqual(r["symbols"]["LV_mag"], 2)
        self.assertEqual(r["symbols"]["BIEG"], 3)
        self.assertIn("woj_styl", r["open"])
        self.assertNotIn("woj_dar_4", r["open"])
        self.assertEqual(r["open"]["mag_sztuczki"]["count"], 2)
        self.assertEqual(r["open"]["mag_zaklecia"]["count"], 3)
        self.assertEqual(sorted(r["open"]["mag_zaklecia"]["available"]), ["pocisk", "sen", "tarcza_m"])
        self.assertEqual(r["grants"]["ranks"]["odp_sil"], 1)  # the fighter path came first
        self.assertEqual(r["grants"]["ranks"]["skradanie"], 1)
        self.assertEqual(r["grants"]["items"], ["siec"])

    def test_complete_build_has_no_problems(self):
        r = resolve(choices=CHOICES_WOJ3_MAG2)
        self.assertEqual(r["problems"], [])
        self.assertEqual(r["grants"]["stats"], {"SIL": 2, "ZRE": 1})
        self.assertEqual([f["label"] for f in r["grants"]["features"]], ["Mur tarcz"])
        self.assertEqual(r["picked"]["mag_zaklecia"], ["Tarcza maga", "Pocisk", "Sen"])
        self.assertEqual(r["grants"]["ranks"]["walka"], 1)

    def test_missing_choices_are_problems(self):
        problems = resolve()["problems"]
        self.assertTrue(any("Styl walki" in p for p in problems))
        self.assertTrue(any("Zaklęcia" in p for p in problems))

    def test_requires_multi_only_with_two_picks(self):
        low = {"SIL": 15, "ZRE": 14, "WIE": 8}
        self.assertTrue(any("requires" in p for p in resolve(stats=low)["problems"]))
        self.assertFalse(any("requires" in p for p in resolve(stats=low, axes=MAG5)["problems"]))

    def test_repeatable_axis_level_split_is_checked(self):
        for picks in ([{"option": "woj", "levels": 4}],
                      [{"option": "woj", "levels": 0}, {"option": "mag", "levels": 5}],
                      [{"option": "woj", "levels": 3}, {"option": "woj", "levels": 2}],
                      []):
            with self.subTest(picks=picks):
                self.assertTrue(resolve(axes={"klasa": picks, "pochodzenie": [{"option": "rybak"}]})["problems"])
        two_at_one = {"klasa": [{"option": "woj", "levels": 1}, {"option": "mag", "levels": 1}], "pochodzenie": [{"option": "rybak"}]}
        self.assertTrue(any("levels" in p for p in resolve(level=1, axes=two_at_one)["problems"]))

    def test_nested_choice_inside_repeatable_entry(self):
        r = resolve(level=4, axes=WOJ4, choices={"woj_dar_4": {"picks": ["wzmocnienie"]},
                                                 "woj_dar_4/wzmocnienie/wzm": {"values": {"SIL": 2}}})
        self.assertIn("woj_dar_4/wzmocnienie/wzm", r["open"])
        self.assertEqual(r["grants"]["stats"]["SIL"], 2)

    def test_stats_bonus_patterns_and_cap(self):
        def problems(values, stats=BASE):
            return [p for p in resolve(choices={"rybak_cechy": {"values": values}}, stats=stats)["problems"]
                    if "Premie" in p]
        self.assertEqual(problems({"SIL": 2, "ZRE": 1}), [])
        self.assertEqual(problems({"SIL": 1, "ZRE": 1, "WIE": 1}), [])
        self.assertTrue(problems({"SIL": 3}))
        self.assertTrue(problems({"SIL": 2, "ZRE": 2}))
        self.assertTrue(problems({"SIL": 2, "NIC": 1}))
        self.assertTrue(problems({"SIL": 2, "ZRE": 1}, stats={"SIL": 19, "ZRE": 8, "WIE": 14}))  # 21 > cap 20

    def test_same_entry_twice_only_if_repeatable(self):
        document = mini_levels()
        grant(document, "woj", 4)["level"] = 1
        grant(document, "woj", 3)["choice"]["options"][0]["grants"].append(
            {"choice": {"id": "tarcza_dar", "label": "Dar tarczy", "kind": "list", "list": "dary"}})
        axes = {"klasa": [{"option": "woj", "levels": 3}], "pochodzenie": [{"option": "rybak"}]}
        twice = {"woj_styl": {"picks": ["tarcza"]}, "woj_dar_4": {"picks": ["twardy"]},
                 "woj_styl/tarcza/tarcza_dar": {"picks": ["twardy"]}}
        self.assertTrue(any("Twardy" in p for p in resolve(3, axes, twice, document=document)["problems"]))
        twice = {**twice, "woj_dar_4": {"picks": ["wzmocnienie"]}, "woj_styl/tarcza/tarcza_dar": {"picks": ["wzmocnienie"]}}
        self.assertFalse(any("Wzmocnienie" in p for p in resolve(3, axes, twice, document=document)["problems"]))

    def test_stale_picks_are_ignored(self):
        r = resolve(axes={"klasa": [{"option": "woj", "levels": 5}], "pochodzenie": [{"option": "rybak"}]},
                    choices=CHOICES_WOJ3_MAG2)
        self.assertNotIn("mag_zaklecia", r["picked"])
        self.assertNotIn("mag_zaklecia", r["open"])

    def test_when_first_and_later(self):
        mag_first = {"klasa": [{"option": "mag", "levels": 2}, {"option": "woj", "levels": 3}], "pochodzenie": [{"option": "rybak"}]}
        r = resolve(axes=mag_first, stats={"SIL": 15, "ZRE": 8, "WIE": 14})
        self.assertNotIn("odp_sil", r["grants"]["ranks"])
        sources = {var for var, _ in r["grants"]["values"]}
        self.assertEqual(sources, {"ZYCIE_KL", "MAGIA"})
        self.assertEqual(len([v for v, _ in r["grants"]["values"] if v == "ZYCIE_KL"]), 2)

    def test_skill_choice_requires_rank(self):
        document = mini_levels()
        grant(document, "woj", 2)["choice"].update(rank=2, requires_rank=1)
        r = resolve(axes=WOJ4, choices={"woj_umiej": {"picks": ["walka"]}}, document=document)
        self.assertTrue(any("Walka" in p for p in r["problems"]))
        r = resolve(axes={**WOJ4, "pochodzenie": [{"option": "rybak"}]}, choices={"woj_umiej": {"picks": ["skradanie"]}},
                    document=document)
        self.assertFalse(any("Skradanie" in p for p in r["problems"]))
        self.assertEqual(r["grants"]["ranks"]["skradanie"], 2)

    def test_axes_before_stats_do_not_crash(self):
        r = resolve(stats=None, choices=CHOICES_WOJ3_MAG2)
        self.assertFalse(any("requires" in p or "above" in p for p in r["problems"]))
