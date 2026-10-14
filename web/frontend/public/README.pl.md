# Narrative Framework 0.3.0-alpha.2

[English](README.md) · [Indywidualne przygody](docs/pl/individual-play.md) · [Kontrakt MCP gracza](docs/pl/mcp-play.md) · [Przykład](examples/README.pl.md)

Samodzielny warsztat przygód RPG obok oryginalnych assetów Unity. Django/DRF, PostgreSQL, React/TypeScript, React Flow i osobny serwis MCP. Licencja MIT; zasady używania nazwy i znaków marki opisuje osobny plik [BRANDING.md](../BRANDING.md).

## Co działa

- Edytor grafu scen, NPC, miejsc i wskazówek, prywatne sekrety MG i kontrola struktury.
- Demo bez konta z lokalnymi szkicami, prywatne scenariusze po zalogowaniu, import/eksport JSON i Markdown.
- Współdzielenie wiki: zaproszenia, role właściciela/współ-MG/gracza, oddzielne publikacje i wybrani odbiorcy.
- Interfejs PL/EN z zapamiętywaniem wyboru oraz przykład w obu językach.
- Przypisanie wybranych materiałów jednej postaci: downtime, nieobecność lub solo.
- MCP gracza z tokenem konkretnej rozgrywki, pamięć Markdown, dziennik i raport.
- Konsekwencje domyślnie oczekujące na decyzję człowieka pełniącego rolę MG.

Serwer nie zawiera własnego czatu AI ani modelu. Rozgrywkę prowadzi agent w Twoim kliencie MCP. Nie wykonuje rzutów, nie weryfikuje mechaniki RPG ani zgodności narracji z granicami. Nie zmienia automatycznie głównej kampanii.

## Uruchomienie przez Docker Compose

Z katalogu `web/`, z zainstalowanym Docker Engine i pluginem Compose:

```sh
cp .env.example .env
python3 -c 'import secrets; print(secrets.token_urlsafe(48))'
# Ustaw wygenerowany DJANGO_SECRET_KEY i osobne losowe POSTGRES_PASSWORD w .env.
docker compose up --build -d
docker compose exec api python manage.py createsuperuser
```

Strona główna działa pod **http://localhost:8080**, a pulpit warsztatu pod **http://localhost:8080/workshop**. Wybierz **Nowa kampania**, aby nazwać świat, **Nowa przygoda**, aby zacząć pusty scenariusz, albo **Otwórz demo przygody**, aby obejrzeć przykład. Po zalogowaniu pulpit pokazuje własne kampanie i przygody. W warsztacie można utworzyć zwykłe konto, zapisać prywatną przygodę i skorzystać z sekcji **Między sesjami**. Administrator może wyłączyć rejestrację ustawieniem `ALLOW_REGISTRATION=0`. Rejestracja wymaga CSRF, poprawnego loginu i silnego hasła o długości minimum 12 znaków; limit wynosi pięć prób na godzinę na IP. Nie ma e-maili ani automatycznego resetu hasła. Nadal można tworzyć konta administracyjnie — `createsuperuser` tworzy konto administratora, a zwykłe konto można utworzyć interaktywnie przez Django:

```sh
docker compose exec api python manage.py shell
```

W powłoce Pythona:

```python
from django.contrib.auth import get_user_model
from getpass import getpass
get_user_model().objects.create_user("login_gracza", password=getpass("Hasło: "))
```

Nie umieszczaj haseł w poleceniach powłoki, dokumentach przygody ani repozytorium. Konfiguracja `.env` jest ignorowana przez Git.

Pierwsze uruchomienie wykonuje migracje i tworzy publiczne demo. Migracja 0002 dodaje tabele rozgrywek, dziennika i decyzji; nie usuwa istniejących scenariuszy. Dane pozostają w wolumenie PostgreSQL. `docker compose down` zachowuje wolumeny; `down -v` usuwa je wraz z danymi.

## Wdrożenie publiczne

Aplikacja jest przygotowana do samodzielnego hostowania. Nie konfiguruje Proxmoxa/LXC ani DNS. Użyj kontenera LXC, w którym Docker już działa zgodnie z polityką hosta, lub maszyny wirtualnej. Dla bezpośredniego HTTPS ustaw w `.env`:

```dotenv
SITE_ADDRESS=narrativeframework.com
HTTP_PORT=80
HTTPS_PORT=443
ALLOWED_HOSTS=narrativeframework.com,localhost,127.0.0.1,api,mcp
CSRF_TRUSTED_ORIGINS=https://narrativeframework.com
SECURE_COOKIES=1
MCP_ALLOWED_HOSTS=narrativeframework.com,narrativeframework.com:443,localhost:*,127.0.0.1:*,mcp:*
MCP_ALLOWED_ORIGINS=https://narrativeframework.com
```

Skieruj DNS na serwer i zapewnij dostęp do portów 80/443. Caddy pobiera certyfikaty. Jeśli TLS kończy istniejące reverse proxy, uzgodnij adres Caddy, przekazywane nagłówki i zaufane origins. Nie wystawiaj bezpośrednio portów bazy, Redisa, API ani MCP. Dotychczasowa konfiguracja proxy obsługuje `/api/*`, `/mcp` i `/play/mcp`.

