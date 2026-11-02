# Audyt wiki kampanii: droga do pierwszej gry

Data: 2026-11-02. Kod: `63d6866`, gałąź `main`. Status: dwie rundy audytu zakończone w zakresie opisanym poniżej; propozycje do wyboru właściciela. Bez zmian w kodzie aplikacji.

## Kryterium produktu

Interaktywny podręcznik prowadzi do gry: krótkie wprowadzenie, jedna decyzja naraz, kolejne kroki z wyjaśnieniem reguł w miejscu wyboru. Nowy gracz nie musi wcześniej znać systemu. MG zaczyna od wyboru lub przygotowania kampanii. Materiały świata są dostępne na żądanie. Styl systemu zmienia oprawę, zachowując znajomą obsługę.

## Metoda i granice

Lokalny stos Docker zbudowany i uruchomiony. Fikcyjne konta: MG i dwóch graczy; pakiet testowy `pack()` oparty na `mini()`. Konta usunięte po przejściach. Zrzuty i tekst ekranów lokalnie: `/tmp/nf-audit/`, poza repozytorium.

Oba wyglądy, obie role, 1440×1000 i 390×844: wejście do kampanii, wszystkie główne zakładki, otwarcie przewodnika. Dodatkowo gracz w obu wyglądach i rozmiarach: trzy opublikowane strony, relacja, artykuł, mapa, cztery kroki kreatora. Przejścia bez zgłoszonych błędów JavaScript. To obserwacja UI, nie pełny test poprawności funkcji.

Druga runda: 76 dodatkowych zrzutów w `/tmp/nf-audit/followup/`, log pomiarów przewodnika `log.json`. Ustawienia MG, wszystkie kroki przewodnika kampanii (7 MG, 4 gracz), udostępniony obraz, powiększenie i zamknięcie Escape oraz wyłączenie/włączenie drużyny: oba style i oba rozmiary. Karty przewodnika mieściły się w wysokości badanego okna. Brak zgłoszonych błędów JavaScript w czterech parach sesji MG/gracz. Skrypt kończy się `PASS audit followup`.

Pełna droga postaci w przeglądarce: Cyberpunk 390×844, pakiet Punkty; wybór roli, statystyki 7/4, umiejętności 4/4/4/4, imię, zapis, wysłanie, zatwierdzenie przez MG, ponowne wejście gracza i otwarcie karty. Świeże konto bez kampanii: oba rozmiary. Użytkownicy testowi usunięci także po nieudanych przebiegach.

Ograniczenia: wyłączenie drużyny sprawdzono przełącznikiem kampanii, nie restartem stosu z globalnym PARTY_PAGE=0. Obraz testowy 1×1 sprawdza przesyłanie, widoczność i dialog, ale nie jakość układu dużych ilustracji ani różnych proporcji. Pełnego cyklu zatwierdzania nie powtarzano w każdej kombinacji stylu i ekranu; ścieżka odesłania do poprawy pozostaje do testu przy implementacji. Przewodnik kreatora nie był osobnym pełnym przebiegiem. To audyt ekspercki, nie badanie z początkującymi graczami.

Nie uruchamiano bramek regresji 1–4: nie implementowano poprawek. Nie oceniano jakości treści rzeczywistego pakietu zasad na podstawie miniaturowego pakietu testowego.

## Problemy i propozycje

F/C = Fantasy i Cyberpunk. S: zmiana lokalna; M: kilka widoków i testy; L: nowa ścieżka produktu, możliwe rozszerzenie danych.

