---
tags: [design, narrative-framework, kreator-postaci, srd]
status: draft
aliases: ["Pakiet SRD 5.2.1", "5E SRD ruleset"]
---

# Wbudowany pakiet zasad 5E (SRD 5.2.1) i droga „PDF → pakiet przez MCP”

Rozszerza [[kreator-postaci/01-koncepcja|kreator postaci]]. Data: 2026-10-30.

## Po co

Kreator postaci działa dziś na prywatnych pakietach systemów bez poziomów, przygotowanych dla konkretnych kampanii.
Każdy inny MG zaczyna od pustej kampanii bez zasad. Chcemy:

1. **Pierwszy publiczny system wbudowany w OSS**: zasady 5E z SRD 5.2.1 (licencja CC-BY-4.0), po polsku
   i po angielsku, wybierane przez MG jednym kliknięciem albo jednym wywołaniem MCP.
2. **Powtarzalną drogę na kolejne systemy**: MG we własnym Claude czyta PDF podręcznika, składa pakiet
   `narrative-ruleset/1` i wysyła go przez MCP. NF tylko przyjmuje, sprawdza i podpowiada, co poprawić.
   Pakiet SRD jest żywym, publicznym wzorem tej drogi (pakiety prywatne nim być nie mogą).
3. Potem, osobnym projektem: **kampania startowa 5E** do zaimportowania (poza zakresem tego dokumentu).

Narzędzie jest dla MG; to MG zakłada kampanię, wybiera zasady, ustawia poziom startowy i zaprasza graczy.
Gracze tworzą postacie w prowadzonym kreatorze, MG zatwierdza.

## Cele

- MG wybiera w kampanii pakiet „5E SRD 5.2.1” (PL albo EN) i ustawia **poziom startowy 1–20** (bez limitu).
- Gracz tworzy pełną postać zgodną z SRD 5.2.1 na tym poziomie: klasa, podklasa (od 3. poziomu), tło,
  gatunek, języki, cechy (trzy metody SRD), premie z tła, biegłości, atuty, styl walki, mistrzostwo broni,
  zdolności klasowe ze wszystkich poziomów do startowego, sztuczki i czary, ekwipunek startowy, charakter.
- **Wieloklasowość od v1** (decyzja usera): gracz rozdziela poziom startowy na kilka klas wg zasad SRD
  (wymagania cech, ograniczone biegłości z kolejnej klasy, PB z łącznego poziomu, PW per klasa,
  wspólna tabela komórek czarów dla wielu klas czarujących, zdolności każdej klasy do jej poziomu).
- **Tekst zasad w wiki kampanii** (decyzja usera): pozycje w kreatorze mają krótkie podsumowanie
  własnymi słowami i odesłanie do strony wiki z pełnym tekstem (czar, zdolność, atut, gatunek, tło…).
- Karta po zatwierdzeniu pokazuje to, czego gracz potrzebuje przy stole: cechy z modyfikatorami, premia
  z biegłości, rzuty obronne, umiejętności z wyliczoną premią, KP, PW, inicjatywa, szybkość, kości
  wytrzymałości, zdolności (z poziomem i źródłem), czary wg kręgów z komórkami, ekwipunek, monety.
- Silnik zyskuje **ogólne** mechaniki potrzebne 5E i podobnym systemom; w kodzie nie ma nazw z 5E.
- MCP daje agentowi MG: opis formatu, instrukcję przekładania podręcznika na pakiet, sprawdzenie pakietu
  bez wgrywania (z listą wszystkich błędów), wybór pakietu wbudowanego, ustawienie poziomu startowego.

## Nie-cele (v1)

- Wyciąganie zasad z PDF po stronie aplikacji (za ambitne — decyzja usera).
- Kampania startowa 5E (następny projekt).
- Magiczne przedmioty przy starcie na wyższym poziomie (decyzja usera: na razie pomijamy; złoto wg
  tabeli SRD zostaje, przedmioty MG daje w grze).
- ~~Awans postaci po grze~~ — **zmiana (user, 2026-10-30): metoda awansowania musi być zaplanowana od razu**;
  ustalenia w sekcji „Awans postaci”.
