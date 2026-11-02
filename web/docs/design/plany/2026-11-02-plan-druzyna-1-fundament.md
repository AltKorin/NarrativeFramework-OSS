# Strona drużyny — plan 1: fundament i kafelki graczy

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gracze kampanii widzą zakładkę „Drużyna”/„Ekipa” z kafelkami: kto kim gra, w czym jest dobry (3 najwyższe
umiejętności + zdanie gracza), a kreator postaci pokazuje nad wyborem roli, co wybrała reszta.

**Architecture:** Nowy moduł `stories/party.py` składa kafelki z istniejących szkiców postaci (`characters.fold` /
`compute`), bez nowych tabel poza przełącznikiem w `Campaign`. Zdanie specjalizacji to zdarzenie `focus_set` w dzienniku
postaci (ADR 012). Całość za flagą instancji `PARTY_PAGE` i przełącznikiem MG `Campaign.party_enabled`.

**Tech Stack:** Django + DRF (`stories`), PostgreSQL, React + TypeScript (Vite), Playwright (testy przeglądarkowe).

**Spec:** `web/docs/design/strona-druzyny.md` (+ słownik „Party” i ADR 013 w `web/docs/architecture.md`).

## Plany całości

| Plan | Zakres | Zależy od |
|---|---|---|
| **1 (ten)** | flaga + przełącznik, zakładka, kafelki, specjalizacja, linijka w kreatorze | — |
| 2 | materiały graczy: CRUD, udostępnianie (prywatne / wybrani / drużyna), obrazki graczy, ukrywanie przez MG, były gracz, przewodnik z informacją z ADR 013 | 1 |
| 3 | tablica: karty (lista), mapa ze wspólnym układem, strzałki, kolory graczy ze Słownika Kolorów, ukrywanie przez MG | 1, 2 (odnośnik do materiału) |
| 4 | „nowe od ostatniej wizyty”, historia dla MG, MCP autora (odczyt), eksport MG/gracza | 2, 3 |

Plany 2–4 rozpisuje się szczegółowo dopiero przy ich starcie, na kodzie po planie 1.

## Global Constraints

- Flaga instancji: `PARTY_PAGE = os.environ.get("PARTY_PAGE", "0") == "1"` w `backend/config/settings.py`; domyślnie wyłączona.
- Przełącznik MG: `Campaign.party_enabled`, domyślnie `True` (`db_default=True`); zmienia tylko właściciel przez `update_campaign`.
- Drużyna = wszyscy członkowie z `role="player"`; MG (właściciel, współprowadzący) nie ma kafelka.
- Napis zakładki: wygląd `cyberpunk` → „Ekipa” / „Crew”, inaczej „Drużyna” / „Party”.
- Ze szkicu inni gracze widzą tylko: imię, status, opcje osi postaci (rola/klasa), 3 umiejętności, zdanie gracza.
  Nigdy statystyk, ekwipunku, historii (tabel), zdarzeń.
- Kafelek: zatwierdzona postać ma pierwszeństwo, inaczej ostatnio zmieniany szkic w toku; pozostałe w zwiniętej liście.
- Specjalizacja: 3 najwyższe umiejętności z rangą > 0 wg wartości (`skill_value`, bez niego ranga), remis → kolejność
  w pakiecie; nazwa progu, gdy pakiet ma `skill_tiers`. Zdanie gracza ≤ 200 znaków.
- Odpowiedzi prywatne (`no-store`, jak reszta API kampanii). Brak dostępu / wyłączona strona → 404.
- Brak nowych zależności. Migracja addytywna.

## Review Focus

- Gracz z kilkoma szkicami, w tym porzuconym nowszym od zatwierdzonej postaci → na wierzchu zatwierdzona (test w Task 2).
- Szkic bez roli/statystyk (świeżo założony) → kafelek z „Bez imienia”, bez umiejętności, bez błędu 500 (test w Task 2).
- Pakiet bez umiejętności (`skills: []`) i pakiet z poziomami (oś zamiast roli) → kafelek działa (test w Task 2).
- `set_focus` na zatwierdzonej postaci przez właściciela działa; przez innego gracza i MG nie (test w Task 1).
- Nowa zakładka w nagłówku kampanii na telefonie (390×844) nie wypycha pierwszej decyzji sesji (`runtime_browser`, Task 5).

---

### Task 1: Zdanie specjalizacji w dzienniku postaci

