---
tags: [schema, design, narrative-framework, kreator-postaci]
status: draft
domain: pakiet-zasad
---

# Schemat: Pakiet zasad

Architektura: [[02-architektura-danych]]. Koperta zawierająca wszystkie domeny jednego systemu.

| Klucz | Typ | Opis | Referencja |
|---|---|---|---|
| `schema` | stała `narrative-ruleset/1` | Wersja formatu | — |
| `id` | tekst 1–80, `[a-z0-9-]` | Identyfikator systemu, np. `moj-system` | — |
| `version` | liczba całkowita ≥ 1 | Wersja pakietu; szkic pamięta, na której powstał | — |
| `title` | tekst ≤ 200 | Nazwa widoczna dla MG i graczy | — |
| `language` | `pl` \| `en` | Język etykiet | — |
| `source` | tekst ≤ 1000 | Skąd pochodzą zasady (podręcznik, strony) — tylko dla MG | — |
| `stats` | lista | [[statystyki]] | — |
| `skills` | lista | [[umiejetnosci]] | `stats` |
| `derived` | lista | [[wartosci-pochodne]] | `stats` |
| `pools` | lista | [[pule]] | `stats` |
| `catalog` | lista | [[katalog]] | `pools`, `stats`, `skills`, `derived`, `catalog` |
| `tables` | lista | [[tabele-losowe]] | `tables` |
| `roles` | lista | [[role]] | `stats`, `skills`, `tables`, `catalog` |
| `methods` | lista ≥ 1 | [[metody]] | wszystkie powyższe |

**Limity v1:** dokument ≤ 450 KB, ≤ 30 statystyk, ≤ 200 umiejętności, ≤ 30 pochodnych,
≤ 100 tabel po ≤ 100 wpisów, ≤ 10 pul, ≤ 1000 wpisów katalogu, ≤ 30 ról, ≤ 10 metod.

Przykład (wymyślony mini-system):
```json
{"schema": "narrative-ruleset/1", "id": "mini", "version": 1, "title": "Mini", "language": "pl",
 "stats": [...], "skills": [...], "derived": [...], "tables": [...], "roles": [...], "methods": [...]}
```

## Otwarte pytania do dograć
- Czy `id` pakietu musi być globalnie unikalny, czy tylko w kampanii? v1: w kampanii.
- Czy dopuszczamy dziedziczenie pakietów (rozszerzenie podstawowego o dodatek)? v1: nie — jeden plik.