- Zmiana przygotowanych czarów w trakcie gry (codzienna) — kreator zapisuje tylko wybór startowy.
- Potwory, zasady walki i bestiariusz jako dane silnika.

## Anty-cele (porażka, nawet jeśli „działa”)

- Nazwy, liczby albo reguły 5E zaszyte w kodzie silnika lub UI (np. `if class == "wizard"`).
- Tekst skopiowany z polskiego wyciągu SRD 5.1 (`5e-SRD_v1.0.pdf`, OGL + „użytek prywatny”) albo
  z polskiego wydania podręcznika. Z polskiego PDF bierzemy **wyłącznie nazwy pojęć** (słownik).
- Pakiet tak duży, że nie da się go wgrać przez MCP, albo pierwsza decyzja w kreatorze wypada poza
  ekran telefonu (844 px — bramka `runtime_browser`).
- Atrybucja niezgodna z warunkami SRD (patrz Licencja).
- Zmiana zachowania istniejących pakietów (pakiety już używane w kampaniach muszą działać bez zmian).

## Licencja i nazewnictwo (decyzje)

- Źródło mechaniki i tekstu: `SRD_CC_v5.2.1.pdf` (CC-BY-4.0). Wymagana formułka atrybucji — dosłownie,
  po angielsku (w pakiecie PL także, z dopiskiem „Tłumaczenie własne”) — w: `THIRD_PARTY_NOTICES.md`,
  polu `source` obu pakietów i w miejscu widocznym dla gracza w kreatorze oraz na karcie postaci.
- SRD zabrania innych wzmianek o Wizards; wolno pisać „compatible with fifth edition” / „5E compatible”.
  Tytuł pakietu: „5E — SRD 5.2.1” / „5E — SRD 5.2.1 (PL)”. Bez „Dungeons & Dragons”/„D&D” w nazwach,
  UI i README. *(Decyzja wykonawcy-projektanta, do potwierdzenia przy czytaniu.)*
- Polski słownik terminów (np. Klasa Pancerza/KP, biegłość, test obronny, Mistrz Podziemi/MP, sztuki
  złota/sz, nazwy cech i umiejętności) z `5e-SRD_v1.0.pdf`; tam, gdzie 5.2.1 wprowadza pojęcia nieobecne
  w 5.1 (gatunek zamiast rasy, mistrzostwo broni, atuty pochodzenia) — własne tłumaczenie, spisane
  w słowniku w repo, żeby kolejne tłumaczenia były spójne.
- Opisy w pakiecie PL tłumaczymy sami z SRD 5.2.1.

## Ograniczenia

- Stack i zasady repo bez zmian (Django + DRF, React, MCP w `stories/mcp_server.py`); bramki przed pushem
  z `HANDOFF.md` (141+ testów, `makemigrations --check`, `check-release.py`, integracje MCP z DOKŁADNĄ listą
  narzędzi, 11 zestawów przeglądarkowych z CI).
- Limity: dokument pakietu dziś ≤ 450 KB (`rulesets.load`), żądanie MCP ≤ 300 KB (`mcp_server.py`).
  Pełne SRD z czarami w dwóch językach prawdopodobnie przekroczy oba — patrz „Rozmiar pakietu”.
- Nowe mechaniki addytywne: istniejące pakiety ładują się i liczą identycznie (test regresji na `mini`
  i na kopii struktury istniejącego pakietu bez treści komercyjnej).
- CLAUDE.md: dane poza kodem (źródła treści jako pliki danych, generator je składa), negatywne testy,
  feature flag dla nowej funkcji widocznej dla MG, pełne CRUD w panelu MG, audit (`simple_history`) dla
  nowych modeli/pól z danymi kampanii.

## Co silnik musi umieć (ogólne pojęcia; nazwy schematów — decyzja wykonawcy)

Każde pojęcie opisane w `web/docs/design/kreator-postaci/schemas/` (Obsidian, frontmatter, „Otwarte
pytania do dograć”), z przykładem z wymyślonego mini-systemu, nie z 5E.

