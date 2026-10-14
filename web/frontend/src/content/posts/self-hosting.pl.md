Własny serwer przydaje się wtedy, gdy chcesz decydować, gdzie przechowujesz kampanie, kiedy aktualizujesz aplikację i jak robisz kopie zapasowe. Narrative Framework można uruchomić z otwartego kodu przez Docker Compose. Publiczna instancja testowa nie jest wymagana do korzystania z własnej instalacji.

## Co uruchamiasz

Stos obejmuje aplikację Django i API, PostgreSQL, Redis, frontend React oraz Caddy. MCP działa jako osobny serwis, żeby można było oddzielnie ograniczać jego zasoby. To nadal jeden serwer z częściowo wspólnymi zasobami, a nie gwarancja odporności na dowolny ruch agenta.

W przeglądarce dostajesz graf przygody, wiki kampanii i panel indywidualnych rozgrywek. Nie potrzebujesz Unity. Jego oryginalny kod pozostaje częścią historyczną repozytorium, z osobnym formatem danych.

## Najpierw uruchom lokalnie

Pobierz [repozytorium GitHub](https://github.com/AltKorin/NarrativeFramework-OSS), skopiuj `web/.env.example` do `web/.env` i ustaw dwa niezależne, losowe sekrety: `DJANGO_SECRET_KEY` i `POSTGRES_PASSWORD`. Potem, w katalogu repozytorium:

```sh
docker compose -f web/compose.yaml up --build -d
```

Domyślny adres to `http://localhost:8080`. Demo działa bez konta, ale zapisuje szkic tylko w tej przeglądarce. Konto daje zapis na serwerze. Wiki wymaga logowania, a członkowie dołączają do kampanii przez zaproszenia.

Kompletna [instrukcja instalacji](/README.pl.md) wyjaśnia konfigurację i aktualizacje. Nie kopiuj na własny serwer sekretów ani ustawień produkcyjnych cudzej instancji.

## Przed wystawieniem do Internetu

Ustaw własne hosty, adresy CSRF i reguły zaufanego proxy. W publicznym wdrożeniu użyj HTTPS oraz bezpiecznych cookies. Przy Cloudflare Tunnel adres tunelu zastępuje kierowanie domeny bezpośrednio na prywatny adres kontenera.

PostgreSQL przechowuje kampanie, publikacje, członkostwa i dzienniki. Wolumen Dockera zachowuje je po ponownym uruchomieniu kontenerów, ale nie zastępuje kopii zapasowej. Polecenie `docker compose down -v` usuwa wolumeny. Kopię bazy przechowuj poufnie i sprawdź odtwarzanie na oddzielnej instancji.

## Co daje otwarty kod

Kod jest dostępny na licencji MIT, z osobnymi zasadami używania marki. Możesz sprawdzić działanie uprawnień, rozwijać funkcje i postawić własną wersję. Zależności oraz obrazy kontenerów zachowują własne licencje.

Eksporty JSON i Markdown pomagają przenosić materiały, ale nie obiecują kompatybilności z każdym narzędziem RPG. Import z Kanki i konwersja formatów Unity nie są obecnie gotowymi funkcjami.

## Gdzie mieści się AI

Do uruchomienia warsztatu nie trzeba klucza dostawcy modelu. Rozgrywkę z AI prowadzi zewnętrzny klient MCP. Osobisty token autora daje dostęp do materiałów MG i nie powinien trafiać do gracza; indywidualna rozgrywka ma własny, ograniczony token.

Jeśli na razie chcesz tylko obejrzeć narzędzie, [otwórz publiczne demo](/workshop/?lang=pl). Przygotowanie wspólnej bazy świata opisuje [poradnik wiki kampanii](/pl/blog/wiki-kampanii-rpg-bez-spoilerow/).
