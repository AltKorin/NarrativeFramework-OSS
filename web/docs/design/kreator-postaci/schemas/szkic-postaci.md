---
tags: [schema, design, narrative-framework, kreator-postaci]
status: skeleton
domain: szkic-postaci
---

# Schemat: Szkic postaci

Architektura: [[02-architektura-danych]]. Dane w bazie, nie w pakiecie zasad.

| Pole | Typ | Opis |
|---|---|---|
| `id` | UUID (klienta, bezpieczne ponowienie) | — |
| `campaign` | kampania | — |
| `member` | członkostwo gracza \| brak (szkic MG) | Właściciel szkicu |
| `ruleset_version` | liczba | Wersja pakietu, na której powstał |
| `method` | id metody | [[metody]] |
| `status` | `draft` \| `submitted` \| `approved` \| `returned` | — |
| `events` | lista zdarzeń, tylko dopisywanie | Źródło prawdy (poniżej) |
| `review_note` | tekst ≤ 2000 | Uwaga MG przy odesłaniu |
| `page` | strona wiki \| brak | Karta utworzona po zatwierdzeniu |
| `version` | liczba | Kontrola współbieżności |

## Zdarzenia

`{type, step, at, ...}` — np. `role_chosen {role}`, `stats_set {values}`,
`roll {table, dice, result, entry}`, `skills_set {values}`, `items_set {items}`,
`pool_loss {pool, item, mode, dice, result}`, `name_set {name, look}`,
`submitted`, `returned {note}`, `approved {page}`.

Stan karty = złożenie zdarzeń. Rzut zapisuje serwer (kości z `secrets.randbelow`); klient nie
przesyła wyników rzutów. Ponowny rzut tej samej tabeli w tym samym kroku jest odrzucany —
wycofanie kroku kasuje wybory, ale rzuty zostają w historii widocznej dla MG.

### Pakiety z poziomem i osiami ([[poziom-i-postep]], [[osie]], [[listy-i-wybory]])

| Zdarzenie | Akcja | Uwagi |
|---|---|---|
| `level_set {level}` | tworzenie szkicu | Pierwsze zdarzenie; gracz dostaje `level.min`, MG może podać `level` (NPC, pregen). Kampanijny poziom startowy — kolejny plan. Awans dopisze kolejne zdarzenia (ADR 012). |
| `axis_set {axis, picks}` | `set_axis` | `picks`: `[{option, levels}]` dla osi `repeatable`, `[{option}]` dla zwykłej |
| `choice_set {key, picks \| values}` | `choose` | `key` musi być otwarty w aktualnym drzewie; `values` dla `stats_bonus` |
| `stats_assigned {values}` | `assign_stats` | Rozdanie `stats_array` albo ostatniego rzutu |
| `stat_set_rolled {dice, rolls, values}` | `roll_stat_set` | Jeden rzut (np. `4d6kh3`) na statystykę; kasuje rozdanie; ponownie tylko po `reroll_granted stats` |
| `ruleset_updated {from, to}` | `update_ruleset` (MG) | Szkic przechodzi z migawki pakietu na aktualny pakiet kampanii; wybory liczą się od nowa według nowych zasad, braki wracają do kroków. Nie dla `approved`; gdy pakiet już aktualny — nic się nie dzieje. Odmowa, gdy nowy pakiet nie ma metody szkicu. API dla MG zwraca `ruleset_outdated` |

Stan szkicu zwracany przez API ma dodatkowo `level`, `axes`, `choices`, `stat_set`, a wynik — `open`
(otwarte wybory z liczbą i dostępnymi pozycjami) dla interfejsu kreatora.

## Otwarte pytania do dograć
- Czy wycofanie kroku z rzutami pozwala rzucić ponownie? Propozycja: tylko gdy MG zezwoli
  (zdarzenie `reroll_granted`) — inaczej gracz losowałby do skutku.
- Edycja po `returned`: gracz poprawia wybory, rzuty zostają. Po `approved` szkic jest tylko do odczytu.
- Czy MG może edytować szkic gracza? v1: nie — odsyła z uwagą; po zatwierdzeniu edytuje kartę w wiki.
