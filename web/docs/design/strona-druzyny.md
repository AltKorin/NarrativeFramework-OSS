---
tags: [design, narrative-framework, druzyna]
status: draft
aliases: ["Strona drużyny", "Party page"]
---

# Strona drużyny — projekt

Uzgodnione z właścicielem 2026-11-02 (sesja pytań Q1–Q33). Pojęcia: słownik w `web/docs/architecture.md`, sekcja
**Party** (Party, Specialization, Player material, Sharing, Party board, Card, Board link, Former player).
Decyzja o prywatności: **ADR 013**.

## Po co

Gracze w grach drużynowych (np. D&D) grają zespołem, a dziś w kampanii tylko czytają: widzą strony opublikowane przez MG
i własny kreator. Nie widzą, co tworzy reszta drużyny, więc nie koordynują ról (np. specjalista od techniki bez
umiejętności technicznych) i nie mają gdzie pisać własnych rzeczy. Strona drużyny to **pierwsze miejsce, w którym
gracze piszą** — potrzebne, zanim drużyna zacznie kampanię.

**Sukces:** przed sesją zero każdy gracz widzi, kto kim gra i w czym jest dobry; w trakcie kampanii drużyna prowadzi
wspólną tablicę śledztwa, a gracze trzymają dzienniki i sekrety postaci, które MG wplata w fabułę.

**Anty-cele:** mieszanie treści graczy z wiki MG (sekrety, szkice MG); gracz edytujący cudzy tekst; zewnętrzne
zasoby osadzane na stronie (śledzenie IP graczy); osobne narzędzie, którego trzeba się uczyć — strona ma działać
na telefonie tak samo jak reszta kampanii.

## Zakres (decyzje właściciela)

### Drużyna
- **Jedna drużyna na kampanię**; należą do niej wszyscy gracze kampanii (Q2). Napis w interfejsie zależy od wyglądu
  kampanii: Fantasy „Drużyna”, Cyberpunk „Ekipa” (Q1). Zakładka obok „Tworzenie postaci”.
- **Kafelek gracza** (Q3, Q20, Q15): imię postaci, opcja osi postaci (rola/klasa), **specjalizacja** = trzy najwyższe
  umiejętności z wartością (i nazwą progu, jeśli pakiet ma `skill_tiers`) + jedno zdanie gracza „w drużynie
  odpowiadam za…”. Na wierzchu zatwierdzona postać; bez niej ostatnio zmieniany szkic w toku. Pozostałe szkice
  w rozwijanej liście, **domyślnie zwiniętej** (Q16).
- Ze szkiców inni gracze widzą tylko to, co na kafelku — bez statystyk, karty i historii (Q3).
- **Bez rezerwacji ról**: dwie te same role są dozwolone (Q5).
- **Kreator postaci** pokazuje nad wyborem roli jedną linijkę „W ekipie: Wojownik (Mira), Kapłan (Tor)” z tych samych
  danych (Q32).

### Materiały graczy
- Jeden rodzaj: **tytuł + treść Markdown + obrazki**, bez kategorii (Q28). Żyją na stronie drużyny, nie w wiki MG
  (Q8). MG może skopiować materiał do swojej wiki.
- **Udostępnianie** (Q6, Q10, Q11): „Prywatne” / „Wybrani gracze” / „Cała drużyna”. Autor **wybiera przy każdym
  tworzeniu** — brak wartości domyślnej. Można zmienić później.
- **MG widzi wszystkie materiały, także prywatne**; kontrolka mówi tylko „Prywatne”, a przewodnik gracza i informacja
  o prywatności mówią wprost, że prowadzący widzi wszystko (Q9, ADR 013).
- **Zmienia i usuwa tylko autor** (Q7, Q13). MG może materiał ukryć albo usunąć, nie edytuje tekstu.
- Obrazki: gracze wgrywają, limity jak u MG (do 8 MB, prywatne dla kampanii); obrazek dziedziczy widoczność
  materiału lub karty, w której jest (Q14).

### Tablica drużyny
- **Karty**, oglądane jako **lista** albo **mapa** (Q12). Karta: tytuł, krótki tekst, opcjonalny odnośnik — do
  opublikowanej strony wiki, do materiału gracza albo **zewnętrzny adres** (Q29). Kto nie widzi materiału, widzi kartę
  bez odnośnika.
- Kartę dodaje każdy z drużyny; **zmienia i usuwa tylko jej autor** (Q13). MG dodaje własne karty (np. przypięta
  wskazówka) i może ukryć lub usunąć kartę gracza bez zmiany tekstu (Q21).
- Tablica jest zawsze dla **całej drużyny** + MG; prywatne rzeczy idą do materiałów (Q19).
- **Mapa:** jeden wspólny układ, każdy przesuwa każdą kartę (Q18). **Strzałki** łączy każdy między dowolnymi
  widocznymi kartami; strzałka należy do autora (zmienia/usuwa tylko on) i ma **jego kolor** (Q17).
