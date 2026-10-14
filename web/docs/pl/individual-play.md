# Indywidualne przygody: downtime, nieobecność i solo

[English](../en/individual-play.md) · [Instalacja](../../README.pl.md) · [Kontrakt MCP](mcp-play.md)

Narrative Framework zachowuje edytor przygód i autorskie MCP. Indywidualna rozgrywka pozwala MG przypisać małą przygodę jednemu graczowi, który rozegra ją z zewnętrznym agentem. Może to być downtime, osobisty wątek albo wyjaśnienie, co robiła nieobecna postać podczas głównej sesji.

Aplikacja dostarcza materiały i trwałą pamięć. Model oraz rozmowę zapewnia Twój klient obsługujący MCP. Ta wersja nie zawiera wbudowanego czatu AI, abonamentu modelu, silnika rzutów ani autonomicznego agenta działającego w tle.

## Trzy obiekty i ich zadania

| Obiekt | Zadanie |
| --- | --- |
| Scenariusz | Edytowalny graf scen, NPC, miejsc i wskazówek autora, także z sekretami |
| Rozgrywka | Zamrożone przypisanie do gracza i postaci, z wybranymi materiałami oraz instrukcjami |
| Raport | Markdown z przebiegiem gry, pamięcią, proponowanymi skutkami i decyzjami MG |

Konto pełni rolę MG dla własnych scenariuszy, a gracza dla przypisanych mu rozgrywek. Nie ma globalnej roli MG. W trybie solo możesz przypisać własną przygodę sobie. Tryby opisują przeznaczenie aktywności; nie implementują różnych mechanik RPG.

## 1. Przygotowanie i przypisanie

1. Otwórz demo, zaimportuj przykład lub napisz scenariusz. Zaloguj się i zapisz prywatną kopię.
2. W sekcji **Między sesjami** wybierz **Przypisz tę przygodę**. Najpierw zapisz zmiany scenariusza.
3. Podaj login istniejącego gracza i opis postaci. Gracze mogą tworzyć konta testowe w warsztacie, jeśli rejestracja jest włączona.
4. Wybierz **Nieobecna postać**, **Downtime** lub **Solo** oraz język przygody.
5. Opisz sytuację początkową, instrukcje dla agenta, granice improwizacji i warunek powrotu. Przycisk **Wstaw przykładowe wprowadzenie** daje tekst do dalszej edycji.
6. Wybierz opisy elementów do udostępnienia agentowi. Scena początkowa jest wymagana. Kopiowane są tylko połączenia między opublikowanymi elementami.
7. Zostaw wszystkie kategorie konsekwencji niezaznaczone, jeśli każda trwała zmiana ma wymagać decyzji MG. Możesz jawnie zatwierdzić z góry wybrane kategorie.
8. Prywatne uwagi wpisz do **Prywatnych notatek MG** i utwórz przypisanie.

Rozgrywka kopiuje wybrane opisy, tytuł i wersję scenariusza oraz instrukcje. Późniejsza edycja lub usunięcie scenariusza nie zmienia już trwającej aktywności. W tej wersji nie można edytować instrukcji i publikacji po przypisaniu — dla zmienionego założenia utwórz nowe przypisanie.

### Co może odczytać gracz

Wszystko dostarczone agentowi w kliencie gracza jest dostępne temu graczowi. **Instrukcje dla agenta nie są tajnym kanałem.** Publikuj wyłącznie materiały, które można ujawnić w tym kliencie. Backend pomija pola `secret`, dodatkowe pola elementów, niewybrane elementy i prywatne notatki MG, korzystając z jawnej listy dozwolonych pól.

Aplikacja nie rozpoznaje spoilerów w zwykłym opisie. Sekret wklejony do publicznego opisu lub instrukcji będzie widoczny. Publikacja jest świadomą decyzją autora; automatyczne wykrywanie spoilerów i dynamiczne odblokowywanie sekretów nie są zaimplementowane.

## 2. Podłączenie agenta gracza

Przypisany gracz loguje się do aplikacji i wybiera aktywność. W sekcji **Podłącz swojego agenta** generuje i kopiuje token rozgrywki. MG również może go wygenerować lub unieważnić; zwykle najlepiej, aby gracz zrobił to sam. Jedna aktywność ma jeden aktywny token ważny przez 14 dni. Nowy token unieważnia poprzedni.

Konfiguracja transportu w kliencie obsługującym Streamable HTTP i własne nagłówki Bearer:

```json
{
  "url": "https://narrativeframework.com/play/mcp",
  "headers": {"Authorization": "Bearer TWOJ_TOKEN_ROZGRYWKI"}
}
```

Lokalny adres to `http://localhost:8080/play/mcp`. To przykład ustawień transportu, nie uniwersalny plik konfiguracji każdego klienta. Nie wklejaj tokena do przygody, rozmowy, repozytorium ani raportu. Wiadomość startowa w panelu go nie zawiera.

Przekaż agentowi wiadomość startową z panelu. Powinien odczytać `get_play_context`, pytać o działania postaci, improwizować w ustalonych granicach i zapisywać znaczące rozstrzygnięcia. Nowa rozmowa lub inny klient mogą wznowić aktywność po odczycie tego samego kontekstu z ważnym tokenem.

