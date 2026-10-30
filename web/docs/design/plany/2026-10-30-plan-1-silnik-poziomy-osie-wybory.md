# Plan 1 — silnik: poziomy, osie postaci, wybory, nowe metody cech

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Silnik pakietów `narrative-ruleset/1` umie wyrazić tworzenie postaci w systemie z poziomami, wieloma osiami postaci (w tym wielokrotnym wyborem z podziałem poziomu), drzewem wyborów z progami poziomu i trzema metodami cech SRD — bez żadnej nazwy z 5E w kodzie.

**Architecture:** Wszystko addytywne w `rulesets.py` (walidacja pakietu + czyste obliczenia: `resolve` drzewa wyborów, rozszerzony `sheet`) i `characters.py` (nowe akcje → nowe zdarzenia szkicu; stan nadal wyliczany z logu). Pakiety bez nowych kluczy liczą się identycznie jak dziś. Poziom szkicu jest zdarzeniem `level_set` (ADR 012: awans dopisze kolejne zdarzenia). Bez migracji, bez UI, bez MCP — to kolejne plany.

**Tech Stack:** Python 3 / Django + DRF, `ast` whitelist, testy `django.test` uruchamiane w kontenerze `api`.

**Spec:** `web/docs/design/pakiet-srd-5e.md` (+ słownik i ADR 011/012 w `web/docs/architecture.md`). Istniejący projekt kreatora: `web/docs/design/kreator-postaci/`.

## Global Constraints

- Żadnych nazw, liczb ani reguł 5E w kodzie (anty-cel spec); testy używają wymyślonego mini-systemu „mini-poziomy” (PL).
- Istniejące pakiety (fixture `mini()` z `test_rulesets.py`, kształt pakietu bez poziomów) ładują się i liczą bez zmian — wszystkie obecne testy `stories` przechodzą bez edycji.
- Nowe pojęcia nazywane jak w słowniku: **oś postaci** (`axes`), poziom (`level`), awans później.
- Formuły tylko przez whitelist AST (`rulesets.parse`), nigdy `eval`.
- Rzuty kośćmi wyłącznie na serwerze (`secrets`), wynik w zdarzeniu, przerzut tylko za zgodą MG (`grant_reroll`).
- Negatywne testy dla każdego nowego pojęcia (CLAUDE.md).
- Bramki przed pushem z `HANDOFF.md`; push i wdrożenie tylko na wyraźne polecenie usera.
- Commity tylko wskazanych ścieżek (`git add <pliki>`), nigdy `git add -A` (w drzewie pracują inni agenci).

Polecenie testów (z `web/`): `docker compose run --rm --no-deps -v "$PWD/backend:/app" api python manage.py test stories.<moduł>`

## Review Focus

1. Gracz zmienia klasę/oś po dokonaniu wyborów → wybory spoza aktualnego drzewa są ignorowane (nie 500), lista problemów mówi, czego brakuje. Test: Task 3 `test_stale_picks_are_ignored`.
2. Zniekształcone komendy (`picks` nie-lista, nieznany `key`, wartości nie-int, bool zamiast int, 10 000 elementów) → `DomainError` 400, nigdy 500. Test: Task 5 `test_malformed_commands_are_rejected`.
3. Pakiet, w którym liczba wyborów zależy od wartości wyliczanej z wyborów (cykl) → odrzucony przy wgraniu. Test: Task 2 `test_choice_count_cannot_depend_on_variables`.
4. Granice poziomu: suma poziomów osi wielokrotnej ≠ poziom, pick z `levels: 0`, poziom = max, dwie klasy na 1. poziomie → problem, nie crash. Test: Task 3 `test_repeatable_axis_level_split_is_checked`.
5. Regresja: pakiety bez nowych kluczy dają identyczne arkusze i przepływ. Test: istniejące `test_rulesets`/`test_characters` + Task 4 `test_pack_without_levels_sheet_unchanged`.

---

### Task 1: Formuły z porównaniami i kości „zachowaj najwyższe”

**Files:**
- Modify: `web/backend/stories/rulesets.py` (`parse`, `evaluate`, nowa `dice_keep`)
- Test: `web/backend/stories/test_rulesets_levels.py` (nowy)

**Interfaces:**
- Produces: formuły przyjmują `>`, `>=`, `<`, `<=`, `==`, `!=` (wynik `1`/`0`) i `and`/`or` (wynik `1`/`0`); `rulesets.dice_keep(value: str, label: str) -> tuple[int, int, int]` = `(count, sides, keep)` dla `"4d6kh3"` (`keep == count`, gdy brak `kh`). `rulesets.dice` bez zmian — nadal odrzuca `kh`.

