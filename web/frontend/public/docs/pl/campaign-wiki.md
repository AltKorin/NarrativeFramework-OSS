# Wiki kampanii: świat dla całej grupy

[English](../en/campaign-wiki.md) · [Rozgrywki indywidualne](individual-play.md) · [Projekt współdzielenia i zakres kolejnych etapów](campaign-sharing-plan.md)

Wiki należy do kampanii. Jedno konto może prowadzić własne kampanie, współprowadzić inne i grać w kolejnych. Artykuły, indeks, mapa i odnośniki zwrotne korzystają z tych samych danych PostgreSQL. Materiały wykorzystasz w wielu przygodach jako jawne kopie.

## Widoki kampanii

MG otwiera kampanię w **Prowadzeniu**. **Oś czasu** pokazuje terminy i blokady,
**Postacie** łączą opisy NPC i graczy z aktualnymi statystykami, a **Materiały świata**
zawierają indeks wiki, edycję, mapę, ustawienia i zaproszenia. Możesz tam rozwinąć
instrukcję **Jak połączyć materiały świata**. Gracz otwiera udostępnione mu materiały.
[Instrukcja prowadzenia](campaign-runtime.md) opisuje podgląd decyzji,
przygotowanie wydarzeń i zakres portu rdzenia Unity.

## Rozpoczęcie i zaproszenia

1. Otwórz pulpit `/workshop/` i wybierz **Nowa kampania**. Zaloguj się lub zarejestruj; po podaniu nazwy trafisz do pierwszych kroków wiki. Istniejącą kampanię otworzysz z listy na pulpicie albo przez **Wiki kampanii** / `/campaigns/`.
2. Utwórz kampanię. Właściciel zmienia jej nazwę/opis i może ją usunąć. Opis kampanii jest widoczny członkom — nie wpisuj tam sekretów.
3. W **Członkowie i zaproszenia** wybierz rolę i utwórz link. Przekaż go prywatnie. Link jest jednorazowy, ważny 7 dni; wykorzysta go pierwsze konto, które świadomie przyjmie zaproszenie. Nie ma wysyłki e-mail ani publicznego katalogu kont.
4. Zaproszony loguje się albo rejestruje i wraca do zaproszenia. Widzi kampanię i rolę, po czym wybiera **Przyjmij zaproszenie**. Samo otwarcie linku niczego nie przydziela. Token pozostaje we fragmencie adresu, nie w parametrze wysyłanym z żądaniem strony.
5. Właściciel widzi, kto dołączył, może unieważnić niewykorzystany link, zmienić rolę lub usunąć członka. Zmiana roli usuwa indywidualne przydziały stron/relacji. Ponowne dołączenie wymaga nowego zaproszenia i nie odtwarza starych przydziałów.

| Rola | Dostęp |
| --- | --- |
| Właściciel | Wszystkie materiały i sekrety, publikacje, członkowie/zaproszenia, ustawienia i usunięcie kampanii |
| Współprowadzący | Wszystkie materiały robocze i sekrety, edycja, publikacje, podgląd gracza, pełny eksport; bez zarządzania rolami i usuwania kampanii |
| Gracz | Tylko opublikowane materiały udostępnione całej grupie albo jemu; odczyt, wyszukiwanie, mapa i własny eksport |

**Współprowadzący widzi wszystkie sekrety MG.** Awans nie jest ograniczonym uprawnieniem do korekty jednego artykułu. Członek może wybrać **Opuść kampanię**; właściciel nie może odejść. Przekazywanie własności oraz edycja artykułów przez graczy nie są jeszcze zaimplementowane.

## Artykuły, szkice i publikacje

Dodawaj postacie, miejsca, organizacje, przedmioty, wiedzę o świecie, dzienniki, sceny i wskazówki. Treść jest Markdown: nagłówki, listy, tabele i formatowanie. HTML nie jest wykonywany; obrazki/załączniki nie są zaimplementowane. Osobne notatki MG są widoczne wyłącznie prowadzącym.