| Pojęcie | Po co (przykład z 5E) | Stan dziś |
|---|---|---|
| **Poziom postaci** — wartość ustawiana przez MG w kampanii, symbol w wzorach (np. `LEVEL`), migawka w szkicu | PB, PW, liczba czarów | brak |
| **Tabele postępu** — wartość per poziom, dostępna we wzorach | PB 2→6, sztuczki znane, komórki czarów, „Rage” | brak |
| **Zmienne ustawiane przez wybory** — wybór (np. klasa) ustawia wartość używaną we wzorach | kość wytrzymałości klasy, cecha czarowania | brak |
| **Wybory „N z listy”** z liczbą zależną od wzoru/poziomu, filtrem (tagi, krąg ≤ X) i bez duplikatów | biegłości klasy, języki, czary, mistrzostwo broni | tylko `gear_package` |
| **Drzewo wyborów z progiem poziomu** — opcja daje: premie, poziomy umiejętności, przedmioty, zdolności (tekst), zmienne i kolejne wybory; dostępna od poziomu N | klasa → zdolności 1..N, podklasa od 3., ASI/atut na 4/8/12/16/19 | brak (`roles` nie wystarczą) |
| **Kilka niezależnych osi pochodzenia** | klasa + tło + gatunek | jedna oś `role` |
| **Oś wybierana wielokrotnie z podziałem poziomu** — suma poziomów = poziom postaci; poziom każdego wyboru jako symbol; wymagania (wzór na cechach); inne nadania przy pierwszym i kolejnym wyborze; zmienne sumowane między wyborami | wieloklasowość: wymagania 13+, „poziom czarującego” = pełne + ½ + ⅓ → tabela komórek | brak |
| **Odesłanie pozycji do strony wiki** — pole w pakiecie (ID/slug strony), w kreatorze link „pełny tekst” | czar → strona czaru w wiki | brak |
| **Zestaw wartości do rozdania** | standardowy zestaw 15/14/13/12/10/8 | brak |
| **Rzut z odrzuceniem kości i rozdaniem wyników** | 4k6 odrzuć najniższą ×6, gracz przypisuje | `stats_role_roll` losuje wiersz roli |
| **Nieliniowy koszt punktów cech** | point buy 27 (14=7, 15=9) | liniowy |
| **Premie do cech z wyborem wzorca i limitem** | tło: +2/+1 albo +1/+1/+1 z trzech cech, max 20 | brak |
| **Wartość umiejętności ze wzoru** (cecha + ranga × PB) i rangi 0/1/2 | biegłość, ekspertyza; rzuty obronne jako druga grupa | umiejętność = liczba |
| **Wpisy referencyjne z metadanymi** filtrowanymi w wyborach | czar: krąg, szkoła, listy klas, czas, zasięg, komponenty, czas trwania | katalog ma cenę/sloty |
| **Karta z sekcjami z pakietu** (nagłówki z danych, nie z kodu) | „Zdolności”, „Czary” | nagłówki pierwszego pakietu zaszyte w `characters.markdown` |

Zasady obliczeń 5E, które pakiet musi dać się wyrazić danymi (test akceptacyjny silnika): modyfikator
`floor((X-10)/2)`; PB z poziomu; PW = pełna kość na 1. poziomie + stała wartość (średnia) za każdy
kolejny + modyfikator KON × poziom (+ premie z gatunku/atutów, np. krasnolud, Tough); KP bez zbroi
10 + ZR oraz z pancerza z katalogu (limit ZR dla średnich, tarcza +2); cechy max 20 (ASI), premie
z tła przed rozdaniem limitu; czary: liczba znanych/przygotowanych i najwyższy krąg z tabeli klasy.

**Rzut kości wytrzymałości przy starcie wyżej niż 1:** SRD dopuszcza rzut albo wartość stałą. v1:
wartość stała (bez losowania, bez pytania o przerzut). *(Decyzja wykonawcy-projektanta.)*

## Pakiet SRD 5.2.1 (treść)