- [ ] **Step 1: Write the failing tests** (`class FormulaComparisonTests(SimpleTestCase)` w nowym pliku)

```python
def test_comparisons_and_boolean_ops_yield_1_or_0(self):
    tree, names = rulesets.parse("SIL >= 13 and WIE >= 13", "x")
    self.assertEqual(names, {"SIL", "WIE"})
    self.assertEqual(rulesets.evaluate(tree, {"SIL": 13, "WIE": 14}), 1)
    self.assertEqual(rulesets.evaluate(tree, {"SIL": 12, "WIE": 14}), 0)
    tree, _ = rulesets.parse("SIL < 3 or SIL == 9", "x")
    self.assertEqual(rulesets.evaluate(tree, {"SIL": 9}), 1)

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
```

Łańcuch `1 < SIL < 3` jest dozwolony (każdy operand sprawdzany).

- [ ] **Step 2: Run** `… test stories.test_rulesets_levels` → FAIL (`unsupported expression`, `dice_keep` missing).
- [ ] **Step 3: Implement.** In `parse.check`: accept `ast.Compare` (ops in a `COMPARISONS` dict, all comparators checked) and `ast.BoolOp` with `And`/`Or`. In `evaluate`: Compare → `int(all(pairwise))`, BoolOp → `int(all/any(bool(v)))`. `dice_keep` uses its own regex `([1-9]\d?)d([1-9]\d{0,2})(?:kh([1-9]\d?))?`, `1 <= keep <= count`.
- [ ] **Step 4: Run** the module and `stories.test_rulesets` → PASS.
- [ ] **Step 5: Commit** `git add web/backend/stories/rulesets.py web/backend/stories/test_rulesets_levels.py && git commit -m "Let ruleset formulas compare values and dice keep the highest"`

---

### Task 2: Walidacja pakietu — poziom, zmienne, postęp, osie, listy, wybory, nowe kroki

**Files:**
- Modify: `web/backend/stories/rulesets.py` (`load`, stałe `STEPS`, `RESERVED`)
- Test: `web/backend/stories/test_rulesets_levels.py` (fixture `mini_levels()` + `class LevelsLoadTests`)
- Docs: `web/docs/design/kreator-postaci/schemas/` — nowe `poziom-i-postep.md`, `osie.md`, `listy-i-wybory.md`; aktualizacja `pakiet-zasad.md` (nowe klucze), `metody.md` (nowe kroki), `umiejetnosci.md` (`skill_value`, rangi), `statystyki.md` (koszty punktów); mapa zależności w `02-architektura-danych.md`. Format jak istniejące schematy (frontmatter, tabela kluczy, przykład z mini-systemu, „Otwarte pytania do dograć”).

**Interfaces:**
- Consumes: Task 1 (`parse` z porównaniami, `dice_keep`).
- Produces — nowe, opcjonalne klucze pakietu (kształt wiążący dla kolejnych tasków i planów):

```text
level:        {"label": str≤40, "min": int 0..100, "max": int min..100}
variables:    [{"id": SYMBOL, "label": str≤80 (opcjonalnie)}]           # suma wkładów z nadań, domyślnie 0
progressions: [{"id": SYMBOL, "by": "LEVEL"|"LV_<opcja>"|<variable>, "values": [int]×1..100}]
              # wartość = values[min(by, len)-1], 0 gdy by < 1
skill_value:  formula; symbole RANK (ranga), STAT (wartość powiązanej cechy) + jak w derived
axes:  [{"id": str1..40, "label": str≤80, "repeatable": bool=false, "options": [OPTION]×1..}]
lists: [{"id": str1..40, "label": str≤80, "entries": [ENTRY]×1..}]
OPTION: {"id", "label", "summary"≤500?, "ref"≤200?, "requires_multi": formula?(tylko stats), "grants": [GRANT]}
ENTRY:  OPTION + {"rank": int 0..20 = 0, "tags": [str≤40]≤20, "repeatable": bool=false}
GRANT:  {"level": int≥1 = 1, "when": "any"|"first"|"later" = "any",
         "values": {variable: formula}, "stats": {stat: int -10..10}, "skills": {skill: rank},
         "items": [catalog id], "features": [{"id","label","summary"?,"ref"?}], "choice": CHOICE}
CHOICE: {"id": str1..60 unikalne w pakiecie, "label", "kind": "options"|"list"|"skills"|"stats_bonus",
         "count": formula = "1",
         options:     "options": [OPTION]
         list:        "list": list id, "tags_any": [str]?, "rank_min": formula?, "rank_max": formula?
         skills:      "among": [skill id]? (domyślnie wszystkie), "rank": int≥1, "requires_rank": int = 0
         stats_bonus: "among": [stat id]|"all", "patterns": [[int≥1]]×1..5, "cap": int}
nowe kroki metod: {"type":"axes","axes":[axis id]}, {"type":"choices"},
  {"type":"stats_array","values":[int]×len(stats)}, {"type":"stats_roll_assign","dice":"4d6kh3"},
  stats_points + "costs": {"<wartość>": int≥0} pokrywające każdą wartość min..max kroku
```