| ID | Ekran / rola | Styl / rozmiar | Problem użytkownika i dowód | Propozycja | Koszt | Priorytet |
|---|---|---|---|---|---|---|
| A1 | Wejście / gracz | F/C, oba | `loadCampaign` wybiera materiały; przy braku publikacji ekran kończy się informacją o oczekiwaniu na MG. Brakuje głównego następnego działania. | Start zależny od postępu: wprowadzenie i Stwórz postać / Kontynuuj / Popraw / Otwórz kartę. Brak zasad wyjaśnia oczekiwanie. | L | P0 |
| A2 | Materiały / gracz | F/C, oba | Wybór i tworzenie nowej kampanii, opuszczenie, eksport oraz wyszukiwanie konkurują z czytaniem. Na telefonie poprzedzają materiał. | Przenieść zarządzanie kampaniami do osobnego miejsca; czytanie zaczynać od treści i spisu treści. | M | P0 |
| A3 | Lista postaci / gracz | F/C, szczególnie 390 | Cztery etapy „Twojej drogi” zajmują większość wysokości. Pierwszy przycisk odsyła do świata; kontynuowanie szkicu jest przy dolnej krawędzi ekranu, nowe metody niżej. | Pokazać bieżący etap i jedno główne działanie; całą drogę udostępnić pomocniczo. | M | P0 |
| A4 | Nawigacja / obie role | F/C, 390 | Drużyna częściowo poza widocznym paskiem, galeria i pomoc dalej. Fantasy: początek Drużyny x=323, koniec x=408; Cyberpunk: Ekipa x=327–392. Kontener kończy się wcześniej niż ekran. | Zaprojektować kompaktową, jawnie dostępną nawigację mobilną; sprawdzić z flagą i bez niej. Nie zawijać bez pomiaru wysokości prowadzenia. | M | P1 |
| A5 | Nowa kampania / MG | F/C, oba | Pusta kampania otwiera dzień, stan świata i zdarzenia. Widoczne dwa przyciski planowania wydarzenia; brak całościowej drogi przygotowania gry. | Osobna droga przygotowania: kampania, zasady, wprowadzenie, gracze, sesja; powrót do aktualnego etapu. | L | P0 |
| A6 | Metoda tworzenia / gracz | F/C, oba | W testowym pakiecie wybory „Pełna” i „Punkty” nie wyjaśniają różnicy ani skutku. | Opcjonalny opis i rekomendowana metoda w pakiecie; jedna metoda pomija wybór. Zweryfikować kontrakt danych przed implementacją. | M | P1 |
| A7 | Kreator / gracz | F/C, oba | Obok bieżącego kroku pokazane są karta, lista wszystkich braków, wysłanie i usunięcie. Instrukcja rozdzielania punktów nie tłumaczy skutku wartości cechy w grze. | Skupić ekran na bieżącym wyborze, dać przykłady systemowe; podgląd karty pomocniczy, wysyłka na podsumowaniu. | L | P1 |
| A8 | Przewodnik / obie role | F/C, oba | Definicja `campaignTour` pomija drużynę i przede wszystkim objaśnia zakładki. | Uzupełnić drużynę, a główne prowadzenie oprzeć na wykonywaniu zadania i zapisanym postępie. | S/M | P1 |
| A9 | Artykuł / gracz | F/C, oba | Wersja, odświeżanie, Markdown i odnośniki zwrotne konkurują z krótką treścią. | Hierarchia podręcznika: tytuł, treść, powiązane materiały; operacje techniczne w menu. | M | P2 |

## Dodatkowe ustalenia z drugiej rundy

| ID | Ekran / rola | Styl / rozmiar | Problem i odtworzenie | Propozycja | Koszt | Priorytet |
|---|---|---|---|---|---|---|
| A10 | Ustawienia / MG | F/C, oba | Formularz jest częścią bocznego spisu materiałów. Nawet na komputerze zapis wymaga przewijania długiego, wąskiego formularza. | Własny widok ustawień dostępny bez przechodzenia przez materiały. | M | P1 |
| A11 | Galeria / gracz | Potwierdzone F, 1440 | Gracz ma otwartą galerię, MG dodaje i udostępnia obraz. Ponowne kliknięcie aktywnej zakładki nie pobiera listy; wyjście do materiałów i powrót pokazuje obraz. | Jawne odświeżanie lub odświeżenie po powrocie do okna; nie wymagać znajomości sposobu przeładowania komponentu. | S/M | P1 |
| A12 | Galeria → ustawienia / MG | Potwierdzone F, 1440 | MG dodaje obraz, przechodzi do ustawień i zapisuje zmianę. Pojawia się konflikt wersji kampanii mimo braku drugiego autora. Przycisk odświeżenia przenosi do Prowadzenia. | Synchronizować wersję po operacji i zachować bieżący widok podczas odświeżania; zachować ochronę przed prawdziwym konfliktem. | M | P1 |