## Dwa MCP

| Endpoint | Token i przeznaczenie |
| --- | --- |
| `/mcp` | Osobisty token autora: odczyt i edycja własnych scenariuszy, także sekretów |
| `/play/mcp` | Token jednej rozgrywki: opublikowane materiały, zapis tur, pamięć, zakończenie |

Nie przekazuj graczowi tokena autora. Token rozgrywki generuje się z panelu aktywności; wygasa po 14 dniach i może być unieważniony. Instrukcje dla agenta są widoczne dla gracza — prywatne sekrety muszą pozostać w odpowiednich polach MG. Szczegóły i przykłady: [instrukcja](docs/pl/individual-play.md), [kontrakt](docs/pl/mcp-play.md).

Oba MCP korzystają ze wspólnych limitów: domyślnie 60 żądań/min na token, dwa jednocześnie na token i osiem globalnie. Redis kontroluje przyjmowanie żądań między procesami; jego awaria blokuje MCP kodem 503. To ustawienia początkowe, nie wynik pomiaru liczby obsługiwanych graczy.

## Dane i kopie zapasowe

Format scenariuszy to `narrative-web/1`, osobny od formatów Unity. Nie ma jeszcze adaptera gwarantującego wymianę z Unity. Przy przypisaniu powstaje zamrożony podzbiór publicznych materiałów `narrative-play/1`.

Pamięć robocza rozgrywki jest tekstem Markdown w PostgreSQL. Jest zapisywana atomowo z dziennikiem, konsekwencjami i wersją. Raport można pobrać jako `.md`; eksport gracza pomija prywatne notatki MG. Narracja i pamięć nie są dowodem zgodności z zasadami — zaakceptowane w ramach aktywności są wyłącznie zatwierdzone wpisy konsekwencji.

Backup z katalogu `web/`:

```sh
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > narrative.dump
```

Przechowuj dump i `.env` bezpiecznie poza serwerem. Odtwarzaj najpierw do zgodnej, pustej bazy w nowej instancji, przy zatrzymanych API/MCP:

```sh
docker compose stop api mcp
docker compose exec -T db sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --exit-on-error' < narrative.dump
docker compose start api mcp
```

Odtworzenie do istniejących tabel wymaga osobnego planu zastąpienia danych. Backup nie jest potwierdzony, dopóki nie sprawdzisz odtworzenia.

## Rozwój i testy

Dla pracy lokalnej: Python 3.11+ zgodny z plikiem zależności, Node 22 i PostgreSQL/Redis dla pełnej integracji. Z `web/`:

```sh
export DEBUG=1
uv venv .venv
uv pip install --python .venv/bin/python -r backend/requirements.lock
.venv/bin/python backend/manage.py migrate
.venv/bin/python backend/manage.py seed_demo
.venv/bin/python backend/manage.py runserver
```

W drugim terminalu uruchom `cd web/frontend && npm ci && npm run dev`. Lokalnie bez konfiguracji PostgreSQL używana jest SQLite. Testy współbieżności wymagają PostgreSQL. MCP uruchamia się osobno z Redisem, z katalogu `backend/`: `../.venv/bin/uvicorn stories.mcp_server:application --port 8001`.

Testy całej instalacji, z głównego katalogu repozytorium:

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/integration.py
docker compose -f web/compose.yaml exec -T api python < web/tests/play_integration.py
```

Zobacz [CONTRIBUTING](../CONTRIBUTING.md), [decyzje architektoniczne](docs/architecture.md) i [zweryfikowane zachowanie](docs/verification.md). Nie opublikowano gwarantowanej pojemności serwera; wymaga ona pomiarów na docelowym sprzęcie.

[Testy odporności i instrukcja odtwarzania](docs/pl/adversarial-testing.md) opisują znalezione błędy, poprawki i izolowane próby awarii. JSON ma limit 32 poziomów i 50 000 wartości. Limit 50 scenariuszy na konto jest egzekwowany transakcyjnie. Migracja 0003 zapisuje odciski żądań tworzenia dla bezpiecznych ponowień; Compose uruchamia ją automatycznie.

[Konfiguracja strony i przygotowanie wdrożenia](docs/pl/website.md) · [English](docs/en/website.md).

## Wiki kampanii

Otwórz `/campaigns/` lub wybierz **Wiki kampanii** w warsztacie. Kampania obsługuje role właściciela, współ-MG i gracza, jednorazowe zaproszenia, publikacje dla wszystkich lub wybranych graczy oraz filtrowane artykuły, wyszukiwanie, relacje i mapę. Materiały można kopiować do różnych przygód z kontrolą wersji, zachowując osobne sekrety MG. Migracje 0004 i 0005 dodają wiki oraz współdzielenie; stare materiały pozostają prywatne. [Instrukcja i kontrakty](docs/pl/campaign-wiki.md).


- [First campaign / Pierwsza kampania — UX](docs/pl/first-campaign-ux.md)
- [Blog, SEO, Analytics](docs/pl/publishing-and-analytics.md)