Nowy artykuł jest **tylko dla MG**, bez publikacji. **Zapisz artykuł** zapisuje wersję roboczą. Nie aktualizuje treści widzianej przez graczy. W **Publikacja dla graczy** wybierz odbiorców:

- wszyscy gracze kampanii, również przyszli członkowie z tą rolą;
- wybrani aktualni gracze;
- tylko MG — wycofaj istniejącą publikację przyciskiem **Wycofaj publikację**.

**Opublikuj dla graczy** kopiuje zapisany tytuł, kategorię i treść do osobnej migawki. Notatki MG nie są do niej kopiowane. Panel pokazuje wersję opublikowaną i roboczą. Zmienione teksty robocze stają się widoczne dopiero po kolejnej publikacji. Publikacja i edycja wymagają aktualnej wersji; konflikt 409 wymaga odświeżenia i uzgodnienia zmian. Przed publikacją sprawdź cały tekst, w tym jawnie wpisane nazwy i odnośniki Markdown. Sekret w zwykłej treści nie zostanie automatycznie rozpoznany; użyj notatek MG albo osobnego artykułu dla węższej grupy.

**Podgląd jako gracz** pokazuje dokładnie opublikowany zakres wybranego gracza, bez możliwości edycji. Powrót do **Widok MG** przywraca szkice i sekrety. Przełączanie PL/EN nie tłumaczy ani nie zastępuje napisanej treści.

## Relacje, wyszukiwanie i eksport

Dodaj opisaną relację między zapisanymi artykułami. Prowadzący widzą ją na obu stronach oraz na mapie. Relacja ma osobne ustawienia udostępniania i domyślnie jest prywatna. Jawne artykuły nie ujawniają automatycznie sekretnej zależności. Gracz widzi relację tylko wtedy, gdy udostępniono mu oba końce i samą relację. Publikacja relacji przechowuje opis jako migawkę. Usunięcie/ukrycie artykułu ukrywa jego połączenia. Mapa ma automatyczny układ; nie zapisuje ręcznych pozycji.

Wszystkie widoki są filtrowane przez backend: indeks, liczniki, mapa, odnośniki, wyszukiwanie, bezpośrednie adresy i eksport. Ukryty zasób zwraca 404. Wyszukiwanie zwraca maksymalnie 100 trafień po tytule/treści. Gracz przeszukuje tylko publikacje; szkice i notatki MG nie trafiają do jego wyników. Prowadzący przeszukują szkice, bez notatek MG.

**Eksport kampanii (MG)** to `narrative-wiki/2`, zawierający sekrety, wersje robocze i publikacje. Nie zawiera tokenów i zaproszeń. **Eksport udostępnionych materiałów** to `narrative-wiki-player/1`, z samym aktualnym zakresem czytelnika. Podgląd MG również eksportuje wersję gracza. Markdown artykułu gracza nie zawiera notatek MG. Import całej wiki i import/synchronizacja Kanki pozostają poza zakresem.

## Wykorzystanie w przygodzie

Prowadzący może skopiować zapisany artykuł kampanii do swojej przygody. Sprawdzane są wersje źródła i celu; współprowadzenie kampanii nie daje dostępu do przygód innych kont. Tytuł/treść przechodzą do opisu, notatki MG do `secret`, a `wiki_origin` zapisuje kampanię, stronę i wersję. NPC/miejsce/scena/wskazówka zachowują typ; inne kategorie stają się materiałem/wskazówką. Druga kopia tej samej strony do tej samej przygody zwraca 409. Podłącz węzeł do grafu według potrzeb.

Kopia jest niezależna od dalszych edycji/usunięcia wiki. Odebranie dostępu do kampanii nie usuwa wcześniej utworzonych własnych kopii ani osobno opublikowanych migawek rozgrywki. Publikacja wiki nie publikuje automatycznie materiałów `/play/mcp` i odwrotnie. Gracz nadal otrzymuje wybrane pola przydzielonej przygody, bez `secret` i `wiki_origin`.

