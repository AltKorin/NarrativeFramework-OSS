---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: umiejetnosci
---

# Schemat: Umiejętności

Architektura: [[02-architektura-danych]]. Referuje [[statystyki]].

| Klucz | Typ | Opis | Referencja |
|---|---|---|---|
| `id` | tekst 1–40 | Identyfikator | — |
| `label` | tekst ≤ 80 | Nazwa dla gracza | — |
| `stat` | id | Statystyka bazowa (do podglądu „stat + umiejętność”) | [[statystyki]] |
| `category` | tekst ≤ 40 | Grupa w UI, np. „Walka”, „Społeczne” | — |
| `cost` | liczba całkowita ≥ 1 | Koszt jednego poziomu przy kupowaniu z puli (umiejętności trudne = 2) | — |
| `basic_min` | liczba całkowita ≥ 0 | Minimalny poziom wymagany od każdej postaci (0 = brak wymogu) | — |
| `variants` | `true` \| `false` | Umiejętność z dookreśleniem wpisywanym przez gracza (np. język, specjalizacja) | — |
| `max` | liczba całkowita | Najwyższy poziom przy tworzeniu | — |

W pakiecie z `skill_value` (np. `"floor((STAT - 10) / 2) + RANK * BIEG"`) poziom umiejętności to **ranga**
(0 brak, 1 biegłość, 2 ekspertyza…) nadawana przez [[osie]] i [[listy-i-wybory]], a karta pokazuje wartość
ze wzoru (`STAT` = wartość powiązanej statystyki).

Przykład:
```json
{"id": "szermierka", "label": "Szermierka", "stat": "ZRE", "category": "Walka",
 "cost": 2, "basic_min": 0, "max": 6}
```

## Otwarte pytania do dograć
- `variants`: czy każdy wariant to osobny wpis na karcie z osobnym poziomem? Propozycja: tak,
  limit 5 wariantów na umiejętność.
- Czy `max` powinien móc zależeć od metody? Propozycja: metoda może nadpisać limit (patrz [[metody]]).