**Files:**
- Modify: `backend/stories/characters.py` (`fold`, `PLAYER_ACTIONS`, `command`, `handle`)
- Test: `backend/stories/test_characters.py` (nowa klasa `FocusTests`, pomocniki jak w `RulesetUpdateTests`)

**Interfaces:**
- Produces: akcja `set_focus {text: str}` → zdarzenie `{"type": "focus_set", "text": str}`; `fold(events)["focus"]: str`
  (domyślnie `""`).

- [ ] **Step 1: Testy** `test_owner_sets_focus_in_any_status` (draft → `focus` = tekst po `strip`; po
  `status="approved"` ustawionym w bazie drugi `set_focus` też przechodzi, `state["focus"]` = nowy tekst),
  `test_focus_is_the_owners_only` (inny gracz → 404, MG na szkicu gracza → 403),
  `test_focus_limits` (201 znaków → 400; `""` → czyści; nie-string → 400).
- [ ] **Step 2:** Uruchom: `docker compose run --rm -v "$PWD/backend:/app" api python manage.py test stories.test_characters.FocusTests` → FAIL.
- [ ] **Step 3:** `set_focus` jako osobny wyjątek w `command`: dozwolony dla właściciela szkicu (`own`) w każdym statusie
  (także `approved`), MG → 403 „Only the player can describe their character”. Nie zwiększa wymagań `submit`.
  `fold`: `elif kind == "focus_set": state["focus"] = e["text"]`; stan startowy `"focus": ""`.
- [ ] **Step 4:** Test → PASS; cały `stories` → PASS.
- [ ] **Step 5:** Commit „Let players describe their job in the party”.

### Task 2: Kafelki drużyny (backend)

**Files:**
- Create: `backend/stories/party.py`, `backend/stories/test_party.py`, `backend/stories/migrations/0011_campaign_party_enabled.py`
- Modify: `backend/config/settings.py` (flaga), `backend/stories/models.py` (`Campaign.party_enabled`),
  `backend/stories/rulesets.py` (`sheet` → `skill_totals`), `backend/stories/wiki.py` (`campaign_json`, `update_campaign`),
  `backend/stories/wiki_views.py` + `backend/config/urls.py` (`GET api/campaigns/<uuid>/party/`)

**Interfaces:**
- Consumes: `characters.fold`, `characters.compute(pack, draft, state)`, `characters.axis_lines(pack, state)`,
  `campaign_access(user, id, minimum="player")`.
- Produces:
  - `rulesets.sheet(...)["skill_totals"]: dict[str, int]` — dla każdego pakietu (wartość ze `skill_value`, bez niego ranga).
  - `party.overview(user, campaign_id) -> {"players": [Tile]}` gdzie
    `Tile = {"member": str, "account": str, "character": Card | None, "drafts": [Card]}`,
    `Card = {"id": str, "name": str | None, "status": str, "axes": str, "skills": [{"label": str, "value": int, "tier": str | None}], "focus": str}`.
  - `campaign_json(c)["party"]: bool` = `settings.PARTY_PAGE and c.party_enabled`; `update_campaign` przyjmuje opcjonalne
    `party_enabled: bool`.

- [ ] **Step 1: Testy** w `test_party.py` (`@override_settings(PARTY_PAGE=True)`, pakiet `mini()` z `test_rulesets` +
  `skill_value="STAT + RANK"`, `skill_tiers=[{"min": 8, "label": "Wyszkolony"}]`):
  - `test_flag_and_switch_hide_the_page`: bez flagi → 404; z flagą i `party_enabled=False` → 404; outsider → 404.
  - `test_player_sees_every_player_without_private_details`: dwóch graczy ze szkicami; odpowiedź gracza A zawiera kafelek B
    z `name`, `axes`, `skills`, `focus`; w JSON brak kluczy `stats`, `items`, `events`, `tables`.
  - `test_approved_character_wins_and_other_drafts_are_listed`: gracz z zatwierdzoną postacią i nowszym porzuconym
    szkicem → `character.status == "approved"`, `drafts` zawiera porzucony.
  - `test_specialization_is_three_highest_ranked_skills_with_tiers` (4 umiejętności z rangą, jedna z rangą 0 i wysoką
    statystyką → nieobecna; kolejność malejąca; `tier` = „Wyszkolony” od 8, inaczej `None`).
  - `test_fresh_draft_and_skill_less_pack_do_not_break` (szkic bez roli → `name None`, `skills []`; pakiet `skills: []`).
  - `test_gm_toggles_the_switch` (właściciel `PUT` z `party_enabled=False` → `campaign_json["party"] False`; gracz nie zmienia).
