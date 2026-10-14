# Próby psucia aplikacji i podstawa regresji

[English](../en/adversarial-testing.md) · [Historia weryfikacji](../verification.md)

Ta runda celowo atakowała aplikację, zamiast ograniczać się do poprawnego przebiegu gry. Celem były lokalne dane testowe: tymczasowe konta, przykładowe przygody i osobny projekt Docker Compose. Próby nie dotyczyły domeny publicznej ani prawdziwej kampanii.

## Odtworzone błędy i poprawki

| Próba | Przed poprawką | Test i zachowanie po poprawce |
| --- | --- | --- |
| Hasło jako obiekt, lista, liczba lub bool | HTTP 500 w uwierzytelnianiu | Sprawdzenie typu i długości; HTTP 400 |
| NUL, niepoprawny Unicode, zły tekst ukryty w metadanych | Błędy PostgreSQL lub kodowania; SQLite przepuszczała część danych | Walidacja kluczy i wartości przed zapisem; 400 bez zapisu |
| Współrzędna z 1001 cyfr | OverflowError w `math.isfinite` | Najpierw sprawdzany zakres; HTTP 400 |
| JSON zagnieżdżony na 1200 poziomach | RecursionError / HTTP 500 | Ograniczony parser i iteracyjne sprawdzanie struktury; HTTP 400 |
| Dokument bez opcjonalnych pól tekstu, sekretu i pozycji | Zapisany scenariusz nie dawał się otworzyć w edytorze | Kanoniczne puste teksty i pozycje zerowe |
| Ten sam UUID tworzenia z innym tytułem lub dokumentem | 200 po cichym zignorowaniu nowej treści | Stały skrót pierwotnej operacji; inna treść daje 409 |
| Utrata odpowiedzi po utworzeniu, potem ponowienie lub odświeżenie | Duplikaty prywatnych przygód | Przeglądarka zachowuje UUID operacji aż do potwierdzenia, także po odświeżeniu |
| Edycja szkicu po utracie odpowiedzi, potem ponowienie | Duplikat albo ryzyko utraty lokalnej treści | Konflikt tej samej operacji; lokalna zmieniona treść pozostaje dostępna |
| Wylogowanie podczas pisania lokalnego demo | Szkic zastępowany fabrycznym przykładem | Lokalny szkic pozostaje; prywatna przygoda wraca do osobno zapisanego demo |
| Import elementu z `kind: ["npc"]` | Konwersja typu przepuszczała wadliwy dokument do potwierdzenia | Ścisły typ tekstowy i walidacja JSON przed zastąpieniem treści |
| Cztery równoczesne rotacje tokena autora, z opóźnieniem bazy | Trzy odpowiedzi 500 w odtworzonym przebiegu | Transakcja i blokada właściciela; jeden aktualny token |
| Odczyt zatrzymany po pobraniu wersji, w międzyczasie zapis tury | Stara wersja i pamięć połączone z nowszym dziennikiem | Odczyt kontekstu blokuje ten sam wiersz co zapis |
| Powtarzane kontrole zdrowia bazy | Kontrola zużywała limit użytkownika i zwracała 429 | Sonda niezależna od limitowania i cache użytkownika |
| Niekompletna treść MCP lub przekroczony czas wykonania | Nieobsłużony timeout, zerwane połączenie lub ogólny błąd serwera | 408 przy odbiorze, 504 przed rozpoczęciem odpowiedzi; po rozpoczęciu przerwanie strumienia bez drugiego statusu |
| Niedostępny Redis lub PostgreSQL | Ogólne błędy na dotkniętych ścieżkach | Kontrolowane 503; brak odrzuconego zapisu po wznowieniu; procesy odzyskują połączenia |

Początkowy zestaw 12 metod testów atakujących na PostgreSQL dał 20 niespełnionych asercji i dwa błędy, wliczając podprzypadki. To liczba przypadków, **nie dwadzieścia osobnych luk**. Wstępny test przeglądarki nie przeszedł czterech z pięciu prób; HTML już wcześniej pozostawał zwykłym tekstem. Końcowy zestaw obejmuje 48 metod testów Django oraz próby przeglądarkowe, rzeczywistego MCP i awarii zależności.

## Granice, których nie udało się obejść w tych próbach

- Obce konto nie odczytuje prywatnego scenariusza/rozgrywki i nie zatwierdza ich konsekwencji.
- Token gracza nie działa w autorskim MCP ani REST; token autora nie działa w MCP gracza.
- Katalog gracza nie zawiera narzędzi autora, a dodatkowe argumenty nie zmieniają przypisanej rozgrywki.
- Wymuszenie `status: approved` i przejście do nieopublikowanej sceny są odrzucane bez zapisu tury.
- Sekrety źródła, dodatkowe pola prywatne i notatki MG nie trafiają do kontekstu ani raportu gracza.
- Importowany HTML i skrypty są wyświetlane jako tekst, nie wykonywany DOM.
- Zbyt duża treść, błędny JSON, rozłączenie i timeout zwalniają zajęte miejsca MCP. Rozpoczęta odpowiedź nie dostaje drugiego statusu HTTP.
- Identyczne ponowienia nie dublują tur ani skutków; inne treści i nieaktualne wersje powodują konflikt. Decyzja MG pozostaje osobną operacją.