Symbole: `LEVEL` (gdy pakiet ma `level`), `LV_<opcja>` dla opcji osi `repeatable` (id opcji musi pasować do `SYMBOL`), zmienne, postępy. **Wczesne** symbole = `LEVEL`, `LV_*`, postępy z `by` ∈ {`LEVEL`, `LV_*`}; `count`, `rank_min`, `rank_max` mogą używać tylko wczesnych. Wartości w `values` nadań: cechy + wczesne. `requires_multi`: tylko cechy. `derived`/`pools`/`skill_value`: wszystko powyżej + zmienne + wszystkie postępy. `LEVEL` i prefiks `LV_` zarezerwowane dla id cech/pochodnych/pul/zmiennych/postępów. Oś `repeatable` wymaga `level`. Zagnieżdżenie wyborów ≤ 5. Limit wpisów: `axes` ≤ 10, `lists` ≤ 50, `variables` ≤ 30, `progressions` ≤ 100, wszystkie opcje/wpisy razem ≤ 3000.

- [ ] **Step 1: Write the fixture and failing tests.** `mini_levels()` — wymyślony system „Mini-poziomy”: cechy `SIL`, `ZRE`, `WIE` (3..20); `level` „Krąg” 1..5; zmienne `ZYCIE_KL`, `MAGIA`; postępy `BIEG` (by `LEVEL`, `[2,2,2,3,3]`), `SZTUCZKI_MAG` (by `LV_mag`, `[2,2,3,3,4]`), `KOMORKI` (by `MAGIA`, `[2,3,4,5,6]`); umiejętności `walka`(SIL), `skradanie`(ZRE), `wiedza`(WIE), `odp_sil`(SIL, kategoria „Odporność”), wszystkie `max: 2`; `skill_value: "floor((STAT - 10) / 2) + RANK * BIEG"`; pochodne `ZYCIE: "ZYCIE_KL + floor((SIL - 10) / 2) * LEVEL"`, `KP: "10 + floor((ZRE - 10) / 2)"`, `KOMORKI_TERAZ: "KOMORKI"`; katalog `siec`; oś `klasa` „Ścieżka” (`repeatable`) z opcjami:
  - `woj` (`requires_multi: "SIL >= 13"`): `values ZYCIE_KL "10 + (LV_woj - 1) * 6"` `when first`; `"LV_woj * 6"` `when later`; `skills {odp_sil: 1}` `when first`; wybór `woj_umiej` (skills, among `walka`,`skradanie`, rank 1); od `level 3` wybór `woj_styl` (options: `tarcza` → feature „Mur tarcz”; `luk` → `stats {ZRE: 1}`); od `level 4` wybór `woj_dar_4` (list `dary`);
  - `mag` (`requires_multi: "WIE >= 13"`): `ZYCIE_KL` `"6 + (LV_mag - 1) * 4"` first / `"LV_mag * 4"` later; `values MAGIA "LV_mag"`; wybór `mag_sztuczki` (list `zaklecia`, `tags_any ["mag"]`, `rank_max "0"`, `count "SZTUCZKI_MAG"`); wybór `mag_zaklecia` (list `zaklecia`, tags `mag`, `rank_min "1"`, `rank_max "1 + floor((LV_mag - 1) / 2)"`, `count "LV_mag + 1"`);
  - oś `pochodzenie` (zwykła): `rybak` → `skills {skradanie: 1}`, `items ["siec"]`, wybór `rybak_cechy` (stats_bonus among SIL/ZRE/WIE, patterns `[[2,1],[1,1,1]]`, cap 20); `uczony` → `skills {wiedza: 1}`, wybór `uczony_cechy` (jak wyżej).
  - listy: `zaklecia`: `iskra`, `swiatlo` (rank 0, tags mag), `tarcza_m`, `pocisk`, `sen` (rank 1, mag), `kula` (rank 2, mag), z `summary` i `ref: "zasady/<id>"`; `dary`: `wzmocnienie` (`repeatable`, wybór `wzm` stats_bonus all, `[[2],[1,1]]`, cap 20), `twardy` (`values ZYCIE_KL "2 * LEVEL"`).
  - metody: `zestaw` [axes klasa+pochodzenie, `stats_array [15,14,8]`, choices, name]; `punkty` [axes, `stats_points pool 10 min 8 max 15 costs {8:0,9:1,10:2,11:3,12:4,13:5,14:7,15:9}`, choices, name]; `rzut` [axes, `stats_roll_assign 4d6kh3`, choices, name].

