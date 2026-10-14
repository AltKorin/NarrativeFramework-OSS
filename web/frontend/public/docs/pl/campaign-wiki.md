# Wiki kampanii: jeden świat, wiele przygód

[English](../en/campaign-wiki.md) · [Rozgrywki indywidualne](individual-play.md) · [Kontrakt MCP](mcp-play.md)

Wiki kampanii jest osobnym, trwałym zbiorem artykułów. Świat nie jest wciskany do grafu przygody z limitem 250 węzłów. Lista artykułów, mapa powiązań i odnośniki zwrotne korzystają z tych samych rekordów kampanii, stron i relacji w PostgreSQL.

## Korzystanie z wiki

1. Zaloguj się w warsztacie i wybierz **Wiki kampanii**, albo otwórz `/campaigns/`.
2. Utwórz kampanię. **Ustawienia kampanii** umożliwiają zmianę nazwy/opisu, odświeżenie oraz usunięcie po potwierdzeniu. Usunięcie obejmuje artykuły i relacje, ale zachowuje kopie materiałów w przygodach.
3. Dodawaj postacie, miejsca, organizacje, przedmioty, wiedzę o świecie, dzienniki, sceny i wskazówki.
4. Pisz treść w Markdown; **Podgląd** pokazuje nagłówki, listy, tabele i formatowanie. HTML nie jest wykonywany. Obrazki w treści i przesyłanie załączników nie są zaimplementowane. Sekrety wpisuj w **Prywatne notatki MG**.
5. Dodaj opisane powiązanie z innym zapisanym artykułem. Relacja będzie widoczna na obu stronach (wychodząca/przychodząca) oraz na mapie kampanii. Kliknięcie węzła mapy otwiera artykuł. Pozycje mapy są automatyczne, bez zapisywania ręcznego układu.
6. Wyszukuj po tytułach i głównej treści. Wynik ma limit 100 trafień; prywatne notatki MG nie są indeksowane. Puste wyszukiwanie pokazuje indeks kampanii według kategorii.
7. Pobierz całą kampanię jako JSON `narrative-wiki/1` lub artykuł jako Markdown. Oba eksporty są **kopiami MG zawierającymi prywatne notatki**. Import całej kampanii oraz import/synchronizacja z Kanką nie są jeszcze zaimplementowane.

Pierwsza wersja jest prywatna dla właściciela konta. „Wspólny świat” oznacza ponowne użycie w jego przygodach; nie dodaje jeszcze dostępu graczy do wiki, ról współprowadzącego ani członkostwa kampanii. Przełączanie PL/EN zachowuje teksty i szkice; nie tłumaczy świata.

## Wykorzystanie artykułu w przygodzie

Zapisz artykuł, wybierz własną zapisaną przygodę i kliknij **Skopiuj do przygody**. Backend sprawdza właściciela oraz wersję artykułu i przygody. Równoczesna zmiana kończy się 409, zamiast kopiować niezauważenie inną treść niż ta przejrzana przez MG.

Kopia jest węzłem scenariusza. Metadane `wiki_origin` przechowują identyfikator kampanii, strony i wersję. Tytuł i treść trafiają do tytułu/opisu węzła; notatki MG do jego `secret`. Postać, miejsce, scena i wskazówka zachowują typ. Pozostałe kategorie wiki stają się materiałem/wskazówką, bo obecny format przygody ma cztery typy. Połącz materiał na mapie przygody zgodnie z potrzebą. Inspektor węzła prowadzi z powrotem do źródła w wiki.

Ten sam artykuł można wykorzystać w różnych przygodach. Druga kopia tej samej strony w jednej przygodzie jest odrzucana, również po utracie odpowiedzi. Kopie są świadomymi migawkami: dalsza edycja/usunięcie wiki nie zmienia przygody, przypisania gracza, dziennika ani pamięci. Automatyczne odświeżanie/synchronizacja kopii nie są zaimplementowane.

