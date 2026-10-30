---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: tabele-losowe
---

# Schemat: Tabele losowe

Architektura: [[02-architektura-danych]]. Wpisy są liściem (treść), `next` referuje inne tabele.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–60 | Identyfikator |
| `label` | tekst ≤ 120 | Pytanie dla gracza, np. „Kim byli twoi rodzice?” |
| `dice` | tekst, wzorzec `NdK` | Kości, np. `1d10`, `2d6` |
| `choose` | `true` \| `false` | Gracz może zamiast rzutu wybrać wpis (gdy pakiet na to pozwala) |
| `entries` | lista | Wpisy (poniżej); zakresy muszą pokryć wszystkie wyniki kości dokładnie raz |

**Wpis:**

| Klucz | Typ | Opis | Referencja |
|---|---|---|---|
| `range` | `[od, do]` | Zakres wyniku | — |
| `text` | tekst ≤ 500 | Wynik (własnymi słowami) | — |
| `next` | id tabeli \| brak | Tabela rzucana jako następna przy tym wyniku | [[tabele-losowe]] |
| `repeat` | tekst `NdK` \| liczba | Ile razy rzucić `next` (np. liczba przyjaciół) | — |

Przykład:
```json
{"id": "pochodzenie", "label": "Skąd pochodzisz?", "dice": "1d6", "choose": true, "entries": [
  {"range": [1, 3], "text": "Z portowego miasta."},
  {"range": [4, 6], "text": "Z gór; masz wrogów w dolinie.", "next": "wrog", "repeat": 1}]}
```

## Otwarte pytania do dograć
- Ograniczenie łańcuchów `next`: limit głębokości 5 i całkowitej liczby rzutów 30 na krok, żeby
  pakiet nie mógł zapętlić tworzenia.
- Czy wynik może zmieniać liczby (np. +1 do statystyki, +100 eb)? v1: nie — tylko tekst historii.