## Kierunek wizualny

Zachować wspólną strukturę i zmienne `--campaign-*`. Najpierw uporządkować drogę i hierarchię treści. Potem opracować oprawę podręcznika dla Fantasy i Cyberpunku: typografię, ramki zasad, ilustracje i nawigację rozdziałów. W tej rundzie nie zmieniano ani nie dobierano palety; przyszły dobór wymaga Słownika Kolorów. Nie dodawać zależności.

## Proponowana kolejność

1. A1 + A3: wejście gracza i następny krok na podstawie stanu postaci.
2. A5: przygotowanie kampanii przez MG.
3. A2 + A4: dostępna nawigacja i spokojny widok materiałów.
4. A6 + A7 + A8: zrozumiałe decyzje i pomoc w kontekście.
5. A9 i oprawa podręcznika dopasowana do systemu.

Przed implementacją właściciel wybiera zakres. Każda poprawka osobno, z testem zachowania. Kryteria: nowe wejście prowadzi do właściwej akcji, postęp przeżywa odświeżenie, brak zasad nie daje ślepej uliczki, zatwierdzona karta jest łatwo dostępna, pierwsza decyzja sesji MG nadal mieści się w 844 px. Pełne bramki z briefu obowiązują po zmianach.

## Konkretny zakres pierwszej poprawki do wyboru

Rekomendacja: A1 + A3, bez przebudowy całej aplikacji naraz.

- Wejście gracza pokazuje krótki opis kampanii i jedno działanie zależne od stanu: Stwórz postać, Kontynuuj, Popraw postać, Otwórz kartę. Oczekiwanie na zasady lub decyzję MG jest jawnym stanem.
- Materiały świata i drużyna pozostają dostępne jako pomocnicza nawigacja.
- Lista kreatora pokazuje bieżący krok, a pełny opis drogi jest rozwijany na żądanie.
- Na telefonie główne działanie mieści się w pierwszym ekranie 390×844. Kontynuacja działa po ponownym wejściu i odświeżeniu.
- Testy obejmą brak zasad, brak szkicu, szkic, odesłanie, oczekiwanie i zatwierdzoną kartę; oba style i brak drużyny. Opis kampanii nie zastępuje stron zasad ani nie wymaga przeczytania historii świata.

To propozycja do zatwierdzenia, nie wykonana implementacja.

## Co już działa i warto zachować

- Świeże konto MG dostaje zwięzły komunikat „Zacznij od kampanii” i przycisk kierujący do nazwy. Problem A5 dotyczy następnego etapu, po wejściu do pustej kampanii.
- Zapisane wybory tworzą postać, wysyłka i zatwierdzanie prowadzą do dostępnej graczowi karty.
- W kreatorze istnieją wyjaśnienia „co robisz / dlaczego”, powrót między krokami i sygnalizacja braków. Potrzebna jest lepsza hierarchia i treść systemowa.
- Udostępniony obraz daje się otworzyć i zamknąć klawiszem Escape; przełącznik drużyny usuwa zakładkę u gracza po odświeżeniu.
- Po zatwierdzeniu nadal dominuje „Poznaj świat”, a na telefonie treść otwartej karty zaczyna się poniżej pierwszego ekranu przez panel zarządzania. Wzmacnia to priorytet A1–A3.

## Powtarzalność i pułapki audytu

Pierwszy przebieg zatrzymał się na otwartej wcześniej galerii gracza: dodany przez MG obraz pojawił się po przejściu do innej zakładki i powrocie (A11). Kolejny wykrył konflikt wersji przy zapisie ustawień po dodaniu obrazu (A12). Obsługa odświeżenia w skrypcie wymagała ponownego wejścia w Materiały świata, ponieważ interfejs przenosi do Prowadzenia. Końcowy przebieg przeszedł po wykonaniu tych kroków przez widoczne kontrolki. Obejścia nie są poprawkami aplikacji.
