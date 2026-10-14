# Pierwsza kampania: projektowanie i sprawdzenie UX

[English](../en/first-campaign-ux.md)

## Cel i podstawa decyzji

MG ma własny pomysł i niewiele czasu przed sesją. Rezultat pierwszego użycia: własna kampania, dwa własne wpisy i nazwana relacja widoczna na mapie. Nie wymaga znajomości Markdowna, MCP ani warsztatu pojedynczych przygód.

Podstawa: wymagania właściciela projektu i przegląd istniejącego interfejsu. To hipoteza projektowa, nie wynik badań z użytkownikami. Zastosowano lokalne skille `ux-researcher-designer` (ścieżka i test użyteczności), `ui-design-system` (spójność i responsywność); wizualnie zachowano istniejący interfejs.

## Zmiana ścieżki

| Moment | Dotychczasowa trudność | Rozwiązanie |
| --- | --- | --- |
| Wejście bez konta | Logowanie prowadziło do warsztatu zamiast z powrotem do kampanii | Stały, ograniczony powrót `next=campaigns` po logowaniu/rejestracji; zaproszenia mają pierwszeństwo |
| Brak kampanii | Pusty widok i formularz w bocznym panelu | Wyjaśnienie rezultatu, przycisk kierujący fokus do nazwy, przykład nazwy |
| Brak wpisów | Wybór spośród wielu kategorii | Dodaj postać / miejsce; kategoria ustawiona, fokus w tytule, jedno zdanie wystarczy |
| Jeden wpis | Brak powodu, żeby utworzyć drugi | Podpowiedź pary: karczmarz i karczma |
| Dwa wpisy | Relacje schowane przy artykule | Formularz od → relacja → do, własne wpisy i własny opis |
| Pierwsza relacja | Konieczność znalezienia mapy | Zapis i otwarcie mapy jednym przyciskiem; kliknięcie punktu otwiera artykuł |
| Powrót | Ryzyko ponownego przechodzenia samouczka | Postęp wyliczany z zapisanych wpisów/relacji; wskazówki można ukryć i ponownie otworzyć |

Wskazówki nie zasłaniają aplikacji, nie tworzą automatycznie przykładowych danych i nie publikują materiałów graczom. Gracze ani podgląd gracza nie widzą narzędzi tworzenia. Istniejące formularze relacji pozostają dostępne. Nie zmieniono układu mapy ani zasad publikacji.

## Weryfikacja techniczna

`web/tests/onboarding_browser.py` przechodzi rzeczywistą ścieżkę w PL i EN: powrót po logowaniu, fokus, zapis obu kategorii, odświeżenie w połowie, własna relacja, mapa, otwarcie wpisu z mapy, ukrycie/przywrócenie wskazówek i szerokość 390 px. Test używa fikcyjnego konta i usuwa je wraz z kampaniami. Nie wysyła danych do GA.

## Test z MG — do przeprowadzenia

Zadanie dla 5 osób, które nie używały narzędzia: „Przygotowujesz sesję w karczmie. Zapisz właścicielkę i karczmę oraz pokaż na mapie, że w niej pracuje. Następnie zmień jej opis”. Nie podawaj nazw przycisków ani instrukcji. Zacznij od strony kampanii, uwzględnij założenie konta.

Zanotuj czas do własnej mapy, ukończenie bez pomocy, pomyłki kierunku relacji, utratę danych i miejsca zawahania. Zapytaj: „Co było niejasne?” i „Czy użyłbyś tego przy przygotowaniu następnej sesji?”. Roboczy cel: co najmniej 4/5 osób kończy bez pomocy, własna mapa w ciągu 3 minut po zalogowaniu, brak utraty danych i przypadkowej publikacji. Są to cele do sprawdzenia, nie osiągnięte wyniki. Automatyczne testy nie dowodzą intuicyjności interfejsu.