```python
class LevelsLoadTests(SimpleTestCase):
    def test_valid_levels_pack_loads(self):
        pack = rulesets.load(mini_levels())
        self.assertEqual(pack["level"], {"label": "Krąg", "min": 1, "max": 5})

    def test_invalid_shapes_reject_whole_pack(self):
        cases = [
            (lambda d: d.pop("level"), "repeatable axis needs level"),
            (lambda d: d["stats"].append({"id": "LEVEL", "label": "x", "min": 1, "max": 2}), "reserved"),
            (lambda d: d["variables"].append({"id": "LV_x"}), "reserved"),
            (lambda d: d["progressions"][0].update(by="ZRE"), "progression .* by"),
            (lambda d: d["progressions"][0].update(values=[]), "progression"),
            (lambda d: axis(d, "klasa")["options"][0].update(id="woj-x"), "symbol"),
            (lambda d: axis(d, "klasa")["options"][0].update(requires_multi="LEVEL >= 1"), "unknown symbol"),
            (lambda d: grant(d, "woj", 0).update(level=0), "level"),
            (lambda d: grant(d, "woj", 0).update(when="sometimes"), "when"),
            (lambda d: grant(d, "woj", 0)["values"].update(NIEMA="1"), "unknown variable"),
            (lambda d: choice(d, "woj_umiej").update(kind="magic"), "kind"),
            (lambda d: choice(d, "woj_umiej").update(among=["latanie"]), "unknown skill"),
            (lambda d: choice(d, "mag_sztuczki").update(list="brak"), "unknown list"),
            (lambda d: choice(d, "rybak_cechy").update(patterns=[[0]]), "pattern"),
            (lambda d: choice(d, "uczony_cechy").update(id="rybak_cechy"), "duplicate choice"),
            (lambda d: d["methods"][0]["steps"][1].update(values=[15, 14]), "every stat"),
            (lambda d: d["methods"][1]["steps"][1]["costs"].pop("12"), "cost for every value"),
            (lambda d: d["methods"][2]["steps"][1].update(dice="4d6kh9"), "dice"),
            (lambda d: d["methods"][0]["steps"][0].update(axes=["rasa"]), "unknown axis"),
            (lambda d: d.update(skill_value="RANK + NIEMA"), "unknown symbol"),
        ]
        for change, message in cases:
            with self.subTest(message=message):
                document = mini_levels(); change(document); rejects(self, document, message)

    def test_choice_count_cannot_depend_on_variables(self):
        document = mini_levels()
        choice(document, "mag_sztuczki")["count"] = "KOMORKI"   # KOMORKI is by MAGIA (a variable)
        rejects(self, document, "count .* early")

    def test_nesting_depth_is_limited(self): ...   # 6 levels of options → "nested too deep"
```

(`axis`, `grant`, `choice` — małe helpery w pliku testów wyszukujące po id; `rejects` z `test_rulesets`.)

- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** w `load`: osobne funkcje walidujące `option(...)`, `grant(...)`, `choice(...)` (rekurencja z głębokością), zbiory symboli wczesnych/późnych przekazywane jawnie; `order(formulas, known)` dostaje `known` rozszerzone o `LEVEL`, `LV_*`, zmienne i postępy; formuły nadań/liczności/rang trzymane jako drzewa w `pack["_trees"]` pod kluczami ścieżek (np. `"choice:mag_sztuczki:count"`) albo w prywatnym `pack["_choices"]` (indeks id → spec) — wybór należy do wykonawcy, byle `resolve` (Task 3) miał je bez ponownego parsowania.
- [ ] **Step 4: Run** `stories.test_rulesets_levels stories.test_rulesets stories.test_characters` → PASS (regresja).
- [ ] **Step 5: Docs** — schematy jak w „Files”; przykład w każdym z mini-poziomów.
- [ ] **Step 6: Commit** (`rulesets.py`, test, pliki schematów).

