# Plan: wiki dla grupy kampanii

Status: **plan do implementacji**. Opisuje docelowe zachowanie, nie funkcje dostępne w obecnym wdrożeniu. [English](../en/campaign-sharing-plan.md) · [Obecna wiki](campaign-wiki.md).

## Cel i decyzje

MG zakłada kampanię, zaprasza grupę i stopniowo ujawnia jej świat. Ten sam użytkownik może być właścicielem jednej kampanii, współprowadzącym drugiej i graczem trzeciej. Wiki należy do kampanii; konto określa członkostwo i uprawnienia. Obecny model prywatnych artykułów pozostaje bazą, ale współdzielenie jest warunkiem użytecznej pierwszej wersji grupowej.

Pierwsze wydanie grupowe obejmuje zaproszenia, role, artykuły dla wszystkich albo wybranych graczy, bezpieczne publikowanie, filtrowaną mapę/wyszukiwanie/eksport i cofanie dostępu. Samo dodanie przycisku „udostępnij” nie kończy tego zadania. W tej samej zmianie trzeba zabezpieczyć istniejące REST i autorskie MCP; osobny MCP czytelnika jest następnym etapem.

## Role

| Operacja | Właściciel | Współprowadzący | Gracz |
| --- | --- | --- | --- |
| Czytanie materiałów roboczych i notatek MG | Tak | Tak | Nie |
| Tworzenie/edycja artykułów i relacji, publikowanie | Tak | Tak | Nie |
| Czytanie udostępnionych treści i pobieranie ich eksportu | Tak | Tak | Tylko własny zakres |
| Zapraszanie, zmiana ról, usuwanie członków | Tak | Nie | Nie |
| Pełny eksport MG | Tak | Tak | Nie |
| Usunięcie kampanii | Tak | Nie | Nie |
| Opuszczenie kampanii | Wymaga przekazania własności | Tak | Tak |

Właściciel pozostaje pojedynczym `Campaign.owner`; pozostałe role zapisuje członkostwo. W pierwszym wydaniu nie ma przekazywania własności ani usuwania własnego konta z aktywnymi własnymi kampaniami. Nie wolno usunąć/zdegradować właściciela przez API członkostwa. Obie role MG widzą wszystkie sekrety — interfejs musi to jasno powiedzieć przed awansem gracza. Edycja przez graczy, propozycje zmian i dzienniki postaci to osobny późniejszy zakres.

## Widoczność i publikowanie

Artykuł ma odbiorców: **tylko MG**, **wszyscy gracze kampanii** albo **wybrani gracze**. Nowy artykuł domyślnie jest tylko dla MG. Brak publikacji oznacza brak dostępu gracza niezależnie od wybranych odbiorców. Nowy członek z rolą gracza widzi opublikowane materiały dla całej grupy; do artykułu dla wybranych trzeba go dopisać. Cofnięcie dostępu do kampanii ma pierwszeństwo przed zachowanymi grantami stron.

MG edytuje wersję roboczą i osobno wybiera **Opublikuj dla graczy**. Publikacja zapisuje migawkę dozwolonych pól: tytuł, kategorię i treść, ze źródłową wersją. `gm_notes` nigdy nie wchodzi do tej migawki. Gracz widzi ostatnią publikację; zapis roboczy jej nie aktualizuje. Wycofanie publikacji ukrywa stronę. Zmiana odbiorców oraz publikacja wymagają aktualnej wersji, są atomowe i trafiają do dziennika działań. Interfejs pokazuje różnicę „robocza / opublikowana” i pozwala sprawdzić widok konkretnego gracza. Nie wprowadzamy jeszcze ukrytych akapitów; sekret trafia do notatek MG lub oddzielnego artykułu o węższym dostępie.

Relacje również mają zakres tylko MG / wszyscy / wybrani. Dla gracza relacja pojawia się wyłącznie, gdy ma on dostęp do obu opublikowanych końców i samej relacji. Samo ujawnienie postaci i miejsca nie ujawnia sekretnej zależności między nimi. Opublikowany opis relacji jest migawką; edycja robocza nie zmienia go automatycznie. Usunięcie końca lub odebranie dostępu natychmiast ukrywa połączenie.

