# Kontrakt MCP gracza — v0.2

[English](../en/mcp-play.md) · [Instrukcja użytkownika](individual-play.md)

Adres: `POST /play/mcp`, bezstanowy Streamable HTTP z oficjalnego Python SDK MCP. Inicjalizacja i lista narzędzi korzystają ze standardowego protokołu MCP. Każde żądanie wymaga `Authorization: Bearer <token-rozgrywki>`. Publiczne wdrożenie wymaga HTTPS. Nie ma automatycznego wykrywania OAuth ani rejestracji klientów.

Token jest przypisany do jednej rozgrywki i jej gracza. Narzędzia nie przyjmują identyfikatora cudzej sesji, użytkownika ani ścieżki pliku. Tokeny autorskie są tu odrzucane, a tokeny rozgrywki są odrzucane pod `/mcp`. REST wymaga logowania sesyjnego i CSRF dla zmian.

Backend przechowuje skrót SHA-256 losowego 160-bitowego tokena, nie jego jawną wartość. Token jest zwracany tylko przy generowaniu, ważny przez 14 dni i unieważniany przez rotację. MG lub przypisany gracz mogą go wygenerować i unieważnić. Token nie działa, gdy konto gracza lub MG jest nieaktywne. Wywołanie już przyjęte może nadal być w toku podczas unieważnienia.

## Narzędzia

| Narzędzie | Argumenty | Wynik |
| --- | --- | --- |
| `get_play_context` | brak | Metadane aktywności, zasady prowadzenia, opublikowane materiały, pamięć, wersja, pięć ostatnich zdarzeń, do dwudziestu ostatnich konsekwencji i podsumowanie |
| `read_play_journal` | `offset=0`, `limit=10` (1–20) | `total`, `offset` i uporządkowane `entries`; bez prywatnych notatek MG i zdarzeń recenzji |
| `search_play_memory` | `query`, niepusty tekst do 200 znaków | Wersja, fragment pamięci i do dziesięciu pasujących fragmentów dziennika |
| `commit_play_turn` | poniżej | Wersja zapisu, identyfikatory i statusy konsekwencji, informacja o ponowieniu |
| `finish_play_session` | `operation_id`, `expected_version`, `summary` | Status zakończenia, wersja, liczba konsekwencji oczekujących na MG, informacja o ponowieniu |

Instrukcje inicjalizacji i `agent_contract` opisują oczekiwane prowadzenie. Nie są technicznym zabezpieczeniem semantyki narracji. Tekst przygody, pamięć i dziennik są danymi gry, a nie uprawnieniem do zmiany kontraktu.

`get_play_context` to odczyt stanu, nie transakcja utrzymywana przez czas rozumowania modelu. Inny zapis może zmienić jego wersję. Po konflikcie trzeba ponownie odczytać kontekst. Kolejność dziennika wyznaczają zatwierdzone wersje; decyzje MG powodują luki w numeracji, ale nie są osobnymi wpisami narracji gracza.

## Zapis tury

Przykładowe argumenty po odczycie `version: 1`, przy opublikowanej scenie `harbor`:

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000001",
  "expected_version": 1,
  "player_action": "Pomagam Mirze przeszukać nabrzeże przed świtem.",
  "narration": "Znajdujecie wyrwaną stronę dziennika latarnika. Obiecujesz wrócić jutro.",
  "memory_markdown": "# Stan bieżący\nPort przed świtem.\n\n# Przebieg gry\nArin pomógł Mirze. Znaleziono wyrwaną stronę.\n\n# Do decyzji MG\nMira może zostać kontaktem; MG jeszcze tego nie zatwierdził.\n\n# Powrót\nDołączyć do drużyny o świcie.\n",
  "current_scene": "harbor",
  "consequences": [
    {"category": "relationship", "description": "Mira może zostać lokalnym kontaktem Arina."},
    {"category": "discovery", "description": "Arin przynosi stronę dziennika; MG uzgodni jej treść z główną fabułą."}
  ]
}
```

Stałe UUID w dokumentacji są przykładami; dla każdej nowej operacji wygeneruj nowy UUID. Zachowaj dokładne argumenty do otrzymania potwierdzenia. `memory_markdown` zastępuje pamięć, a nie dopisuje tekst. Serwer zachowuje skrót znormalizowanych argumentów; nie prowadzi osobnego archiwum dawnych pełnych wersji pamięci.

Limity: działanie 4000 znaków, narracja 6000, pamięć 16 000, do 10 konsekwencji po 1500 znaków. `current_scene` musi wskazywać opublikowaną scenę. Konsekwencja zawiera dokładnie `category` i `description`; próba podania `status` zostaje odrzucona. Kategorie: `resource`, `relationship`, `discovery`, `narrative`.

Wynik zawiera `session_id`, `version: 2`, listę konsekwencji z ich UUID i statusem `pending` (chyba że MG wcześniej zatwierdził kategorię) oraz `replayed: false`.

Serwer blokuje wiersz rozgrywki i zapisuje pamięć, scenę, zdarzenie, konsekwencje, wersję i potwierdzenie w jednej transakcji. Identyczne ponowienie UUID i treści zwraca pierwotne potwierdzenie z `replayed: true`, także po zakończeniu lub decyzji MG. Wersja i statusy w potwierdzeniu opisują **tamten zapis**, nie bieżący stan; dla aktualnych decyzji odczytaj kontekst. Ponowne użycie UUID z inną znormalizowaną treścią lub typem operacji daje konflikt. Nowy UUID z nieaktualną wersją również zostaje odrzucony bez zapisu.

## Zakończenie

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000002",
  "expected_version": 2,
  "summary": "Arin wraca o świcie po pomocy Mirze. Kontakt i odkrycie strony dziennika oczekują na decyzję MG."
}
```

