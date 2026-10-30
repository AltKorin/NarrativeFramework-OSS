---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: statystyki
---

# Schemat: Statystyki

Architektura: [[02-architektura-danych]]. Liść zależności — niczego nie referencuje.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–20, `[A-Za-z0-9_]` | Identyfikator używany we wzorach, np. `SIL` |
| `label` | tekst ≤ 80 | Nazwa dla gracza, np. „Siła” |
| `short` | tekst ≤ 10 | Skrót na karcie (domyślnie `id`) |
| `min` | liczba całkowita | Najniższa wartość przy tworzeniu |
| `max` | liczba całkowita ≥ `min` | Najwyższa wartość przy tworzeniu |
| `description` | tekst ≤ 500 | Krótki opis w kroku przydziału (własnymi słowami, nie cytat podręcznika) |

Przykład:
```json
{"id": "SIL", "label": "Siła", "min": 2, "max": 8, "description": "Dźwiganie, walka wręcz."}
```

## Otwarte pytania do dograć
- Czy potrzebne są statystyki nieprzydzielane przy tworzeniu (np. ustalane później przez grę)?
  Propozycja: flaga `creation: false`, wtedy wartość startowa `min`.