- Zakres: 12 klas z podklasą SRD, poziomy 1–20; 4 tła; 9 gatunków z wyborami (np. linie elfów,
  pochodzenie smocze, spuścizna diabelstwa); atuty pochodzenia, ogólne, style walki, epickie dary;
  broń z mistrzostwem, pancerze, narzędzia, ekwipunek; pakiety ekwipunku startowego klas i teł (opcje
  A/B/złoto) z zakupami za złoto; ekwipunek przy starcie na wyższym poziomie wg tabeli SRD (złoto
  wg progu poziomu); języki (wybór albo rzut 1k12); charakter; wszystkie czary SRD z listami klas.
- Trzy metody SRD jako metody pakietu: standardowy zestaw, punkty (27), rzut 4k6.
- Źródła treści jako pliki danych w repo (np. CSV/JSON per domena: klasy, postęp klas, gatunki, tła,
  atuty, czary, ekwipunek) z kolumnami PL i EN; generator składa dwa pakiety (`…-pl`, `…-en`) i waliduje
  je silnikiem. Słownik PL obok danych.
- **Strony wiki z pełnym tekstem zasad** generowane z tych samych danych (SRD CC-BY pozwala na pełny
  tekst z atrybucją; PL = nasze tłumaczenie): przy wyborze pakietu wbudowanego MG dostaje je w kampanii
  jako sekcję „Zasady” opublikowaną graczom, ze stałymi ID (ponowny wybór nie tworzy duplikatów).
  Pakiet trzyma tylko podsumowania + odesłania, więc pozostaje mały. W pakietach z komercyjnych PDF
  (droga MCP) odesłania są opcjonalne, a instrukcja dla agenta zabrania przepisywania chronionego tekstu
  do wiki — tylko własne notatki MG.
- Kolejność treści: najpierw podsumowania (kreator działa), potem pełne strony wiki EN, potem PL.
- Pakiety wbudowane leżą w repo jako artefakt generatora; test sprawdza, że wygenerowany plik zgadza się
  z danymi źródłowymi (brak ręcznych poprawek w wyniku).

### Rozmiar pakietu

Pełny tekst zasad trafia do wiki, nie do pakietu (patrz wyżej) — to główny sposób trzymania rozmiaru.
Mimo to pełne SRD w jednym języku to setki wpisów (ok. 340 czarów, zdolności 12 klas × 20 poziomów). Wymagania:

- Pakiet wbudowany MG wybiera po ID — nie przechodzi przez limit żądania MCP.
- Pakiet z PDF (droga MCP) o rozmiarze porównywalnym z pełnym SRD musi dać się wgrać przez MCP
  (np. wgrywanie w częściach z sumą kontrolną albo podniesiony limit tylko dla tego narzędzia —
  mechanizm wybiera wykonawca; zachować idempotencję i `expected_revision`).
- Limit dokumentu pakietu podniesiony tak, by mieścił pełne SRD w jednym języku z zapasem; nowy limit
  wpisany w schemat `pakiet-zasad.md`.

## Ustawienia kampanii (MG)

- „Zasady”: lista pakietów wbudowanych (na start dwa: 5E SRD PL i EN) obok istniejącego wgrania pliku
  i MCP. Wybór = zwykłe ustawienie pakietu (rewizja, migawka w szkicach w toku — bez zmian).
- **Poziom startowy** (domyślnie 1, zakres z pakietu): dodaj/zmień w panelu MG i przez MCP. Szkic
  zapamiętuje poziom z chwili utworzenia; zmiana poziomu nie przerabia szkiców w toku (MG może je
  odesłać). Pakiety bez pojęcia poziomu nie pokazują tego pola.
- Atrybucja i link do licencji widoczne przy pakiecie wbudowanym.
- Pełne CRUD zachowane: wybierz / zmień / usuń pakiet; ustaw / zmień poziom.

## Kreator (gracz)

- Kroki tylko te, które dotyczą poziomu (postać 1. poziomu nie widzi podklasy).
- Wybory z długich list (czary, ekwipunek): wyszukiwarka, filtr (krąg, szkoła), licznik „wybrano 3/4”,
  krótki opis przy pozycji i podgląd pełnego opisu; działa na telefonie.
