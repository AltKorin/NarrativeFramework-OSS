# Przykład: noc poza drużyną

[English](README.en.md) · [Instrukcja](../docs/pl/individual-play.md)

Mała przygoda daje nieobecnemu zwiadowcy znaczące zajęcie, gdy drużyna bada ruiny. Arin zostaje w porcie i pomaga Mirze szukać śladów zaginionego ojca. O świcie wraca z opowieścią o nocy i propozycjami konsekwencji dla MG.

## Wczytaj materiały

Zaimportuj [`absence.pl.json`](absence.pl.json) przez **Import JSON** i zapisz na swoim koncie. Plik korzysta z istniejącego formatu scenariuszy webowych i zawiera prywatne sekrety autora przeznaczone dla MG. Pełny plik źródłowy nie jest materiałem dla gracza prywatnej przygody. Ten przykład jest publiczny w repozytorium open source: pokazuje filtrowanie pól, a nie tajność już opublikowanej fabuły.

Możesz też zainstalować ten sam prywatny scenariusz dla istniejącego konta. Z katalogu `web/`:

```sh
docker compose exec api python manage.py seed_absence_example --owner TWOJ_LOGIN --language pl
```

Polecenie nie tworzy kont ani tokenów i nie nadpisuje istniejącego przykładu o tym samym właścicielu i tytule. Konto gracza tworzy administrator. Do próby solo możesz przypisać przygodę sobie.

## Przygotuj aktywność

W sekcji **Między sesjami** przypisz zapisany scenariusz:

| Pole | Przykład |
| --- | --- |
| Rodzaj | Nieobecna postać |
| Postać | Arin, zwiadowca wracający do drużyny o świcie |
| Wprowadzenie | Gdy drużyna bada ruiny, Mira prosi Arina o poszukiwanie śladów ojca na nabrzeżu |
| Instrukcje | Krótkie śledztwo, rozmowa i znaczący wybór; reaguj także na pomysły spoza grafu |
| Granice | Bez trwałych obrażeń, rzadkich nagród i rozwiązania głównego zadania; trwałe odkrycia i relacje wymagają MG |
| Powrót | O świcie Arin może spotkać drużynę w porcie |
| Opublikowane elementy | `harbor`, `market`, `mira`; scena początkowa `harbor` jest wymagana |
| Z góry zatwierdzone kategorie | Żadne |
| Prywatne uwagi MG | Uzgodnij raport z główną sesją; zachowaj dla siebie informacje o solni i kaplicy |

Przeczytaj opisy przed publikacją. Graf źródłowy zawiera też `salt`, `ledger` i `chapel`, z informacjami niepotrzebnymi w tej małej aktywności. Nie publikuj ich w tym przykładzie. Pola sekretów są pomijane także dla trzech wybranych elementów.

## Rozegraj przez MCP

Przypisany gracz generuje token w panelu i konfiguruje `/play/mcp`. Wiadomość startowa jest dostępna w panelu. Agent odczytuje kontekst, pyta o działania Arina i improwizuje w ustalonych granicach.

Aby przejść przykład na poziomie narzędzi, użyj argumentów zapisu i zakończenia z [kontraktu MCP](../docs/pl/mcp-play.md). Przebieg:

1. Jedna tura zapisana z wersją 2: Arin pomaga Mirze i znajduje stronę. Pamięć zaznacza, że kontakt i odkrycie oczekują na MG.
2. Identyczne powtórzenie UUID i argumentów zwraca potwierdzenie bez drugiego wpisu dziennika.
3. Zakończenie z wersją 3 zapisuje powrót Arina, ale obie konsekwencje nadal oczekują.
4. Nowa rozmowa z agentem odczytuje ten sam trwały stan; nie potrzebuje oryginalnego transkryptu.
5. MG rozpatruje skutki. Może zatwierdzić, że Mira pamięta pomoc, a odrzucić odkrycie przez stronę sekretu głównego przeciwnika.

Detale w przykładach narzędzi są ilustracją, nie faktami fabularnymi zweryfikowanymi przez serwer. Test integracyjny korzysta z tej samej sceny początkowej i proponowanego kontaktu; sprawdza rozdzielenie tokenów i katalogów, wznowienie w nowym kliencie MCP, decyzję przez REST człowieka oraz unieważnienie dostępu.

## Oczekiwany raport

Raport oddziela:

- **Przebieg gry:** Arin szukał z Mirą i wrócił o świcie.
- **Zatwierdzone konsekwencje:** tylko wpisy przyjęte przez MG lub wcześniej dopuszczone kategorie.
- **Oczekujące i odrzucone skutki:** nie są przyjętymi faktami kampanii.
- **Prywatne uwagi MG:** obecne tylko w eksporcie MG, nie w eksporcie gracza.

MG ręcznie włącza uzgodnione skutki do wspólnej gry. Przykład nie aktualizuje ekwipunku, karty postaci ani głównej kampanii. Rzuty losowe i mechanika konkretnego RPG pozostają poza zakresem tej wersji.