---

### Task 3: Rozwiązywanie drzewa wyborów — `rulesets.resolve`

**Files:**
- Modify: `web/backend/stories/rulesets.py`
- Test: `web/backend/stories/test_rulesets_levels.py` (`class ResolveTests`)
- Docs: `kreator-postaci/schemas/listy-i-wybory.md` — sekcja „Jak liczone” (klucze instancji wyborów, ignorowanie nieaktualnych)

**Interfaces:**
- Consumes: znormalizowany pakiet z Task 2.
- Produces:

```text
resolve(pack, level: int|None, axes: dict[axis_id, list[{"option": id, "levels": int}]],
        choices: dict[key, {"picks": [id]} | {"values": {stat: int}}], stats: dict[stat, int]) -> dict:
  # stats = cechy bazowe (przed premiami); requires_multi sprawdzane na bazowych + premiach z nadań
  "symbols":  {"LEVEL": int, "LV_<opcja>": int, <wczesne postępy>: int}
  "open":     {key: {"id", "label", "kind", "count": int, "available": [id] | None, "source": str}}
  "grants":   {"stats": {stat: int}, "ranks": {skill: int}, "items": [id], "features": [{"id","label","summary","ref","source"}],
               "values": [(variable, tree)]}
  "picked":   {key: [label]}          # do karty
  "problems": [str]
```

Klucz instancji wyboru: `<choice id>` na najwyższym poziomie; wewnątrz wybranej opcji/wpisu `<klucz rodzica>/<id opcji>/<choice id>`. Poziom osi dla progu `level` nadania: `LV_<opcja>` dla osi `repeatable`, inaczej `LEVEL`. `when: first` = pierwszy pick osi wielokrotnej, `later` = kolejne. Rangi umiejętności łączone przez `max`. Wybory z `state`, których klucza nie ma w `open`, są ignorowane.

Problemy (komunikaty po angielsku jak reszta modułu): brak picku osi; suma `levels` ≠ `LEVEL`; pick z `levels < 1`; powtórzona opcja; `requires_multi` niespełnione dla dowolnego picku przy ≥2 pickach; liczba picków ≠ `count`; pick spoza `available`; ten sam wpis listy wybrany drugi raz gdziekolwiek (chyba że `repeatable`); `skills` z `requires_rank` na umiejętności bez tej rangi; `stats_bonus` niezgodny z żadnym wzorcem albo wynik > `cap`.

- [ ] **Step 1: Failing tests** — na `mini_levels()`, poziom 5:

```python
def test_multiclass_opens_choices_by_class_level(self):
    r = resolve(axes={"klasa": [{"option": "woj", "levels": 3}, {"option": "mag", "levels": 2}],
                      "pochodzenie": [{"option": "rybak", "levels": 5}]})
    self.assertEqual(r["symbols"]["LV_woj"], 3); self.assertEqual(r["symbols"]["BIEG"], 3)
    self.assertIn("woj_styl", r["open"]); self.assertNotIn("woj_dar_4", r["open"])
    self.assertEqual(r["open"]["mag_sztuczki"]["count"], 2)
    self.assertEqual(r["open"]["mag_zaklecia"]["count"], 3)
    self.assertEqual(sorted(r["open"]["mag_zaklecia"]["available"]), ["pocisk", "sen", "tarcza_m"])  # kula: krąg 2
    self.assertEqual(r["grants"]["ranks"]["odp_sil"], 1)   # woj jest pierwszy

def test_requires_multi_only_with_two_picks(self):
    low = {"SIL": 15, "ZRE": 14, "WIE": 8}
    self.assertTrue(any("requires" in p for p in resolve(level=5, stats=low, axes=WOJ3_MAG2)["problems"]))
    self.assertFalse(any("requires" in p for p in resolve(level=5, stats=low, axes=MAG5)["problems"]))

def test_repeatable_axis_level_split_is_checked(self):
    for picks in ([{"option": "woj", "levels": 4}], [{"option": "woj", "levels": 0}, {"option": "mag", "levels": 5}],
                  [{"option": "woj", "levels": 3}, {"option": "woj", "levels": 2}]):
        with self.subTest(picks=picks):
            self.assertTrue(resolve(level=5, axes={"klasa": picks, ...})["problems"])

def test_nested_choice_inside_repeatable_entry(self):   # woj 4 → dar wzmocnienie → wzm stats_bonus
    r = resolve(level=4, axes=WOJ4, choices={"woj_dar_4": {"picks": ["wzmocnienie"]},
                                             "woj_dar_4/wzmocnienie/wzm": {"values": {"SIL": 2}}})
    self.assertEqual(r["grants"]["stats"]["SIL"], 2)

def test_stats_bonus_patterns_and_cap(self): ...  # {"SIL":2,"ZRE":1} ok; {"SIL":3} problem; SIL 19 + 2 → cap problem
def test_same_entry_twice_only_if_repeatable(self): ...
def test_stale_picks_are_ignored(self):   # wybory maga zostają w state po zmianie na woj 5 → brak wyjątku, brak w "picked"
def test_when_first_and_later(self):   # mag pierwszy → ZYCIE_KL wkład "6 + ..."; odp_sil brak
```

- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** `resolve` (czysta funkcja, bez Django). **Step 4: Run** → PASS. **Step 5: Commit.**

---

### Task 4: Arkusz z poziomem — `rulesets.sheet(..., resolution=None)`

**Files:**
- Modify: `web/backend/stories/rulesets.py` (`sheet`)
- Test: `web/backend/stories/test_rulesets_levels.py` (`class LevelSheetTests`)

**Interfaces:**
- Consumes: wynik `resolve` (Task 3).
- Produces: `sheet(pack, stats, skills=None, items=(), losses=None, resolution=None)`; gdy `resolution` podane: cechy = bazowe + `grants.stats` (problem, gdy > `max` cechy); zmienne = suma wkładów; późne postępy; `values` dla pochodnych/pul zawiera symbole, zmienne i postępy; gdy pakiet ma `skill_value`, umiejętności liczone ze wzoru dla każdej umiejętności (ranga 0 też). Nowe klucze wyniku: `"level"`, `"variables"`, `"progressions"`, `"skill_values"` ({skill: int}), `"features"`, `"choices"` (= `resolution["picked"]`), `"base_stats"`; `"stats"` = cechy końcowe. Bez `resolution` wynik identyczny jak dziś (te same klucze).

- [ ] **Step 1: Failing tests** — wartości policzone ręcznie z definicji mini-poziomów:

```python
def test_multiclass_sheet_hand_computed(self):
    # Krąg 5: woj 3 + mag 2; zestaw: SIL 15, WIE 14, ZRE 8; rybak: +2 SIL, +1 ZRE; woj_umiej: walka
    s = sheet_for(level=5, base={"SIL": 15, "ZRE": 8, "WIE": 14}, axes=WOJ3_MAG2_RYBAK,
                  choices={"rybak_cechy": {"values": {"SIL": 2, "ZRE": 1}}, "woj_umiej": {"picks": ["walka"]}, ...})
    self.assertEqual(s["stats"], {"SIL": 17, "ZRE": 9, "WIE": 14})
    self.assertEqual(s["variables"], {"ZYCIE_KL": 30, "MAGIA": 2})      # 10 + 2*6 + 2*4
    self.assertEqual(s["derived"], {"ZYCIE": 45, "KP": 9, "KOMORKI_TERAZ": 3})
    self.assertEqual(s["skill_values"]["walka"], 6)        # 3 + 1*3
    self.assertEqual(s["skill_values"]["skradanie"], 2)    # -1 + 1*3
    self.assertEqual(s["skill_values"]["wiedza"], 2)       # 2 + 0
    self.assertEqual(s["skill_values"]["odp_sil"], 6)

def test_feat_adds_variable_per_level(self): ...   # woj 4 + dar twardy → ZYCIE_KL += 8
def test_stat_above_max_is_a_problem(self): ...
def test_pack_without_levels_sheet_unchanged(self):
    pack = rulesets.load(mini())
    self.assertEqual(set(rulesets.sheet(pack, {"SIL": 5, "WOL": 5}, {}, [], None)),
                     {"stats", "derived", "pools", "skills", "items", "price", "modifiers", "problems", "warnings"})
```

- [ ] **Step 2–4:** run FAIL → implement → run PASS (+ `stories.test_rulesets`).
- [ ] **Step 5: Commit.**

---

### Task 5: Szkic postaci — poziom, osie, wybory, zestaw/rzut/koszty cech

**Files:**
- Modify: `web/backend/stories/characters.py` (`PLAYER_ACTIONS`, `fold`, `compute`, `handle`, `create_draft`, `approve`, `markdown`, `roll_log`)
- Test: `web/backend/stories/test_characters_levels.py` (nowy; REST jak `test_characters.py`, fixture `mini_levels()`)
- Docs: `kreator-postaci/schemas/szkic-postaci.md` (nowe zdarzenia), `kreator-postaci/02-architektura-danych.md` („Wyjście do reszty systemu”: poziom, zdolności, wybory na karcie)

