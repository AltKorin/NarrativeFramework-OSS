# Współtworzenie Narrative Framework

[English](../../../CONTRIBUTING.md)

Repozytorium zawiera oryginalne assety Unity i samodzielną aplikację webową. Zmiany webowe nie powinny przepisywać GUID assetów Unity ani obiecywać zgodności importu/eksportu bez adaptera.

## Gdzie pracować

- `Assets/NarrativeFrameworkForUnity/`: oryginalny runtime, edytor i dokumentacja Unity.
- `web/backend/stories/domain.py`: format scenariusza, zapis z wersją, walidacja i eksport autora.
- `web/backend/stories/play.py`: przypisania, dostęp gracza, pamięć, tury, potwierdzenia operacji i decyzje MG.
- `models.py`, `migrations/`: model bazy i migracje.
- `views.py`, `play_views.py`: adaptery HTTP z logowaniem sesyjnym.
- `mcp_server.py`: osobne katalogi MCP autora i gracza oraz wspólne limity.
- `web/frontend/src/`: edytor, panel rozgrywek i słowniki PL/EN.
- `web/tests/`: testy rzeczywistego MCP i przeglądarki.
- `web/docs/`, `web/examples/`: instrukcje, decyzje i kompletny przykład.

## Uruchomienie i sprawdzenie

Skorzystaj z [instrukcji](../../README.pl.md) i plików blokujących wersje zależności. Narzędzia przeglądarkowe i formatter są zależnościami deweloperskimi, nie produkcyjnymi. Dla zmiany modeli dodaj migrację; nie przerabiaj już zastosowanej migracji.

Zmiany domeny sprawdzaj przez `python manage.py test stories`. Testy współbieżności są pomijane na SQLite i muszą przejść na PostgreSQL z Compose przed deklaracją bezpieczeństwa współbieżnych zapisów. Dla MCP i uprawnień uruchom `web/tests/integration.py` oraz `play_integration.py` wewnątrz kontenera API. Dla interfejsu wykonaj `npm run build` i odpowiednie testy Playwright: `browser.py`, `languages.py`, `play_browser.py`. Skrypty tworzą i usuwają tymczasowe konta; używaj instancji testowej.

## Zachowaj kontrakty

- Scenariusz autora i indywidualna rozgrywka są osobnymi obiektami. Kopia przypisania nie śledzi po cichu późniejszych zmian źródła.
- Gracz może odczytać odpowiedzi swojego MCP. Pola sekretów, dodatkowe pola prywatne, nieopublikowane materiały i `gm_notes` nie mogą trafić do kontekstu, wyszukiwania, dziennika ani eksportu gracza. Stosuj jawne listy dozwolonych pól.
- Token rozgrywki nie uwierzytelnia w autorskim MCP ani REST. Przechowuj jego skrót; nie loguj i nie commituj wartości jawnej.
- Tura zapisuje atomowo pamięć, zdarzenie, konsekwencje, wersję i potwierdzenie. Ten sam UUID i znormalizowana treść odtwarzają wynik; inna treść powoduje konflikt. Sprawdzaj wyścigi na PostgreSQL.
- Status konsekwencji ustala serwer, a oczekujące wpisy rozpatruje wyłącznie MG tej aktywności. Narracja nie jest technicznie zweryfikowanym kanonem kampanii. Nie obiecuj semantycznego egzekwowania zasad ani automatycznego scalania kampanii.
- Rozwijaj interfejs i instrukcje po polsku oraz angielsku. Identyfikatory i format danych pozostają niezależne od języka. Przełączenie interfejsu nie może zmieniać treści autora.

## Opis zmian

Opisz konkretną sytuację użytkownika, wynik, zmienioną granicę systemu i faktycznie wykonane testy. Aktualizuj instrukcje PL/EN, kontrakt narzędzi MCP i decyzje architektoniczne. Odróżniaj działające funkcje od planów; test funkcjonalny nie dowodzi pojemności serwera.

Nie commituj `.env`, baz, sekretów, prawdziwych kampanii, wyników builda ani `node_modules`. Przykłady powinny być fikcyjne i nadawać się do dystrybucji. Obowiązuje główna licencja MIT z opisanymi wyłączeniami marki.
