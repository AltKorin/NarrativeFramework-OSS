---
tags: [schema, design, narrative-framework, kreator-postaci]
status: skeleton
domain: role
---

# Schemat: Role

Architektura: [[02-architektura-danych]]. Referuje [[statystyki]], [[umiejetnosci]], [[tabele-losowe]], [[katalog]].
Szkielet — pola ustalone, kształt pakietów do potwierdzenia na pierwszym pakiecie testowym.

| Klucz | Typ | Opis | Referencja |
|---|---|---|---|
| `id` | tekst 1–40 | Identyfikator | — |
| `label` | tekst ≤ 80 | Nazwa roli | — |
| `description` | tekst ≤ 1000 | Krótki opis (własnymi słowami) | — |
| `ability` | `{label, rank}` | Zdolność specjalna roli i jej startowy poziom | — |
| `stat_rows` | lista `{statId: wartość}` | Wiersze do metody półlosowej: rzut wybiera wiersz | [[statystyki]] |
| `skill_package` | `{skillId: poziom}` | Gotowy pakiet umiejętności roli | [[umiejetnosci]] |
| `templates` | lista `{label, stats, skills, items}` | Gotowe postaci do metody szablonowej | [[statystyki]], [[umiejetnosci]] |
| `lifepath` | lista id tabel | Dodatkowe tabele historii dla tej roli | [[tabele-losowe]] |
| `gear_package` | lista grup `{choose, items}` | Sprzęt i wszczepy roli: z każdej grupy gracz bierze `choose` wpisów z `items` | [[katalog]] |

## Otwarte pytania do dograć
- Czy `stat_rows` jest wybierany rzutem (`1dN`, N = liczba wierszy) czy osobną tabelą losową?
  Propozycja: rzut `1dN` liczony przez silnik.
- Czy pakiet umiejętności może zawierać umiejętności z `variants` (np. język pochodzenia)?
  Propozycja: tak, z wariantem do wpisania przez gracza.
- Czy szablon (`templates`) ma własny `gear_package`, czy dziedziczy z roli? Propozycja: własny, pełna lista bez wyboru.