- [ ] **Step 2:** Uruchom `... manage.py test stories.test_party` → FAIL.
- [ ] **Step 3:** Implementacja: flaga w `settings.py`; pole + migracja (`BooleanField(default=True, db_default=True)`);
  `skill_totals` w `sheet` z istniejącego `value_of`; `party.overview` (blokada kampanii jak w wiki, gracze posortowani po
  nazwie konta, wybór szkicu wg Global Constraints, `axes` z `axis_lines` albo etykiety roli); widok i URL.
- [ ] **Step 4:** Testy → PASS; `stories` → PASS; `makemigrations --check --dry-run` → „No changes detected”.
- [ ] **Step 5:** Commit „Show the party: who plays whom and what they are good at”.

### Task 3: Zakładka „Drużyna”/„Ekipa” (frontend)

**Files:**
- Create: `frontend/src/Party.tsx` (+ style w istniejącym `character-builder.css` albo nowym `party.css`)
- Modify: `frontend/src/campaignRuntimeTypes.ts` (`CampaignView` + `"party"`), `frontend/src/CampaignWiki.tsx`
  (zakładka, `focusView`, render, przełącznik w formularzu ustawień obok „Wygląd kampanii”)

**Interfaces:**
- Consumes: `GET /api/campaigns/<id>/party/` (Task 2), `POST .../characters/<draft>/ {action: "set_focus", text, version}` (Task 1).
- Produces: `export default function Party({ campaignId, pl, cyber, request, onCreate }: {...})`.

- [ ] **Step 1:** Zakładka widoczna, gdy `campaign.party`; kolejność: gracz `materials, characters, party, gallery`;
  MG `run, timeline, actors, characters, party, gallery, materials`; etykieta wg Global Constraints.
- [ ] **Step 2:** Kafelek: imię (albo „Bez imienia”), status, osie, do 3 umiejętności („Elektronika 14 · Profesjonalista”),
  zdanie gracza. Własny kafelek: pole „W drużynie odpowiadam za…” (`maxLength=200`) + „Zapisz” → `set_focus`.
  Pozostałe szkice: `<details>` „Szkice (N)”, domyślnie zwinięte. Gracz bez postaci: przycisk „Stwórz postać” → `onCreate`
  (przełącza na „Tworzenie postaci”).
- [ ] **Step 3:** Przełącznik „Strona drużyny” (checkbox) w formularzu kampanii właściciela, wysyłany jako `party_enabled`.
- [ ] **Step 4:** `npx tsc --noEmit -p .` → bez błędów.
- [ ] **Step 5:** Commit „Add the party tab for players and the GM”.

### Task 4: Linijka drużyny w kreatorze

**Files:** Modify: `frontend/src/CharacterBuilder.tsx` (krok `role`)

**Interfaces:** Consumes `GET /party/` (Task 2); gdy 404 lub błąd — nic nie pokazuje.

- [ ] **Step 1:** Nad listą ról: „W ekipie/W drużynie: Wojownik (Mira), Kapłan (Tor)” — inne kafelki z `character`
  niebędącym bieżącym szkicem; format `"<axes> (<name>)"`, pominięte kafelki bez osi.
- [ ] **Step 2:** `tsc` → bez błędów. Commit „Show the party's picks above the role choice”.

### Task 5: Test przeglądarkowy, dokumentacja, bramki

**Files:**
- Create: `tests/party_browser.py` (wzór: `tests/onboarding_browser.py`; konta i kampania tworzone przez `manage.py shell`)
- Modify: `.github/workflows/*.yml` (pętla zestawów + `party_browser`), `compose.yaml` jeśli test potrzebuje
  `PARTY_PAGE=1` w stosie CI, `CHANGELOG.md`, `web/README.md` (flaga), przewodnik gracza „Droga gracza” (zakładka)

- [ ] **Step 1:** `party_browser.py`: dwóch graczy, pakiet `mini`, każdy tworzy szkic; gracz A widzi kafelek B z rolą;
  A zapisuje zdanie specjalizacji i B je widzi po odświeżeniu; zwinięta lista szkiców; telefon 390×844 bez poziomego
  przewijania; brak błędów JS.
- [ ] **Step 2:** Bramki z `HANDOFF.md` (stories, migracje, `check-release`, 3 integracje MCP, 11 zestawów + `party_browser`) → wszystkie OK.
- [ ] **Step 3:** Commit „Cover the party page in the browser suite and document the flag”.
