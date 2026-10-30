---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: pule
---

# Schemat: Pule

Architektura: [[02-architektura-danych]]. Referuje [[statystyki]] i [[wartosci-pochodne]].

Pula to wartość z maksimum wyliczanym ze wzoru, zmniejszana przez koszty wpisów z [[katalog]]
(np. „Humanity”, „Dusza”, „Esencja”). Silnik udostępnia we wzorach dwie zmienne na pulę:
`<id>` — wartość bieżąca (maksimum minus wydane) i `<id>_MAX`.

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst 1–20, `[A-Za-z0-9_]` | Identyfikator we wzorach |
| `label` | tekst ≤ 80 | Nazwa na karcie |
| `max` | wzór | Maksimum, np. z jednej statystyki ([[wartosci-pochodne]] — ten sam język) |
| `floor` | liczba | Najniższa dopuszczalna wartość przy tworzeniu; zejście niżej blokuje wysłanie szkicu |
| `warning` | liczba \| brak | Poniżej tej wartości karta ma ostrzeżenie (nie blokuje wysłania), np. ryzyko utraty kontroli |

Przykład (wymyślony system):
```json
{"id": "DUSZA", "label": "Dusza", "max": "WOL * 10", "floor": 1, "warning": 30}
```
Pochodna zależna od puli, np. obniżenie statystyki po stracie:
```json
{"id": "WOL_AKT", "label": "Wola (aktualna)", "formula": "min(WOL, ceil(DUSZA / 10))"}
```

## Otwarte pytania do dograć
- Czy pula może mieć koszt „zwrotny” (terapia przywraca punkty)? Poza tworzeniem postaci — v1 nie.
- Czy pochodna nadpisująca statystykę (np. aktualna wartość) ma zastępować ją na karcie
  i w aktorze silnika? Propozycja: karta pokazuje obie, aktor dostaje obie pod osobnymi kluczami.
