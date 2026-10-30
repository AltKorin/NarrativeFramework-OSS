---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: listy-i-wybory
---

# Schemat: Listy i wybory

Architektura: [[02-architektura-danych]]. Referuje [[osie]] (kształt opcji i nadań), [[umiejetnosci]],
[[statystyki]], [[poziom-i-postep]].

## Lista (`lists[]`)

`{"id", "label", "entries": [wpis]}` — wpis = opcja z [[osie]] + `rank` (0–20, np. krąg), `tags` (≤ 20 tekstów,
np. listy klas) i `repeatable` (może być wybrany w kilku wyborach, np. dar wybierany co kilka poziomów).

## Wybór (`grants[].choice`)

| Klucz | Typ | Opis |
|---|---|---|
| `id` | tekst ≤ 60, unikalny w pakiecie | — |
| `label` | tekst ≤ 120 | Nagłówek kroku i sekcji karty |
| `kind` | `options` \| `list` \| `skills` \| `stats_bonus` | Rodzaj |
| `count` | wzór na symbolach wczesnych, domyślnie `"1"` | Ile wybrać |
| `options` | opcje | `kind: options` |
| `list`, `tags_any`, `rank_min`, `rank_max` | id listy, teksty, wzory wczesne | `kind: list` — filtr wpisów |
| `among`, `rank`, `requires_rank` | id umiejętności, ranga, wymagana ranga | `kind: skills` (np. ekspertyza: `rank 2`, `requires_rank 1`) |
| `among`, `patterns`, `cap` | id statystyk albo `"all"`, wzorce premii, limit | `kind: stats_bonus`, np. `[[2,1],[1,1,1]]` |

## Jak liczone

- **Klucz instancji** wyboru: `<id>` dla wyboru z opcji osi; dla wyboru wewnątrz wybranej opcji/wpisu
  `<klucz rodzica>/<id opcji>/<id wyboru>`. Szkic zapisuje wybory po kluczu.
- Wybory, których klucza nie ma w aktualnym drzewie (np. po zmianie opcji osi), są **ignorowane**, nie
  błędne — zostają w logu zdarzeń.
- Problemy: zła liczba picków, pick spoza filtra, ten sam wpis listy drugi raz (o ile nie `repeatable`),
  premia niezgodna z żadnym wzorcem albo ponad `cap`, ranga bez wymaganej.

Przykład:
```json
{"id": "mag_zaklecia", "label": "Zaklęcia", "kind": "list", "list": "zaklecia", "tags_any": ["mag"],
 "rank_min": "1", "rank_max": "1 + floor((LV_mag - 1) / 2)", "count": "LV_mag + 1"}
```

## Otwarte pytania do dograć
- Wybór „zamień jeden wcześniejszy pick” przy awansie — projektowany w planie awansu (ADR 012).