**Interfaces:**
- Consumes: Task 1–4.
- Produces — akcje (REST/MCP bez zmian w routingu: `POST characters/<id>/` z `action`) i zdarzenia:

| Akcja | Dane | Zdarzenie | Uwagi |
|---|---|---|---|
| (tworzenie) | `level` (tylko MG; gracz → `level.min` pakietu, kampanijny poziom startowy dojdzie w planie 2) | `level_set {level}` jako pierwsze | tylko pakiet z `level`; poza zakresem → 400 |
| `set_axis` | `axis`, `picks: [{"option", "levels"?}]` | `axis_set {axis, picks}` | krok `axes` z tą osią; dla zwykłej osi `levels` pomijane |
| `choose` | `key` + `picks: [id]` albo `values: {stat: int}` | `choice_set {key, picks|values}` | `key` musi być w `open` aktualnego `resolve`; ≤ 200 picków |
| `assign_stats` | `values: {stat: int}` | `stats_assigned {values}` | multizbiór = `stats_array.values` albo ostatni rzut |
| `roll_stat_set` | — | `stat_set_rolled {dice, rolls: [{"dice": [int], "kept": [int], "result": int}], values: [int]}` | jeden wynik na cechę; ponownie tylko po `grant_reroll target=stats` |
| `set_stats` | jak dziś | jak dziś | z `costs`: suma kosztów == `pool` |

`compute`: gdy metoda ma krok `axes` → `resolve` + `sheet(..., resolution=...)`; problemy z obu; karta (`markdown`) dodaje linię poziomu i osi („**Krąg 5** · Ścieżka: Wojownik 3 / Mag 2 · Pochodzenie: Rybak”), sekcje `features` i każdego wyboru (nagłówek = `label` wyboru), umiejętności z `skill_values` (gdy są); nagłówki sekcji z opcjonalnego `pack["sheet_labels"]` (`skills`, `items`, `history`, `features`) z dotychczasowymi tekstami PL/EN jako domyślnymi. `approve` dopisuje do wartości aktora `LEVEL` i zmienne. `roll_log` loguje `stat_set_rolled`.

- [ ] **Step 1: Failing tests** (`class LevelCharacterFlowTests(TestCase)`, setUp jak w `test_characters.py`, pakiet `mini_levels()`):

```python
def test_gm_sets_level_player_gets_minimum(self):
    self.assertEqual(self.start(self.player)["state"]["level"], 1)
    gm = self.start(self.gm, level=5); self.assertEqual(gm["state"]["level"], 5)
    self.start_fails(self.gm, level=6)

def test_multiclass_array_build_submit_approve(self):
    d = self.start(self.gm, level=5, method="zestaw")
    d = self.act(self.gm, d, action="set_axis", axis="klasa", picks=[{"option": "woj", "levels": 3}, {"option": "mag", "levels": 2}])
    d = self.act(self.gm, d, action="set_axis", axis="pochodzenie", picks=[{"option": "rybak"}])
    d = self.act(self.gm, d, action="assign_stats", values={"SIL": 15, "ZRE": 8, "WIE": 14})
    for key, data in CHOICES_WOJ3_MAG2.items(): d = self.act(self.gm, d, action="choose", key=key, **data)
    d = self.act(self.gm, d, action="set_name", name="Ilsa", look="")
    d = self.act(self.gm, d, action="submit")
    self.assertEqual(d["status"], "approved")
    self.assertEqual(d["sheet"]["derived"]["ZYCIE"], 45)
    page = WikiPage.objects.get(pk=d["page"])
    for text in ("Krąg 5", "Wojownik 3", "Mag 2", "Mur tarcz", "Pocisk"):   # CHOICES_WOJ3_MAG2: woj_styl=tarcza, mag_zaklecia ⊃ pocisk
        self.assertIn(text, page.body)

def test_array_assignment_must_match_values(self): ...      # {"SIL":15,"ZRE":15,"WIE":8} → 400
def test_point_costs(self): ...                               # 15/8/8 = 9 → "Spend exactly 10"; 13/12/10 = 5+4+2=11 → problem; 14/10/8=7+2+0=9 ...
def test_roll_keep_is_server_side_once(self):
    with patch("stories.characters.secrets.randbelow", side_effect=[5, 0, 3, 2] * 3):   # 6,1,4,3 → 13 ×3
        d = self.act(self.player, d, action="roll_stat_set")
    self.assertEqual(d["state"]["stat_set"], [13, 13, 13])
    self.act(self.player, d, status=409, action="roll_stat_set")
def test_choose_rejects_closed_keys(self): ...               # woj_dar_4 przy woj 3 → 400
def test_malformed_commands_are_rejected(self):
    for command in [dict(action="set_axis", axis="klasa", picks="woj"), dict(action="set_axis", axis="klasa", picks=[{"option": "woj", "levels": True}]),
                    dict(action="choose", key=None, picks=[]), dict(action="choose", key="woj_umiej", picks=["walka"] * 10_000),
                    dict(action="choose", key="rybak_cechy", values={"SIL": "2"}), dict(action="assign_stats", values=[15, 14, 8])]:
        with self.subTest(command=command): self.act(self.player, d, status=400, **command)
def test_existing_flow_unchanged(self): ...                  # pakiet mini() przechodzi jak w test_characters (smoke)
```

