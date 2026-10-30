---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: osie
---

# Schemat: Osie postaci, opcje i nadania

Architektura: [[02-architektura-danych]]. Referuje [[statystyki]], [[umiejetnosci]], [[katalog]],
[[poziom-i-postep]], [[listy-i-wybory]]. Pojęcie ze słownika: *character axis* (`web/docs/architecture.md`).

Oś = nazwana lista opcji, z której wybiera każda postać (np. „Ścieżka”, „Pochodzenie”). Istniejące
[[role]] zostają bez zmian dla starszych pakietów.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–60 | Identyfikator |
| `label` | tekst ≤ 80 | Nazwa osi dla gracza |
| `repeatable` | bool, domyślnie `false` | Wiele opcji z podziałem poziomu (suma = `LEVEL`); wymaga `level`; id opcji muszą być symbolami |
| `options` | lista ≥ 1 opcji | Patrz niżej |

## Opcja (także wpis listy i opcja wyboru)

| Klucz | Typ | Opis |
|---|---|---|
| `id`, `label` | tekst ≤ 60 / ≤ 120 | — |
| `summary` | tekst ≤ 500 | Krótki opis własnymi słowami |
| `ref` | tekst ≤ 200 | Odesłanie do strony zasad z pełnym tekstem |
| `requires_multi` | wzór na statystykach | Tylko oś `repeatable`: sprawdzany dla każdej wybranej opcji, gdy wybrano ≥ 2 |
| `grants` | lista nadań | Co daje opcja |

## Nadanie (`grants[]`)

| Klucz | Typ | Opis |
|---|---|---|
| `level` | ≥ 1, domyślnie 1 | Od jakiego poziomu opcji (oś `repeatable`: `LV_<opcja>`, inaczej `LEVEL`) |
| `when` | `any` \| `first` \| `later` | Oś `repeatable`: tylko pierwsza / tylko kolejne wybrane opcje |
| `values` | {zmienna: wzór} | Wkład do zmiennej; wzór na statystykach i symbolach wczesnych |
| `stats` | {statystyka: −10..10} | Stała premia |
| `skills` | {umiejętność: ranga} | Ranga (łączona maksimum ze wszystkich źródeł) |
| `items` | [id katalogu] | Przedmioty |
| `features` | [{`id`, `label`, `summary`?, `ref`?}] | Zdolności wypisywane na karcie |
| `choice` | wybór | Patrz [[listy-i-wybory]] |

Zagnieżdżenie wyborów ≤ 4 (każde dodaje 5 poziomów JSON; limit dokumentu to 32). Opcji i wpisów razem ≤ 3000.

Przykład (mini-poziomy, skrócony):
```json
{"id": "klasa", "label": "Ścieżka", "repeatable": true, "options": [
  {"id": "woj", "label": "Wojownik", "requires_multi": "SIL >= 13", "grants": [
    {"when": "first", "values": {"ZYCIE_KL": "10 + (LV_woj - 1) * 6"}, "skills": {"odp_sil": 1}},
    {"when": "later", "values": {"ZYCIE_KL": "LV_woj * 6"}},
    {"level": 3, "choice": {"id": "woj_styl", "label": "Styl walki", "kind": "options", "options": [...]}}]}]}
```

## Otwarte pytania do dograć
- Wymagania pojedynczej opcji (nie tylko przy wielu)? v1: tylko `requires_multi`.