Ta sama polityka działa w indeksie, wynikach wyszukiwania, licznikach, odnośnikach zwrotnych, mapie, eksporcie, bezpośrednim adresie i MCP. Gracz przeszukuje tylko opublikowaną treść, nigdy szkice ani sekrety. Żądanie ukrytego zasobu zwraca 404. Backend nie wysyła całej kampanii do przefiltrowania przez React. Odnośnik Markdown napisany ręcznie nie nadaje dostępu; jego widoczny tekst jest częścią publikowanej treści i musi być objęty podglądem MG.

## Zaproszenia i cofanie dostępu

Właściciel tworzy link z przypisaną rolą, ważny domyślnie 7 dni. Link jest jednorazowy, może zostać unieważniony; serwer przechowuje skrót losowego tokenu. Nie wymaga wysyłki e-mail — właściciel sam przekazuje link. Zaproszony loguje się lub zakłada konto, widzi nazwę kampanii i proponowaną rolę, a potem świadomie akceptuje. Samo otwarcie linku nie dodaje członka. Link jest zaproszeniem dla posiadacza: UI mówi, że wykorzysta go pierwsze akceptujące konto; właściciel widzi, kto go przyjął. Nie publikujemy katalogu kont.

Przyjęcie jest atomowe: równoczesne próby nie dopiszą dwóch kont. Istniejące członkostwo nie zmienia roli wskutek powtórzenia zaproszenia; awans/degradacja to osobna operacja właściciela. Właściciel może usunąć członka, gracz/współprowadzący może odejść. Powrót wymaga nowego zaproszenia i nie przywraca dawnych indywidualnych grantów.

Uprawnienia są sprawdzane przy każdym żądaniu. Operacje zapisu, zmiany ról, publikacji, akceptacji zaproszenia i revokacji serializują się na kampanii; zapis rozpoczęty po cofnięciu dostępu musi zostać odrzucony. Wcześniej wysłanych odpowiedzi, pobranych plików i pamięci zewnętrznego agenta nie można cofnąć. Odpowiedzi prywatne nie trafiają do współdzielonego cache; odświeżenie/zmiana karty ponownie pobiera uprawnienia i usuwa niedostępne treści z UI. Otwarte karty pokazują utratę dostępu zamiast ponawiać zapis bez końca.

## Model i granice istniejących funkcji

Planowane modele: `CampaignMember` (unikalne campaign/user, rola), `CampaignInvitation` (skrót, rola, termin, akceptacja/unieważnienie), odbiorcy i indywidualne granty strony/relacji, opublikowane migawki stron/relacji oraz `CampaignAuditEvent`. Zdarzenie zawiera aktora, operację, identyfikatory i wersję, bez kopii sekretów i tokenów. Granty wskazują członkostwo, więc usunięcie członka usuwa jego przydziały. Zaproszenia i lista członków mają limity oraz ograniczenie częstotliwości; początkowo 50 członków i 20 aktywnych zaproszeń na kampanię. Nie jest to deklaracja wydajności serwera.

Wspólna warstwa `campaign_access` rozstrzyga rolę i buduje widok odbiorcy. `stories/wiki.py`, `wiki_views.py` i MCP korzystają z niej zamiast samodzielnych filtrów `owner=user`. Serializatory MG i gracza są oddzielne i oparte na jawnej liście pól. Istniejące autorskie narzędzia wiki pozostają wyłącznie dla właścicieli/współprowadzących, nawet jeśli osobisty token należy do gracza danej kampanii.

Kopiowanie do przygody pozostaje jawne: MG może użyć artykułu kampanii, którą współprowadzi, ale zapisuje go tylko do własnej przygody. Członkostwo kampanii nie daje prawa do cudzych scenariuszy ani rozgrywek. Istniejące migawki przygód i przydziałów graczy pozostają niezależne; cofnięcie wiki nie usuwa wcześniej opublikowanych materiałów osobnej rozgrywki. UI i dokumentacja muszą to powiedzieć.

