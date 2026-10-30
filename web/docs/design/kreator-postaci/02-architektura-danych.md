---
tags: [architecture, design, narrative-framework, kreator-postaci]
status: draft
aliases: ["Architektura danych kreatora"]
---

# Kreator postaci — architektura danych

Koncepcja: [[01-koncepcja]].

## Domeny

| Domena | Jedno zdanie | Schemat |
|---|---|---|
| Pakiet zasad | Koperta: identyfikator, wersja, język i spis pozostałych domen jednego systemu RPG | [[pakiet-zasad]] |
| Statystyki | Lista cech postaci z zakresem wartości | [[statystyki]] |
| Umiejętności | Lista umiejętności z powiązaną statystyką, kosztem i limitami | [[umiejetnosci]] |
| Wartości pochodne | Wzory liczone ze statystyk (np. punkty życia) | [[wartosci-pochodne]] |
| Pule | Wartości z maksimum ze wzoru, zmniejszane przez wszczepy (np. Humanity) | [[pule]] |
| Katalog | Sprzęt, broń, pancerze, ubrania i wszczepy: cena, koszt w pulach, sloty, wymagania, bonusy | [[katalog]] |
| Tabele losowe | Tabele „rzuć kośćmi → wynik”, opcjonalnie wskazujące kolejną tabelę | [[tabele-losowe]] |
| Role | Archetypy postaci: zdolność specjalna, pakiety umiejętności, tabele statystyk, szablony | [[role]] |
| Metody tworzenia | Sekwencja kroków (wybór roli, statystyki, umiejętności, historia, budżet) | [[metody]] |
| Szkic postaci | Zapis zdarzeń jednego gracza tworzącego postać w kampanii + status zatwierdzenia | [[szkic-postaci]] |

## Mapa zależności

```
Szkic postaci ──► Pakiet zasad (id + wersja) ──► Metody tworzenia
Metody tworzenia ──► Role, Statystyki, Umiejętności, Tabele losowe, Katalog
Role ──────────────► Statystyki, Umiejętności, Tabele losowe, Katalog
Katalog ───────────► Pule, Statystyki, Umiejętności, Wartości pochodne, Katalog (wymagania, sloty)
Umiejętności ──────► Statystyki
Wartości pochodne ─► Statystyki, Pule, wcześniejsze Wartości pochodne
Pule ──────────────► Statystyki, Wartości pochodne (wzór maksimum)
Tabele losowe ─────► Tabele losowe (next — bez cykli)
Statystyki ────────► (nic — liść)
```

- **Liście (zmieniane najczęściej, niczego nie referencują):** Statystyki, wpisy Tabel losowych
  (treść historii), etykiety tekstowe. Tłumaczenie lub poprawka balansu nie wymaga zmian wyżej.
- **Referencje wyłącznie przez `id`** wewnątrz pakietu. Silnik waliduje przy imporcie, że każde
  `id` istnieje — pakiet z wiszącą referencją jest odrzucany w całości.
- **Szkic referuje pakiet przez `id` + `wersję`.** Podmiana pakietu w kampanii nie zmienia
  istniejących szkiców; szkic utworzony na starej wersji można dokończyć albo MG odrzuca go z uwagą.

## Wyjście do reszty systemu

Zatwierdzenie szkicu wywołuje istniejące serwisy (bez nowych ścieżek zapisu):
1. `wiki.create_page(kind="pc")` — karta w Markdown wygenerowana ze stanu szkicu
   (statystyki, pochodne, umiejętności, historia); notatki MG = log rzutów.
2. `campaign_runtime.command(set_value, scope="actors")` — statystyki, pule i pochodne jako atrybuty aktora.
   Sprzęt i wszczepy trafiają na kartę (Markdown) z ceną, kosztem w pulach i slotami.

## Przechowywanie

- Pakiet zasad: nowa tabela `CampaignRuleset` (kampania, `document` JSON, wersja, odcisk).
  Jeden aktywny pakiet na kampanię w v1.
- Szkic: tabela `CharacterDraft` (kampania, członkostwo gracza, wersja pakietu, status,
  `events` JSON append-only, wersja optymistyczna).
- Obie tabele addytywne (nowa migracja), bez zmian w istniejących — zgodnie z blue/green.

## Decyzje (potwierdzone 2026-10-30)
1. Jeden aktywny pakiet zasad na kampanię.
2. Gracz ma do 3 aktywnych szkiców (np. postać zapasowa).
3. Ponowny rzut tylko za zgodą MG (zdarzenie `reroll_granted`).
4. MG nie edytuje szkicu gracza — odsyła z uwagą; po zatwierdzeniu edytuje kartę w wiki.
5. MG tworzy NPC i pregeny tym samym kreatorem; szkic bez gracza, zatwierdzany od razu.
6. Sprzęt i wszczepy z katalogu, z pulami (Humanity), slotami i wymaganiami — od pierwszej wersji.

## Otwarte pytania do dograć
- Czy pakiet zasad ma być publikowalny graczom jako czytelna strona zasad? Na razie nie —
  gracz widzi tylko etykiety i opisy potrzebne w danym kroku.
- Pule a cykl: maksimum puli może zależeć od pochodnej, a pochodna od puli — import odrzuca cykle,
  kolejność wyliczania z sortowania topologicznego.