- Gracz cały czas widzi: poziom, PB, wyliczone wartości i to, co jeszcze niepoprawne (serwer rozstrzyga).
- Język interfejsu kreatora = język pakietu (jak dziś).

## MCP (agent MG)

Cienkie nakładki na te same serwisy co REST (role, wersje, idempotencja jak dziś):

- opis formatu `narrative-ruleset/1` i **instrukcja przekładania podręcznika na pakiet** (co jest
  mechaniką, co tekstem chronionym; komercyjny pakiet zostaje prywatny w kampanii; tabele czytać
  wzrokowo; „najpierw mini-pakiet z trzema wierszami, potem masowa treść”; licencja/`source`),
  wskazująca generator SRD w repo jako wzór;
- sprawdzenie pakietu bez wgrywania — **wszystkie** błędy naraz, z miejscem w dokumencie;
- lista i wybór pakietów wbudowanych; ustawienie poziomu startowego;
- wgrywanie dużego pakietu (patrz „Rozmiar pakietu”).

Lista narzędzi w `tests/integration.py` jest dokładna — aktualizować razem z kodem.

## Kolejność prac (wymagana)

1. **Silnik + mini-pakiet** wymyślonego systemu z poziomami (≈3 wiersze na każde nowe pojęcie) —
   wyłapuje braki zanim powstanie masowa treść (sprawdzone na istniejącym pakiecie). Testy pozytywne i negatywne każdego
   pojęcia; regresja istniejących pakietów.
2. **Wycinek SRD EN**: 3 klasy (bez czarów, pełna czarująca, półczarująca), 2 gatunki, 2 tła, ~10 czarów,
   2 strony wiki; postacie wzorcowe na poziomach 1, 3, 4, 5, 20 i 2 postacie wieloklasowe (w tym dwie klasy
   czarujące — wspólna tabela komórek) z wartościami policzonymi ręcznie z SRD jako test.
3. Pełny pakiet EN, potem PL (słownik + tłumaczenie podsumowań); strony wiki EN, potem PL.
4. UI MG (wbudowane pakiety, poziom), kreator dla nowych kroków, MCP, dokumentacja (README EN/PL,
   wiki guides, CHANGELOG `Unreleased`, `THIRD_PARTY_NOTICES.md`).
5. Wdrożenie wg `HANDOFF.md` (backup, „wdrażaj <commit>” od usera).

## Zostawione wykonawcy

- Dokładne nazwy i kształt nowych schematów, typów kroków i zdarzeń szkicu.
- Format plików źródłowych treści i struktura katalogów generatora.
- Mechanizm wgrywania dużego pakietu przez MCP i nowe limity.
- Nazwa i domyślny stan feature flagi.
- Układ UI nowych kroków (w granicach: jedno działanie na ekran, mobile 844 px).

## Ustalenia z przesłuchania (2026-10-30, runda 1 — wszystkie przyjęte)

1. **Oś postaci** = ogólne pojęcie listy opcji, z której postać wybiera (system bez poziomów: Rola; 5E: Klasa, Tło, Gatunek);
   nazwy osi z pakietu. „Rola” bez dopowiedzenia jest zarezerwowana dla roli w kampanii (MG/współ-MG/gracz).
   Słownik: `web/docs/architecture.md` → Glossary → Characters and rules.
2. **Strony zasad** kopiowane do kampanii; MG może je edytować (zasady domowe).
3. **Nowa wersja pakietu wbudowanego**: kampania zostaje na swojej; MG widzi „dostępna nowa wersja” i aktualizuje
   jawnie. Nic nie zmienia się po cichu.
4. **Wieloklasowość**: gracz rozdziela poziom startowy między klasy; wymagania SRD twarde (bez furtki MG w kreatorze).
5. **Jeden język na kampanię**: pakiet i strony zasad w jednym języku; zmiana języka = zmiana pakietu.
6. **Czary przygotowane**: kreator zapisuje wybór startowy; codzienne zmiany poza v1.

## Awans postaci