- **Kolory graczy** (Q26, Q27): przydzielane automatycznie z palety dobranej ze Słownika Kolorów, tak by się nie
  zlewały; gracz może wybrać inny z tej palety; MG ma stały kolor. Kolor autora = kolor jego strzałek i obramowania
  jego kart.

### Wspólne zasady
- **„Nowe od ostatniej wizyty”** — znacznik przy nowych kartach i nowo udostępnionych materiałach; bez powiadomień
  mailowych (Q23).
- **Historia** (Q31): każda zmiana materiału, karty, strzałki i położenia na mapie trafia do historii „kto, co, kiedy”.
  Historię widzi MG; gracze widzą przy karcie „ostatnio zmienione przez X”.
- **Były gracz** (Q22, Q24, Q25): jego materiały, karty i strzałki **zostają** z dotychczasową widocznością, oznaczone
  „były gracz”; usuwa je MG. Autorem jest **konto**, nie członkostwo — po powrocie do kampanii rzeczy wracają do
  właściciela z prawem edycji.
- **Wdrożenie** (Q30): flaga instancji (domyślnie wyłączona) + przełącznik MG w kampanii „Strona drużyny”
  (domyślnie włączony, gdy flaga aktywna).
- **MCP autora** (Q33): tylko odczyt — gracze, specjalizacje, materiały (z prywatnymi) i tablica — żeby przy
  przygotowaniu sesji wplatać sekrety graczy w fabułę.

## Ograniczenia z projektu

- Standard kampanii (`CLAUDE.md`): **lista + dodaj + zmień + usuń w panelu** dla materiałów, kart i strzałek od
  pierwszej wersji; usuwanie z potwierdzeniem i skutkiem („usunie też 2 strzałki”); audyt zmian; feature flag.
- Granica uprawnień: `campaign_access.py` (blokada kampanii przed sprawdzeniem członkostwa, jak w wiki). Odpowiedzi
  prywatne, `no-store`. Audyt bez treści tekstów (jak `wiki.touch`).
- Telefon: `runtime_browser` pilnuje, że pierwsza decyzja sesji mieści się w 844 px — nowa zakładka w nagłówku
  kampanii nie może tego zepsuć (zdarzyło się dwa razy).
- Zmiany schematu addytywne (blue/green); nic istniejącego nie staje się widoczne dla graczy.

## Decyzje wykonawcy (moje, do sprawdzenia przy czytaniu)

- **Linki zewnętrzne:** tylko `http`/`https`, nowa karta, `rel="noopener noreferrer"`; zewnętrznych obrazków nie
  osadzamy (śledzenie IP).
- **Limity (backpressure):** materiał ≤ 12 000 znaków, ≤ 50 materiałów na gracza, ≤ 300 kart i ≤ 600 strzałek na
  drużynę, tytuł ≤ 200, tekst karty ≤ 2 000, zdanie specjalizacji ≤ 200, odbiorców „wybranych” ≤ liczba graczy.
- **Eksport:** eksport MG (`narrative-wiki/2` lub następca) zawiera drużynę z prywatnymi materiałami; eksport gracza
  — tylko to, co gracz widzi.
- **Zdanie specjalizacji należy do postaci** (zdarzenie w jej dzienniku, zgodnie z ADR 012), nie do członkostwa;
  edytowane na stronie drużyny przez właściciela postaci.
- **„Trzy najwyższe umiejętności”** = najwyższa wartość (`skill_value`, bez niego ranga); remisy rozstrzyga kolejność
  w pakiecie. Pakiet bez umiejętności → tylko zdanie gracza.
- **Gracz bez postaci** ma kafelek z imieniem konta i przyciskiem do kreatora.
- **Paleta kolorów:** przy implementacji otworzyć `Słownik Kolorów.pdf`, wybrać połączenie z min. 6 rozróżnialnymi
  kolorami (kontrast na jasnym i ciemnym wyglądzie kampanii), wpisać stronę/nazwę i wartości w tym dokumencie.
- MG nie ma kafelka (nie jest członkiem drużyny), ale ma kolor na tablicy.
- **„Ukryte przez MG”**: materiał/karta znika dla pozostałych graczy; autor dalej go widzi z oznaczeniem „ukryte przez
  prowadzącego” i może go usunąć; MG może cofnąć ukrycie. Strzałki do ukrytej karty znikają razem z nią.
- **Położenia na mapie** w historii zapisywać jako jedno zdarzenie na zakończone przeciągnięcie, nie na każdy piksel.

## Dla wykonawcy

Model danych, API, nazwy zdarzeń, układ mapy (wzorzec: mapa relacji wiki na React Flow), konflikty równoczesnych
przesunięć (wersje optymistyczne jak w wiki) i podział na plany — do ustalenia w planie implementacji
(`superpowers:writing-plans`), wg wzorca planów w `web/docs/design/plany/`.

## Otwarte pytania do dograć

- Brak — runda pytań zamknięta 2026-11-02. Nowe wątpliwości z planowania dopisywać tutaj.
