# Nowa strona główna Narrative Framework

[English](../en/website.md) · [Uruchomienie](../../README.pl.md)

Strona przedstawia webowy warsztat RPG. Unity pojawia się wyłącznie w historii powstania projektu. Nie promuje pakietu Unity ani nie obiecuje wbudowanego modelu AI.

## Adresy i konfiguracja

- `/`, `/pl/`, `/en/`: strona główna; PL/EN można zmieniać bez przeładowania.
- `/workshop/`: edytor, publiczne demo oraz panel konta.
- `/workshop/?account=register&lang=pl`: otwarty formularz rejestracji.
- `/mcp`, `/play/mcp`, `/api/`: dotychczasowe kontrakty pozostają pod tymi adresami.
- `/privacy.html`, `/cookies.html`: zachowane oryginalne dokumenty istniejącej witryny.

Adresy przycisków znajdują się w `web/frontend/public/site-config.json`:

```json
{
  "applicationUrl": "/workshop",
  "repositoryUrl": "https://github.com/AltKorin/NarrativeFramework-OSS",
  "supportUrl": "https://www.buymeacoffee.com/StolemCentrumGier"
}
```

`repositoryUrl` wskazuje publiczne repozytorium projektu. Na własnej instancji można podać adres swojego forka albo ustawić `null`, aby ukryć odnośnik. Wsparcie używa napisu „Oil to my machine” i zwykłego linku — skrypt Buy Me a Coffee nie jest ładowany.

Jeśli aplikacja stanie na osobnym hoście, ustaw `applicationUrl` na przykład na `https://app.narrativeframework.com/workshop/`. To proponowany adres, nie potwierdzone wdrożenie. Skonfiguruj ten host w Django, CSRF i MCP zgodnie z instrukcją instalacji. Nie wystarczy zmienić przycisku na stronie.

Po zmianie konfiguracji przebuduj frontend, ponieważ linki są również zapisane w generowanym HTML. Backend nie jest potrzebny do wyświetlenia samej strony głównej. Do edytora i rejestracji potrzebny jest cały stos Compose.

## Rejestracja wersji testowej

`ALLOW_REGISTRATION=1` włącza publiczne tworzenie zwykłych kont; `0` je wyłącza. Flaga jest zwracana przez `/api/session/`, a interfejs ukrywa formularz po jej wyłączeniu.

`POST /api/register/` wymaga tokena CSRF pobranego z `/api/session/` i pól `username`, `password`, `password_confirm`. Login: 3–32 znaki ASCII, litery/cyfry/kropka/podkreślenie/myślnik. Hasło: minimum 12 znaków, maksymalnie 4096; sprawdzane walidatorami Django. Rozbieżne lub słabe hasła zwracają 400, zajęty login 409, brak CSRF 403. Osobny limit: pięć prób na godzinę na IP. Udana rejestracja tworzy sesję i zwraca 201. Nadmiarowe pola nie nadają uprawnień administratora.

Nie ma e-maili ani automatycznego odzyskiwania hasła. Informacja jest widoczna przed rejestracją. Reset administracyjny: `docker compose exec api python manage.py changepassword LOGIN`. Przygody można eksportować. Obecne dokumenty prywatności/cookies zachowano bez redakcji; po ustaleniu publicznego hostingu wymagają uzgodnienia z nowym zakresem kont i danych.

## Budowanie i sprawdzanie

Z głównego katalogu repozytorium:

```sh
python3 web/scripts/sync-site-docs.py
npm --prefix web/frontend ci
npm --prefix web/frontend run build
docker compose -f web/compose.yaml up --build -d
docker compose -f web/compose.yaml exec -T api python manage.py test stories
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/site_browser.py
```

Playwright jest opcjonalną zależnością developerską opisaną w [weryfikacji](../verification.md). Test tworzy tymczasowe konto przez publiczny formularz, zapisuje prywatną przygodę i usuwa konto w `finally`.

Vite generuje osobny pakiet landing page i edytora. Dodatkowy build SSR służy wyłącznie do wygenerowania statycznego HTML PL/EN, z opisem, canonical, hreflang i obrazem Open Graph. Na serwerze nie uruchamia się Node. Caddy serwuje stronę i przekazuje dotychczasowe endpointy do API/MCP. Opcjonalna analityka wymaga zgody; aplikacja ma osobny, domyślnie wyłączony pomiar. Zewnętrzne fonty i skrypt wsparcia nie są ładowane. Ilustrację portu wygenerowano przy użyciu AI; podgląd edytora to prawdziwy zrzut lokalnego demo.

## Wdrożenie referencyjne przez Cloudflare Tunnel

Strona główna działa pod https://narrativeframework.com, a warsztat pod https://app.narrativeframework.com/workshop/. Wdrożenie na Debianie 13 w osobnym LXC obejmuje Docker Compose i usługę systemową cloudflared. PostgreSQL ma osobny trwały wolumen; nie przenoszono bazy developerskiej ani kont testowych.

Cloudflared przekazuje ruch HTTP do Caddy wewnątrz LXC; publiczne połączenie korzysta z HTTPS. `SECURE_COOKIES=1` jest wymagane w tym wariancie. `TRUSTED_PROXY_CIDRS` zawiera wyłącznie adresy lokalnego proxy: przy publikacji przez port Dockera jest to również adres jego bramy. Caddy analizuje X-Forwarded-For od prawej strony, odrzuca podszyte nagłówki od niezaufanych źródeł i przekazuje rozwiązany adres klienta do API. Dzięki temu limit rejestracji nie obejmuje wszystkich użytkowników jako jednego połączenia tunelu.

`PUBLIC_HOSTS` określa nazwy, dla których żądania z nagłówkiem proxy `X-Forwarded-Proto: http` mają przekierować do HTTPS. W tym wdrożeniu są to domena główna, www i app. Do innej instalacji wpisz własne nazwy i zaufane adresy proxy.

Konfiguracja tunelu i jego poświadczenia pozostają na serwerze, poza repozytorium i obrazami Dockera. Cloudflared uruchamia się jako usługa po restarcie. Przed przełączeniem domeny zapisano kopię poprzednich rekordów domeny głównej i www. Nie modyfikowano rekordów poczty ani innych subdomen.

Po zmianie konfiguracji frontendowej przebuduj usługę web. Własny `site-config.json` oraz `.env` są konfiguracją danej instancji; zachowaj ich wartości przy aktualizacjach źródeł. Sekrety i ustawienia tunelu nie powinny trafić do archiwum wydania. `web/scripts/package-web.py` wyklucza lokalne sekrety, bazy, zależności i prywatne notatki wdrożeniowe.

Testy na docelowym LXC oraz przez publiczne HTTPS opisuje [weryfikacja](../verification.md). Przy świeżym DNS można użyć `SITE_TEST_RESOLVE_IP` z potwierdzonym publicznym adresem Cloudflare — dotyczy to wyłącznie procesu testowego; certyfikat nadal jest sprawdzany dla rzeczywistej nazwy domeny. Nie zmienia to DNS ani pliku hosts komputera.

Szczegółowa instrukcja przygotowania kontenera: [LXC na proxmox-hp](lxc-setup.md).


[Blog / SEO / Analytics](publishing-and-analytics.md)
