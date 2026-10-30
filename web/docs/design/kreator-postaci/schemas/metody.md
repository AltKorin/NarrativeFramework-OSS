---
tags: [schema, design, narrative-framework, kreator-postaci]
status: skeleton
domain: metody
---

# Schemat: Metody tworzenia

Architektura: [[02-architektura-danych]]. Referuje wszystkie domeny pakietu.
Szkielet — kolejność i typy kroków ustalone; parametry do potwierdzenia na pakiecie testowym.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–40 | Identyfikator |
| `label` | tekst ≤ 80 | Nazwa metody dla gracza |
| `description` | tekst ≤ 500 | Dla kogo i ile trwa |
| `steps` | lista kroków | Wykonywane w kolejności; gracz może wracać do wcześniejszych |

## Typy kroków

| `type` | Parametry | Co robi |
|---|---|---|
| `role` | — | Gracz wybiera rolę |
| `template` | — | Gracz wybiera szablon roli → ustawia statystyki, umiejętności, sprzęt |
| `stats_points` | `pool`, opcjonalnie `min`/`max` nadpisujące | Przydział puli punktów na statystyki |
| `stats_role_roll` | — | Serwer losuje wiersz `stat_rows` roli |
| `skills_points` | `pool`, `max`, `require_basic` | Kupowanie poziomów z puli wg `cost` |
| `skills_package` | — | Kopiuje `skill_package` roli |
| `tables` | lista id tabel, `include_role_lifepath` | Rzuty (albo wybór, gdy `choose`) po kolei |
| `gear_package` | `loss_mode` (`fixed` \| `roll`) | Wybory z `gear_package` roli; koszty w pulach liczone wg trybu |
| `shop` | `budgets: [{amount, currency, label, categories}]`, `loss_mode`, `keep_change` | Zakupy z [[katalog]] w ramach budżetów (np. sprzęt i osobno ubrania); walidacja slotów, wymagań i `floor` pul; niewydana reszta zostaje jako gotówka, jeśli `keep_change` |
| `name` | — | Imię, ksywka, krótki opis wyglądu (wymagane zawsze, dodawane automatycznie, jeśli brak) |
| `axes` | `axes: [id osi]` | Wybór opcji osi (oś `repeatable`: z podziałem poziomu) — [[osie]] |
| `choices` | — | Wszystkie otwarte wybory z drzewa osi — [[listy-i-wybory]] |
| `stats_array` | `values: [liczby]` (tyle, ile statystyk) | Gracz rozdaje zestaw wartości |
| `stats_roll_assign` | `dice`, np. `4d6kh3` | Serwer rzuca raz na każdą statystykę (zachowaj najwyższe), gracz rozdaje wyniki |

`stats_points` przyjmuje też `costs: {"<wartość>": koszt}` — koszt pokrywający każdą wartość `min`–`max`
kroku (nieliniowy zakup); wtedy suma kosztów musi równać się `pool`.

## Otwarte pytania do dograć
- Czy metoda może mieszać kroki (np. pakiet umiejętności + kilka punktów do rozdania)? Propozycja:
  tak — `skills_package` i `skills_points` w jednej metodzie sumują się.
- Kto jest właścicielem limitów: statystyka/umiejętność czy metoda? Propozycja: domyślnie domena,
  metoda może nadpisać.
- Czy zakupy w `shop` po `gear_package` mogą dokładać wszczepy do slotów z pakietu? Propozycja: tak — sloty liczone łącznie.
- `loss_mode: roll` — rzut przy dodaniu wpisu do koszyka czy przy wysłaniu szkicu? Propozycja: przy wysłaniu (gracz nie przelosowuje przez zdejmowanie i dokładanie).