## Dostęp, limity i administracja

- Każde żądanie sprawdza aktualne członkostwo. Operacje serializują się na kampanii. Po cofnięciu dostępu oczekujący zapis nie może użyć dawnej roli.
- UI ponownie sprawdza uprawnienia po powrocie do karty i co 30 sekund w widocznej, bezczynnej karcie. Po degradacji usuwa szkice/secrety z widoku; po utracie kampanii czyści jej materiały. Otwarte formularze nie zapisują się automatycznie. API odmawia dostępu od razu, bez czekania na odświeżenie UI.
- Wysłanych już odpowiedzi, pobranych eksportów i pamięci zewnętrznego agenta nie da się cofnąć. Nie traktuj ukrycia w UI jako usunięcia dostarczonych danych.
- Odpowiedzi API mają `Cache-Control: private, no-store`. Dziennik dostępu dla MG pokazuje ostatnie 100 operacji; baza przechowuje aktora, operację, identyfikator i wersję bez treści sekretów/tokenów.
- 20 własnych kampanii, 50 zaproszonych członków i 20 aktywnych zaproszeń na kampanię; maksymalnie 30 utworzonych zaproszeń na godzinę/kampanię. Właściciel jest osobny od limitu członków.
- 1000 artykułów i 2000 relacji/kampanię; tytuł 200, opis 4000, treść i notatki po 12 000, etykieta relacji 80 znaków. Obowiązują też istniejące limity żądań/admission. To limity zasobów, nie obietnica wydajności.
- Migracja 0005 jest addytywna: stare materiały pozostają prywatne i nieopublikowane. 0004 tworzy wcześniejsze tabele wiki. Compose migruje przed startem API/MCP. Kopia PostgreSQL obejmuje członkostwa i publikacje; przechowuj ją jako dane poufne.

## API i MCP

REST wymaga sesji i CSRF przy zapisie. Endpointy opisuje [wersja angielska](../en/campaign-wiki.md#rest-and-mcp). Publikacja przyjmuje `{version, audience, recipients}`; audience to `gm`, `all` lub `selected`, a recipients to UUID aktualnych członkostw graczy. `gm` wycofuje publikację. Zmiana odbiorców tworzy ponownie migawkę aktualnego zapisanego artykułu — sprawdź go przed zatwierdzeniem. Zmiana roli przyjmuje `{version, role}` z wersją kampanii. Odczyty obsługują `?preview=<UUID członkostwa gracza>` wyłącznie dla MG.

Autorskie `/mcp` pozwala właścicielowi i współprowadzącemu wyszukiwać, czytać i zapisywać materiały robocze, wraz z sekretami. Gracz nie uzyska tych uprawnień osobistym tokenem. Rola jest sprawdzana przy każdym wywołaniu. `/play/mcp` pozostaje osobnym katalogiem, bez dostępu do wiki. **MCP czytelnika kampanii `/campaign/mcp` nie jest jeszcze zaimplementowane**; to kolejny etap, niezależny od działającego współdzielenia w przeglądarce.

## Weryfikacja

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/wiki_integration.py
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/sharing_browser.py
```

Testuj na instancji testowej. Skrypty tworzą i usuwają fikcyjne konta. Testy PostgreSQL obejmują równoczesną akceptację linku, publikację wobec edycji i cofnięcie dostępu wobec zapisu. Przeglądarka sprawdza cały przepływ grupy, filtrowane eksporty, podgląd, degradację, cofnięcie dostępu, PL/EN i telefon.

Migracja 0006 zachowuje wartości domyślne nowych pól także w bazie, aby stary kod mógł nadal tworzyć prywatne strony i relacje przy wycofaniu wersji aplikacji. Wycofanie kodu nie wymaga cofania migracji i usuwania danych współdzielenia.

Eksport MG zawiera również prywatny stan `runtime`; eksport gracza i podglądu gracza go pomija. Endpointy silnika i migracja `0007_campaign_runtime`: [kontrakt](../narrative-runtime.md).
