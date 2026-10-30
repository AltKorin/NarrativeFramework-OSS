---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: katalog
---

# Schemat: Katalog (sprzęt i wszczepy)

Architektura: [[02-architektura-danych]]. Referuje [[pule]], [[statystyki]], [[umiejetnosci]],
[[wartosci-pochodne]] i inne wpisy katalogu. Jedna lista dla broni, pancerzy, ekwipunku, ubrań i
wszczepów — silnik nie rozróżnia kategorii poza tym, co wynika z pól.

| Klucz | Typ | Opis | Referencja |
|---|---|---|---|
| `id` | tekst 1–60 | Identyfikator | — |
| `label` | tekst ≤ 120 | Nazwa | — |
| `category` | tekst ≤ 40 | Grupa w sklepie, np. „Broń”, „Wszczepy: neuralne” | — |
| `price` | liczba całkowita ≥ 0 | Cena w walucie budżetu | — |
| `description` | tekst ≤ 500 | Własnymi słowami: co daje, ewentualnie dane gry (obrażenia, SP) | — |
| `costs` | `{poolId: {dice, fixed}}` | Koszt w pulach: `dice` przy rzucie (`2d6`, `1d6/2` = połowa w dół), `fixed` przy stałej wartości | [[pule]] |
| `slots` | liczba ≥ 0 | Ile slotów opcji udostępnia (wszczep bazowy) | — |
| `requires` | lista grup id | Wymaga zainstalowanego **jednego z** wpisów w każdej grupie, np. `[["cyberramie-l", "cyberramie-p"]]` | [[katalog]] |
| `uses_slot_of` | lista id | Zajmuje 1 slot w jednym z tych wpisów (silnik przydziela automatycznie, jeśli jest wolny) | [[katalog]] |
| `slot_cost` | liczba ≥ 1 | Ile slotów zajmuje (domyślnie 1; duże opcje zajmują 2–3) | — |
| `paired` | `true` \| `false` | Kupowany parami: liczba sztuk parzysta, para w dwóch różnych egzemplarzach bazy (np. dwoje oczu) | — |
| `max` | liczba ≥ 1 | Ile sztuk może mieć postać (domyślnie 1 dla wpisów z `costs`, bez limitu dla reszty) | — |
| `modifiers` | lista `{target, value}` | Stałe bonusy pokazywane na karcie (statystyka, umiejętność albo pochodna) | [[statystyki]], [[umiejetnosci]], [[wartosci-pochodne]] |

Przykład (wymyślony system):
```json
{"id": "oko-sokola", "label": "Oko sokoła", "category": "Wszczepy: oczy", "price": 500,
 "costs": {"DUSZA": {"dice": "1d6", "fixed": 3}},
 "requires": [["oko-bazowe"]], "uses_slot_of": ["oko-bazowe"],
 "modifiers": [{"target": "spostrzegawczosc", "value": 1}]}
```

## Walidacja przy tworzeniu postaci
- Suma cen ≤ budżet kroku `shop` (patrz [[metody]]).
- Każda grupa w `requires` spełniona; każdy `uses_slot_of` ma wolny slot; liczba sztuk ≤ `max`.
- Usunięcie wpisu bazowego usuwa też zależne od niego opcje (z komunikatem, co zniknie).
- Koszty w pulach liczone wg trybu kroku (`fixed` albo `roll` — rzut wykonuje serwer).

## Zweryfikowane na pakiecie testowym (2026-10-30)
Mały pakiet z prawdziwego systemu wykazał trzy brakujące pola, dodane do silnika: dzielnik kości
(`1d6/2`), `slot_cost` i `paired`. Ograniczenie v1: sloty liczone łącznie na typ bazy, nie na
egzemplarz (opcja nie jest „przypięta” do konkretnego oka).

## Otwarte pytania do dograć
- Czy bonusy z `modifiers` mają się kumulować z kilku sztuk? Propozycja: nie, chyba że wpis ma `stack: true`.
- Warianty (np. „Pistolet ciężki — dobra jakość”) jako osobne wpisy czy parametr? v1: osobne wpisy.
- Wpisy „do wyboru jednego z” w pakietach roli — opisane w [[role]] (`gear_package`), nie tutaj.