Token gracza działa tylko pod `/play/mcp`, z osobnym katalogiem pięciu narzędzi. Nie pozwala używać autorskiego `/mcp` ani uwierzytelniać się w przeglądarkowym REST API. Dotychczasowe autorskie MCP i jego tokeny nadal służą do pisania scenariuszy.

## 3. Rozgrywka i pamięć

`commit_play_turn` zapisuje atomowo działanie gracza, narrację, nową pamięć Markdown, bieżącą opublikowaną scenę i proponowane konsekwencje. Dziennik zachowuje działania i narrację, a pamięć roboczą zastępuje najnowsze krótkie podsumowanie agenta. Powinno ono zachować istotne wcześniejsze fakty. Osobna historia dawnych wersji pamięci nie jest prowadzona.

Pamięć jest Markdownem **przechowywanym jako tekst w PostgreSQL**, a nie luźnym plikiem współdzielonym przez procesy. Jedna transakcja zapisuje pamięć, konsekwencje, wersję i potwierdzenie ponawianej operacji. W każdej chwili można pobrać czytelny raport `.md`; backup korzysta z dotychczasowej procedury PostgreSQL. Nie potrzeba nowego wolumenu plików.

Kontekst zawiera opublikowane materiały, bieżącą pamięć, pięć ostatnich zdarzeń gry i maksymalnie dwadzieścia ostatnich konsekwencji. Starsze wpisy odczytuje się stronami. Wyszukiwanie to dosłowne dopasowanie tekstu bez rozróżniania wielkości liter, nie wyszukiwanie wektorowe. Kontekst opisuje odczytany stan; kolejny zapis może od razu zdezaktualizować jego numer wersji.

Serwer przekazuje agentowi zasady prowadzenia, ale nie zapewnia, że model ich przestrzega. Nie wykonuje rzutów ani nie sprawdza mechaniki systemu RPG. Ustal uczciwie sposób rozstrzygania; agent nie powinien twierdzić, że serwer rzucił kośćmi.

## 4. Zakończenie, decyzje i powrót do drużyny

Agent wywołuje `finish_play_session` z podsumowaniem. Rozgrywka zostaje zakończona i nie przyjmuje nowych tur. Identyczne ponowienia wcześniejszych operacji nadal zwracają ich potwierdzenia. Oczekujące konsekwencje pozostają do decyzji MG także po zakończeniu.

W panelu MG zatwierdza lub odrzuca konsekwencje i może dopisać uzasadnienie widoczne dla gracza. Raport Markdown pomaga uzgodnić powrót postaci z przebiegiem głównej sesji. Eksport gracza pomija prywatne notatki MG; eksport MG je zawiera.

### Co oznacza zatwierdzenie konsekwencji

Kategorie to `resource` (zasoby), `relationship` (relacje), `discovery` (odkrycia) i `narrative` (rozwój fabuły). Agent podaje kategorię i opis, ale nie może sam ustawić statusu zatwierdzenia. Backend zatwierdza wpis tylko wtedy, gdy MG wcześniej dopuścił jego kategorię lub zatwierdzi go później. W przeciwnym razie wpis oczekuje na decyzję albo zostaje odrzucony.

To **rejestr decyzji**, a nie dowód zgodności narracji z założeniami. Model może opisać niedozwolone zdarzenie, błędnie sklasyfikować skutek albo go pominąć. Pamięć, narracja i podsumowanie są niezweryfikowanym zapisem gry. Tylko zatwierdzone wpisy konsekwencji są przyjęte w ramach tej aktywności. Nawet one nie zmieniają automatycznie głównej kampanii, ekwipunku ani karty postaci — tę integrację uzgadnia MG.

Dla ostrożnego prowadzenia pozostaw wszystkie kategorie do decyzji. Dopuszczenie kategorii `resource` obejmuje wszystkie zgłoszone w niej opisy, nie tylko drobne zakupy. Limity liczbowe i semantyczna klasyfikacja nie są zaimplementowane.

## Ograniczenia tej wersji

- Jeden gracz na aktywność; bez jednoczesnej gry wielu osób i anonimowych zaproszeń.
- Wymagane istniejące konto. Jeden token rozgrywki ważny 14 dni; unieważnienie blokuje kolejne wywołania, nie odpowiedź już będącą w drodze.
- Maksymalnie 100 rozstrzygniętych tur, 10 proponowanych konsekwencji w turze i 16 000 znaków pamięci roboczej.
- Brak ponownego otwierania, zmiany gracza, usługi rzutów, automatycznego scalania kampanii, pełnej historii pamięci i edycji systemu plików przez MCP.
- Oba MCP współdzielą globalne limity i PostgreSQL. Oddzielenie od webowego API częściowo izoluje obciążenie, ale nie gwarantuje określonej pojemności.

Zobacz [pełny przykład](../../examples/README.pl.md), [kontrakt MCP](mcp-play.md) i [decyzje architektoniczne](../architecture.md).
