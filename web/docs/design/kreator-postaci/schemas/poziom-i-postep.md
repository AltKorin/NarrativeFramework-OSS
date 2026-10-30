---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: poziom-i-postep
---

# Schemat: Poziom, zmienne i postęp

Architektura: [[02-architektura-danych]]. Opcjonalne — pakiet bez `level` działa jak dotąd.

## `level`

| Klucz | Typ | Opis |
|---|---|---|
| `label` | tekst ≤ 40 | Nazwa poziomu w systemie, np. „Krąg” |
| `min` | 0–100 | Najniższy poziom (domyślny poziom startowy) |
| `max` | `min`–100 | Najwyższy poziom |

Daje we wzorach symbol `LEVEL` (łączny poziom postaci). Oś postaci z `repeatable: true` daje dla każdej
opcji symbol `LV_<id opcji>` (poziom w tej opcji, 0 gdy niewybrana). `LEVEL`, `RANK`, `STAT` i prefiks `LV_`
są zarezerwowane — nie mogą być id statystyk, pochodnych, pul, zmiennych ani postępów.

## `variables`

Lista `{"id": SYMBOL, "label"?}`. Wartość = **suma** wkładów z nadań (`grants[].values`, patrz [[osie]]),
domyślnie 0. Używane w pochodnych, pulach, `skill_value` i jako podstawa postępu.

## `progressions`

| Klucz | Typ | Opis |
|---|---|---|
| `id` | SYMBOL | Symbol we wzorach |
| `by` | `LEVEL` \| `LV_<opcja>` \| id zmiennej | Co indeksuje tabelę |
| `values` | 1–100 liczb | Wartość dla 1, 2, 3…; powyżej końca — ostatnia; `by` < 1 → 0 |

**Symbole wczesne** = `LEVEL`, `LV_*` i postępy z `by` = `LEVEL`/`LV_*`. Tylko one mogą występować w
`count`, `rank_min`, `rank_max` wyborów (liczba wyborów nie może zależeć od wyborów). Postęp po zmiennej
jest „późny” — dostępny w pochodnych i `skill_value`.

Przykład (mini-poziomy):
```json
"level": {"label": "Krąg", "min": 1, "max": 5},
"variables": [{"id": "ZYCIE_KL"}, {"id": "MAGIA"}],
"progressions": [{"id": "BIEG", "by": "LEVEL", "values": [2, 2, 2, 3, 3]},
                 {"id": "KOMORKI", "by": "MAGIA", "values": [2, 3, 4, 5, 6]}]
```

## Wzory — ograniczenia (wszystkie pakiety)

Funkcje: `ceil`, `floor` (1 argument), `round` (1–2), `min`, `max` (2–10). Liczby w wzorach ≤ 10⁹; wynik
działania powyżej 10¹⁵ albo nieskończony jest błędem pakietu (komunikat zamiast awarii).

## Otwarte pytania do dograć
- Postęp indeksowany od `level.min` zamiast od 1? v1: zawsze od 1 (systemy z poziomem 0 dodają wartość na początku).
