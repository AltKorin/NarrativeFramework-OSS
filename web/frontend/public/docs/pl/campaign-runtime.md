# Prowadzenie kampanii, oś czasu i karty postaci

[English / API i źródła Unity](../narrative-runtime.md) · [Materiały i publikowanie](campaign-wiki.md)

Kampania łączy przygotowanie wydarzeń z decyzjami podczas sesji. Statystyki, fakty
świata i dostępność wydarzeń oblicza deterministyczny port rdzenia narracyjnego
Unity do Pythona, dostępny w tym samym repozytorium na licencji MIT.

## Cztery widoki

| Widok | Do czego służy |
| --- | --- |
| **Prowadzenie** | Dostępne wydarzenia, najbliższe terminy, blokady i zasoby drużyny. Wydarzenia dostępne teraz są uporządkowane według końca terminu. Rozegrane i przeterminowane są zwinięte. |
| **Oś czasu** | Okna dostępności wydarzeń, warunki i archiwum. Odpowiada na pytanie „co i kiedy”, korzystając z dni świata gry. |
| **Postacie** | Karty NPC, postaci graczy i organizacji: opis, aktualne statystyki, relacje liczbowe i powiązane wydarzenia. |
| **Materiały świata** | Artykuły Markdown, mapa powiązań, publikowanie, zaproszenia i ustawienia. Dotychczasowe linki do artykułów nadal działają. |

MG zaczyna w Prowadzeniu. Gracz widzi swoje opublikowane materiały; prywatny stan
silnika i jego podgląd nie są udostępniane graczom. Publikacja opisu postaci nie
publikuje automatycznie jej bieżących statystyk. Kategoria **Kronika (notatki)**
służy do swobodnego opisu historii; reguły i terminy prowadzi osobna Oś czasu.

## Przykład: opłata za przejście przez bramę

1. W Postaciach dodaj NPC Strażnik i postać gracza Bohater. Zapisz ich opisy.
2. Otwórz kartę Strażnika, wybierz utworzenie nowej nazwy statystyki i zapisz
   `zdrowie` = 10. W **Stan świata i statystyki** ustaw zasób drużyny `złoto` = 12.
3. Wybierz **Zaplanuj wydarzenie**. Nazwij je „Zamknięcie bramy”, określ stawkę
   i dni dostępności 2–3 (oba krańce są wliczone).
4. Rozwiń **Warunki i uczestnicy**, zaznacz Strażnika i dodaj warunek jego
   `zdrowie > 0`. Wybieraj istniejące nazwy statystyk z listy.
5. Dodaj rozstrzygnięcie „Zapłać strażnikowi” i konsekwencje: zmiana zasobu
   `złoto` o −5 oraz dodanie faktu `przejście_otwarte`.
6. Przygotuj drugie wydarzenie wymagające tego faktu, również dostępne od dnia 2.
7. Wybierz **Następny dzień**. Podgląd pokaże wydarzenia, które staną się dostępne
   lub przekroczą termin. Zatwierdź przejście do dnia 2.
8. Przy bramie wybierz **Sprawdź skutki: Zapłać strażnikowi**. Zobaczysz złoto
   12 → 7, nowy fakt i odblokowanie drugiego wydarzenia.
9. **Wróć** zamyka podgląd bez zmian. **Potwierdź i zapisz** zapisuje decyzję,
   pokazuje wynik i przenosi rozegrane wydarzenie do archiwum.

Kliknięcie uczestnika wydarzenia otwiera jego kartę bez wychodzenia z prowadzenia.
Wynik i dzień decyzji pozostają w historii po odświeżeniu strony. Ten sam wybór nie
może ponownie naliczyć konsekwencji. Jeżeli inny MG zmieni stan w czasie oglądania
podglądu, zapis starej wersji zostanie odrzucony; odśwież stan i sprawdź skutki ponownie.

## Zakres reguł

Obsługiwane są wymagane i blokujące fakty, okna dni oraz porównania liczbowe zasobów,
wskaźników świata i statystyk postaci. Konsekwencje dodają/usuwają fakty, zmieniają
wartości liczbowe, ustawiają stany logiczne i kierunkowe relacje liczbowe między
postaciami. Zmiana zasobu lub statystyki jest przyrostem; ustawienie relacji podaje
nową wartość bezwzględną. Relacje opisowe z mapy materiałów są osobnym zapisem.

Czas przesuwa się o jeden dzień na polecenie MG. Nie uruchamia sam wydarzeń ani
konsekwencji przeterminowania. Możesz zaplanować osobne późniejsze wydarzenie.
Nie ma cofania decyzji, losowania, wykonywania własnego C# ani automatycznego
scalania konsekwencji przygód indywidualnych ze stanem kampanii. Markdown nie jest
interpretowany jako reguły. Port nie importuje całego formatu scenariuszy Unity.

## Dane, aktualizacja i testy

Migracja `0007_campaign_runtime` dodaje stan JSON do kampanii; istniejące artykuły
pozostają bez zmian. Compose wykonuje migracje przy starcie. Pełny eksport MG
zawiera stan silnika, a kopia PostgreSQL obejmuje go wraz z resztą kampanii.
Postaci użytej przez reguły nie można usunąć ani zmienić w kategorię niebędącą
postacią — jej identyfikator zachowuje powiązania statystyk i wydarzeń.

[Kontrakt techniczny](../narrative-runtime.md) opisuje endpointy, limity, mapowanie
źródeł Unity i uruchamianie testów. [Weryfikacja](../verification.md) oddziela wyniki
sprawdzonych scenariuszy od funkcji pozostających poza zakresem.