Testy dotyczą uprawnień backendu, nie posłuszeństwa modelu. Model nadal może opisać nieprawdziwe lub niedozwolone zdarzenie. Granicą przyjętych konsekwencji pozostaje rejestr decyzji MG.

## Osobny cel testów

Użyj skonfigurowanego `web/.env` i Docker Compose. Z głównego katalogu repozytorium:

```sh
HTTP_PORT=127.0.0.1:18080 HTTPS_PORT=127.0.0.1:18443 \
  docker compose -p narrative-adversarial -f web/compose.yaml up --build -d
```

Poczekaj na zakończenie uruchamiania. Projekt ma oddzielne kontenery PostgreSQL/Redis i wolumeny danych. Port HTTP jest dostępny tylko lokalnie. Współdzieli konfigurację źródłową i cache buildów, nie bazę głównej aplikacji. Testów awarii nie kieruj do prawdziwej kampanii.

## Powtórzenie prób

Testy bazy i deterministyczne próby ASGI:

```sh
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api \
  python manage.py test stories --verbosity 1
```

Rzeczywiste MCP, także wrogie wywołania gracza, oraz niekompletne żądanie HTTP:

```sh
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api python < web/tests/integration.py
docker compose -p narrative-adversarial -f web/compose.yaml exec -T \
  -e PLAY_TEST_BASE=http://web -e PLAY_TEST_HOST=localhost:18080 api python < web/tests/play_integration.py
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api python < web/tests/adversarial_slow_body.py
```

Przeglądarka wymaga opcjonalnego Playwrighta opisanego w instrukcji weryfikacji:

```sh
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers \
  web/.venv/bin/python web/tests/adversarial_browser.py
```

Skrypt tworzy tymczasowe konto w `narrative-adversarial`, przerywa wyłącznie jego odpowiedź tworzenia scenariusza **po** udanym zapisie serwera, ponawia, odświeża i sprawdza wynik. Testuje także wylogowanie i złośliwy import. Sekrety są trzymane w pamięci procesu i nie są wypisywane. Nie przerywa przypadkowego ruchu innych użytkowników.

Awaria i wznowienie zależności — uruchamiaj **po** zakończeniu pozostałych prób:

```sh
web/.venv/bin/python web/tests/adversarial_faults.py
```

Projekt i lokalny port są wpisane w skrypcie na stałe. Zatrzymuje i przywraca tylko jego Redis/PostgreSQL, sprawdza 503, brak zapisu i odzyskanie działania. Obie zależności są przywracane w `finally`. Nie przebudowuj kontenerów ani nie uruchamiaj innych scenariuszy w tym celu testowym podczas próby awarii.

Na koniec zatrzymaj stos, zachowując wolumeny:

```sh
docker compose -p narrative-adversarial -f web/compose.yaml down
```

## Uściślone kontrakty

- Treść żądań HTTP ma format `application/json`; limit odczytu 512 KiB, do 32 poziomów zagnieżdżenia i 50 000 wartości. MCP zachowuje limit zewnętrzny 300 KB, a dokument scenariusza 256 KB po uzupełnieniu pól domyślnych.
- Klucze i tekst JSON muszą poprawnie kodować się w UTF-8 i nie mogą zawierać NUL. Odrzucane są liczby nieskończone i NaN. Zwykły polski tekst, nowe linie i Unicode pozostają obsługiwane.
- Nowe scenariusze mają skrót znormalizowanej treści tworzenia. Zgodne ponowienie zwraca bieżący własny scenariusz, także po późniejszych zmianach. Starsze rekordy bez skrótu są porównywane z aktualną treścią, bo nie da się odtworzyć ich pierwotnego żądania.
- Tworzenie przez API chroni limit 50 prywatnych scenariuszy blokadą właściciela. Polecenia administracyjne i bezpośrednia baza są zaufaną administracją, nie ścieżką tego limitu API.
- Kontekst jest spójny podczas odczytu; następna operacja może od razu zdezaktualizować wersję. Stare potwierdzenia mutacji nadal opisują pierwotny zapis, nie aktualną decyzję MG.
- Sonda sprawdza bazę niezależnie od Redisa. Pozostałe błędy przyjmowania żądań blokują operację i zwracają ogólne 503 bez szczegółów połączenia.
- Anulowanie odpowiedzi async nie gwarantuje zatrzymania już działającego synchronicznego kodu bazy. Po niepewnym wyniku ponów ten sam UUID i treść oraz odczytaj kontekst.

## Zakres pozostający poza tą rundą

To nie jest deklaracja kompletnego pentestu, pojemności ani semantycznego egzekwowania zasad RPG czy odporności modelu na prompt injection. Nie sprawdzono każdej awarii sieci, wyczerpania zasobów systemu, silnika przeglądarki, OAuth (niezaimplementowany), publicznego TLS/DNS ani docelowego Proxmoxa/LXC. Kontekst nadal zawiera tylko dwadzieścia ostatnich konsekwencji; pełny rejestr jest w panelu i raporcie. Publiczna sonda bazy celowo nie korzysta z limitu użytkownika; kontrola ruchu na brzegu wdrożenia pozostaje poza tym zestawem regresji.