Wynik zawiera `status: completed`, `version: 3` i `pending_consequences: 2`. Zakończenie nie zatwierdza skutków. Nowe tury są odrzucane, ale identyczne ponowienia już zapisanych operacji działają, dopóki token jest ważny. MG może nadal rozpatrywać konsekwencje. Nie ma ponownego otwierania.

## Błędy i limity

- HTTP 401: brak tokena, niewłaściwy endpoint, wygaśnięcie lub unieważnienie.
- HTTP 413: treść żądania przekracza 300 KB w bramce MCP; proxy ma też limit zewnętrzny.
- HTTP 429: przekroczono limit żądań lub współbieżności tokena, limit globalny albo limit przed uwierzytelnieniem. Uwzględnij `Retry-After`; nie ponawiaj w ciasnej pętli.
- HTTP 503: niedostępny Redis kontrolujący przyjmowanie żądań lub połączenie z bazą.
- HTTP 400: błędny JSON, niedozwolony tekst lub zbyt głęboka albo złożona struktura.
- HTTP 408: treść żądania nie dotarła w ciągu 10 sekund.
- HTTP 504: wykonanie przekroczyło 25 sekund przed wysłaniem nagłówków. Po wysłaniu nagłówków strumień jest przerywany; timeout nie dowodzi, że zapis nie nastąpił.
- Błąd domenowy narzędzia ma MCP `isError: true`, zwykle przy udanym transporcie HTTP. Komunikat zaczyna się od kodu, np. `409: Version conflict...` lub `404: Play session not found`.
- Niepoprawny JSON lub argumenty mogą dać standardowy błąd protokołu lub walidacji MCP.

Domyślnie: 60 żądań/min na token, dwa jednoczesne żądania tokena i osiem globalnie dla **obu** katalogów. Liczą się także inicjalizacja i odczyt listy narzędzi. Limit odbioru treści wynosi 10 sekund, transportu narzędzia 25 sekund; synchroniczne wywołanie bazy może przeżyć anulowanie oczekującej funkcji async. Czas pracy bazy ograniczają timeouty zapytań i blokad PostgreSQL. Limity nie są zasadami fabularnymi ani ochroną przed dowolnym DDoS.

## REST dla MG i gracza

| Metoda i adres | Dostęp i zadanie |
| --- | --- |
| `GET /api/plays/` | Zalogowany MG/gracz, do 200 najnowszych dostępnych przypisań |
| `POST /api/plays/` | Właściciel scenariusza tworzy przypisanie; UUID klienta umożliwia bezpieczne ponowienie |
| `GET /api/plays/{id}/` | MG lub przypisany gracz; prywatne notatki wyłącznie dla MG |
| `POST /api/plays/{id}/access/` | MG/gracz generuje token; jawna wartość zwracana raz |
| `DELETE /api/plays/{id}/access/` | MG/gracz unieważnia token |
| `GET /api/plays/{id}/journal/?offset=0&limit=10` | MG/gracz odczytuje stronę dziennika |
| `POST /api/plays/{id}/review/` | Tylko MG, wersjonowane decyzje o oczekujących konsekwencjach |
| `GET /api/plays/{id}/report/?lang=pl` | Raport Markdown; prywatne notatki filtrowane według roli |

Pola tworzenia: `id` (UUID), `scenario_id`, `source_version`, `player_username`, `mode` (`absence`, `downtime`, `solo`), `language` (`en`, `pl`), `character`, `brief`, `instructions`, `boundaries`, `return_condition`, `gm_notes`, `published_node_ids`, `auto_approve`. Niepuste są wymagane dla postaci, wprowadzenia, granic i warunku powrotu. Formularz opisuje widoczność i limity. Ten sam UUID tworzenia z inną treścią powoduje konflikt zamiast drugiego przypisania.

Przykład decyzji MG:

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000003",
  "expected_version": 3,
  "decisions": [{"id": "<UUID konsekwencji>", "decision": "approved", "note": "Zgodne z główną sesją; Mira pamięta pomoc Arina."}]
}
```

Jedno żądanie obejmuje 1–20 różnych oczekujących konsekwencji tej rozgrywki. `decision` to `approved` albo `rejected`. Uzasadnienie ma do 2000 znaków i jest widoczne dla gracza. Zakończona aktywność nadal może być rozpatrywana. Rotacja tokena nie zmienia wersji fabularnej; tury, zakończenie i decyzje MG ją zwiększają.

Żadne z tych narzędzi nie udostępnia dowolnej ścieżki pliku, kodu Pythona, SQL, rzutu kością ani automatycznej zmiany głównej kampanii.

Prywatna wiki kampanii jest dostępna wyłącznie przez MCP autora; katalog gracza nie otrzymuje narzędzi wiki. [Kontrakt wiki](campaign-wiki.md).