Pole stanu: `state["level"]`, `state["axes"]`, `state["choices"]`, `state["stat_set"]` (lista wyrzuconych wartości) — `view` zwraca je jak resztę stanu.

- [ ] **Step 2: Run** `stories.test_characters_levels` → FAIL.
- [ ] **Step 3: Implement** w `characters.py` wg tabeli; `roll_keep(count, sides, keep) -> tuple[list[int], list[int], int]` obok `roll`.
- [ ] **Step 4: Run** cały `stories` (141 + nowe) → PASS.
- [ ] **Step 5: Commit.**

---

### Task 6: Bramki i zamknięcie planu

- [ ] `makemigrations --check --dry-run` → „No changes detected” (plan nie zmienia modeli).
- [ ] `python3 -I scripts/check-release.py` → „Publication checks passed”.
- [ ] Integracje MCP w kontenerze (`tests/wiki_integration.py`, `tests/integration.py`) → bez zmian, PASS.
- [ ] 11 zestawów przeglądarkowych z CI (pętla z `.github/workflows/*.yml`, reset `throttle_login_*`) → PASS (`characters.py` jest na ścieżce UI kreatora).
- [ ] `HANDOFF.md` / `SUMMARY.md` (lokalne) — stan planu 1, nowe gotcha; OpenProject: komentarz + `move … zrobione` dla zadania planu 1.
- [ ] Push — tylko po poleceniu usera.

---

## Mapa kolejnych planów (każdy osobnym dokumentem, pisany po zamknięciu poprzedniego)

| Plan | Zakres | Daje działające |
|---|---|---|
| 2 | Ustawienia kampanii: poziom startowy (CRUD MG, `simple_history`), tryb PW (stała/rzut serwera); pakiety wbudowane (lista z repo, wybór po ID, wersje + „dostępna nowa wersja”), strony zasad (kopiowanie ze stałymi ID, odesłania `ref` → link w kreatorze/karcie, usuwanie z potwierdzeniem); flaga instancji; atrybucja CC-BY z danych | MG wybiera pakiet wbudowany i poziom (na razie mini) |
| 3 | Kreator UI dla nowych kroków (osie, wybory z wyszukiwarką/filtrami, zestaw/rzut/koszty, licznik, poziom) + panel MG; zrzuty mobile 844 px | gracz tworzy postać mini-poziomy w przeglądarce |
| 4 | Generator SRD + dane źródłowe EN; **wycinek** (3 klasy: bez czarów, pełna, półczarująca; 2 gatunki; 2 tła; ~10 czarów; 2 strony zasad) + postacie wzorcowe (poz. 1, 3, 4, 5, 20 + 2 wieloklasowe) jako testy | pierwsze postacie 5E |
| 5 | Pełny pakiet EN + strony zasad EN | komplet SRD EN |
| 6 | Słownik PL + tłumaczenie podsumowań i stron zasad PL | komplet SRD PL |
| 7 | MCP agenta MG: opis formatu, instrukcja PDF→pakiet, walidacja wszystkich błędów naraz, wgrywanie dużego pakietu, pakiety wbudowane, poziom | droga „kolejne zasady z PDF” |
| 8 | Awans (ADR 012): przyznanie przez MG (postać/drużyna), kroki awansu, zatwierdzenie, cofnięcie ostatniego, PW stała/rzut | awans postaci |
| 9 | Dokumentacja OSS (README EN/PL, wiki guides, CHANGELOG, THIRD_PARTY_NOTICES), wdrożenie prod z backupem po „wdrażaj <commit>”, test usera, włączenie flagi | wydanie |