Migracja jest addytywna: dotychczasowi właściciele zachowują dostęp; wszystkie stare artykuły i relacje są tylko dla MG, bez publikacji/grantów. Nic nie staje się publiczne automatycznie. `summary` kampanii traktujemy jako opis dla grupy; sekretne przygotowanie pozostaje w artykułach/notatkach MG. Zmiana kontraktu eksportu wymaga osobnej wersji: pełna kopia MG i eksport gracza mają odrębne schematy, bez tokenów/zaproszeń.

## MCP kampanii

Po wydaniu grupowej wiki: osobny `/campaign/mcp` z tokenem konta ograniczonym do jednej kampanii i odczytu. Token przechowywany jako skrót, z terminem i możliwością odwołania. Narzędzia czytelnika: wyszukiwanie, odczyt strony, widoczne relacje. Zawsze zwracają opublikowany widok gracza, także właścicielowi używającemu tokenu czytelnika. Nie dają zapisu ani notatek MG. Członkostwo i granty są ponownie sprawdzane na każde wywołanie; zmiana roli/unieważnienie członkostwa unieważnia także wydane tokeny kampanii.

Token rozgrywki `/play/mcp` nie zyskuje automatycznie dostępu do kampanii. Połączenie pseudo-MG z wiedzą kampanii wymaga później osobnego, jawnego zakresu postaci/sesji — nie może korzystać z osobistego tokenu MG. Instrukcja modelu „nie ujawniaj sekretów” nie zastępuje kontroli na serwerze.

## Kolejność realizacji i warunki ukończenia

1. **Uprawnienia i dane:** addytywne migracje, polityka dostępu, transakcje, zaproszenia, publikacje, granty, audyt; testy prawdziwego PostgreSQL. Nie wystawiać funkcji częściowo zabezpieczonej.
2. **Cały przepływ grupy:** panel członków i zaproszeń, przyjęcie linku, wybór roli, publikowanie/odbiorcy, widok gracza i podgląd MG, filtrowany graf/wyszukiwanie/eksport, cofnięcie dostępu, PL/EN i telefon. To minimalne użyteczne wydanie, obejmujące punkty 1–2 razem.
3. **MCP czytelnika kampanii:** osobny katalog/token, te same projekcje uprawnień, ograniczenia i testy rzeczywistego protokołu.
4. **Później:** propozycje edycji graczy, prywatne dzienniki postaci, import z Kanki, przekazywanie własności. Każde jako osobny zakres; nie blokują wydania grupowego.

Scenariusz akceptacyjny: właściciel zaprasza współ-MG, Alicję i Boba. Publiczny dla grupy „Port” widzą oboje. „Dług Alicji” widzi tylko Alicja. „Prawdziwa tożsamość burmistrza” i notatki MG widzą wyłącznie prowadzący. Sekretna relacja między dwoma jawnymi stronami nie pojawia się graczom. Zapis roboczy „Portu” nie zmienia ich widoku do ponownej publikacji. Odebranie Alicji dostępu blokuje adresy, wyszukiwanie, eksport i jej token MCP; Bob nadal ma własny zakres. Degradacja współ-MG do gracza natychmiast zmienia projekcję i odbiera sekretne odczyty/zapisy.

Testy muszą dodatkowo objąć: próby samodzielnego awansu, podmianę campaign/page/member UUID, wycieki tytułów/liczników/relacji i szkiców, notatki w eksportach, równoczesną akceptację linku, publikację wobec edycji, zapis wobec usunięcia członka, ponowne zaproszenie bez dawnych grantów, cache po zmianie roli i brak zmiany istniejącego `/play/mcp`. Wdrożenie: kopia bazy, migracja ze wszystkimi starymi treściami prywatnymi, testy dwóch niezależnych kont przez HTTPS, dokumentacja PL/EN i sprawdzony plan wycofania kodu. Przy wycofaniu współdzielenia publikacje pozostają w bazie; stary kod przywraca dostęp tylko właściciela, bez obietnicy dalszej współpracy.