Runda 2 przesłuchania (2026-10-30) — wszystkie przyjęte. Budujemy w tym projekcie, etap zaraz po działającym
tworzeniu postaci 5E; silnik od etapu 1 projektowany tak, by awans był dopisaniem zdarzeń, nie przeróbką.

7. **Awans to ogólne pojęcie silnika**: pakiet opisuje metody awansu tak jak metody tworzenia (kroki). 5E: „kolejny
   poziom”; system bez poziomów (później): wydanie punktów rozwoju na umiejętności. Bez słowa „poziom” zaszytego w kodzie.
8. **Awans przyznaje MG** (jednej postaci albo całej drużynie); NF nie liczy XP w v1.
9. **Pętla awansu = pętla tworzenia**: gracz widzi „możesz awansować”, przechodzi kroki tylko dla nowego awansu
   (przy wieloklasowości wybór klasy z twardymi wymaganiami, zdolności, czary, ASI/atut, PW), wysyła do MG;
   MG zatwierdza albo odsyła z uwagą. Po zatwierdzeniu karta w wiki i aktor w silniku aktualizują się;
   awanse są w logu zdarzeń postaci.
10. **Wymiany tylko według pakietu** (np. jedna sztuczka/czar znany przy awansie); pełna przebudowa wyłącznie
    jako osobne działanie MG „odeślij postać do przebudowy”.
11. **Cofnięcie awansu**: MG cofa ostatni zatwierdzony awans (tylko od ostatniego wstecz), z potwierdzeniem
    i skutkiem („wróci na 5. poziom, znikną: …”).
12. **PW przy awansie**: domyślnie stała średnia; ustawienie kampanii pozwala włączyć rzut serwera (raz, bez
    przerzutu odświeżeniem).

## Self-hosting i wdrażanie (runda 3 — przyjęte)

NF jest **self-hosted**: każda instalacja jest niezależna, bez centralnej usługi NF. Skutki:

- Pakiety wbudowane i tekst stron zasad są **danymi w wydaniu** (repo); instancja nie pobiera niczego z sieci.
- „Dostępna nowa wersja pakietu” pojawia się dopiero po aktualizacji instancji do nowego wydania; zastosowanie do
  kampanii nadal jawnie przez MG (ADR 011).
- Strony zasad kopiowane do kampanii — setki stron na kampanię to akceptowalna skala jednej grupy (ADR 011).
- Po zmianie pakietu strony zasad poprzedniego zostają; MG usuwa je jednym działaniem z potwierdzeniem
  („usunie N stron zasad”).
- **Flaga funkcji** = ustawienie instancji (pakiety wbudowane, poziom startowy, awans). Na `app.narrativeframework.com`
  wyłączona do testu usera na jego kampanii testowej 5E, potem włączona; w wydaniu OSS domyślnie włączona dopiero
  od wydania po tym teście, z opisem w README, jak ją wyłączyć. Pakiety już używane w kampaniach działają bez zmian przez cały czas.
- Postać pozostaje logiem zdarzeń po zatwierdzeniu (ADR 012).
- Atrybucja CC-BY jedzie z danymi (strony zasad, `source`, kreator, karta), żeby każda instancja ją pokazywała
  bez konfiguracji.

## Decyzje projektanta (do sprawdzenia przy czytaniu)

- Tytuł pakietu bez „Dungeons & Dragons”/„D&D”, tylko „5E — SRD 5.2.1” (warunki atrybucji SRD).
- PW przy starcie powyżej 1. poziomu: wartość stała z SRD, bez rzutu.
- Poziom startowy jest ustawieniem kampanii; postacie MG (NPC/pregeny przez MCP) mogą go nadpisać.
- Pakiety wbudowane wybierane po ID (nie przechodzą przez limit żądania MCP).

## Otwarte pytania do dograć

- Odpowiedzi usera po przeglądzie (2026-10-30): pełny tekst → wiki z odesłaniem z kreatora;
  wieloklasowość obowiązkowa w v1; magiczne przedmioty na starcie — na razie pomijamy.
- Rozstrzygnięte w rundzie 3: strony zasad po zmianie pakietu zostają, MG usuwa je jednym działaniem.