Publikacja dla gracza nadal wymaga świadomego wyboru materiałów i korzysta z istniejącej listy dozwolonych pól. Kopia wiki nie jest automatycznie publikowana. `secret` i metadane źródła są pomijane w migawce gracza. Sekret wklejony w zwykły opis pozostaje zwykłym opisem — o jego publikacji decyduje MG.

## Zapis, wersje i limity

Migracja `0004_campaign_wikipage_wikirelation` dodaje Campaign, WikiPage i WikiRelation bez zmiany dotychczasowych scenariuszy/rozgrywek. Compose wykonuje migracje przed startem API/MCP.

- 20 kampanii na właściciela, 1000 artykułów i 2000 relacji na kampanię.
- Tytuł: 200 znaków; opis kampanii: 4000; treść artykułu i prywatne notatki: po 12 000; opis relacji: 80.
- Tworzenie przyjmuje UUID klienta. Kampania/artykuł przechowują odcisk pierwotnego żądania: dokładne ponowienie zwraca aktualny rekord, zmienione — konflikt. Taka sama relacja semantyczna zwraca istniejącą relację.
- Zmiany kampanii są serializowane blokadą jej wiersza. Zapis/usunięcie artykułu wymaga jego bieżącej wersji. Ustawienia/usunięcie kampanii wymagają wersji kampanii, zmienianej też przez edycje stron i relacji.
- Nieaktualny zapis zwraca 409; interfejs zachowuje szkic i pyta przed jego odrzuceniem przy odświeżeniu. Ponowienia tworzenia w bieżącej karcie używają tego samego UUID. Niezapisany szkic przeglądarki nie jest kopią zapasową na serwerze. Próba opuszczenia zmienionego artykułu ostrzega przed utratą zmian.
- Obowiązują dotychczasowe limity JSON, tekstu, uwierzytelnienia i przyjmowania żądań. Eksport całej wiki jest tylko dla właściciela i może przekraczać rozmiar pojedynczego żądania; MCP nie pobiera całej wiki bez limitu.

## API i MCP autora

REST wymaga sesji i CSRF przy zapisie. Endpointy i pola szczegółowo opisuje [wersja angielska](../en/campaign-wiki.md#rest-and-author-mcp). Kopiowanie przyjmuje `{scenario_id, version, source_version}` — `version` to wersja przygody, `source_version` to wersja artykułu. PUT artykułu zastępuje `title`, `kind`, `body`, `gm_notes`; pominięte teksty są puste. Cudze zasoby oraz relacje między różnymi kampaniami są odrzucane.

Autorskie `/mcp` ma dodatkowo `list_campaigns`, `search_wiki`, `read_wiki_page`, `save_wiki_page`. Wyszukiwanie zwraca metadane; odczyt jedną stronę wraz z notatkami MG; zapis wymaga oczekiwanej wersji. Przed zapisem odczytaj aktualny artykuł i zachowaj jego prywatne notatki. Narzędzia nie występują w `/play/mcp` — token gracza nie daje dostępu do całego świata.

## Weryfikacja

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/wiki_integration.py
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/wiki_browser.py
```

Uruchamiaj na środowisku testowym. Skrypty integracyjne/przeglądarkowe tworzą tymczasowe konta i sprzątają po sobie. Testy PostgreSQL sprawdzają równoczesne edycje i kolizje UUID różnych właścicieli. Przeglądarka sprawdza Markdown z próbą wstrzyknięcia HTML, uprawnienia, wyszukiwanie, relacje/odnośniki/mapę, zachowanie szkicu przy konflikcie, eksport, sekrety/metadane kopiowania, otwieranie przygody, PL/EN i widok mobilny.

## Plan współdzielenia kampanii

[Plan współdzielenia kampanii](campaign-sharing-plan.md) — plan następnej wersji, jeszcze niezaimplementowany.
