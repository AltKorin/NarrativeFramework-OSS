---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: wartosci-pochodne
---

# Schemat: Wartości pochodne

Architektura: [[02-architektura-danych]]. Referuje [[statystyki]] i wcześniejsze pochodne.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–30 | Identyfikator, np. `HP` |
| `label` | tekst ≤ 80 | Nazwa na karcie |
| `formula` | tekst ≤ 200 | Wyrażenie (język poniżej) |

## Język wzorów

Liczby całkowite i ułamkowe, `+ - * /`, nawiasy, identyfikatory statystyk, pul (`HUM`, `HUM_MAX` —
patrz [[pule]]) i **wcześniej zdefiniowanych** pochodnych, funkcje `ceil`, `floor`, `round`, `min`, `max`.
Wynik zaokrąglany w dół do liczby całkowitej, chyba że wzór sam zaokrągla.

Wzór jest parsowany do drzewa z białą listą węzłów — nigdy nie jest wykonywany jako kod
(bez `eval`). Nieznany identyfikator, funkcja albo cykl → błąd importu pakietu.

Przykład:
```json
{"id": "ZYCIE", "label": "Punkty życia", "formula": "10 + 5 * ceil((SIL + WOL) / 2)"},
{"id": "RANNY", "label": "Ciężko ranny poniżej", "formula": "ceil(ZYCIE / 2)"}
```

## Otwarte pytania do dograć
- Czy pochodne mogą zależeć od umiejętności lub roli (np. bonus z roli)? v1: tylko statystyki
  i pochodne; zależności od roli przez modyfikatory roli (patrz [[role]]).
