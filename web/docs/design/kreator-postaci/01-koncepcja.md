---
tags: [concept, design, narrative-framework, kreator-postaci]
status: draft
aliases: ["Kreator postaci", "Character builder"]
---

# Kreator postaci — koncepcja

## Co i dlaczego

Gracz tworzy postać **sam, w swoim panelu kampanii**, prowadzony krok po kroku przez zasady
konkretnego systemu RPG. MG ogląda gotowy szkic, zatwierdza albo odsyła z uwagą. Zatwierdzona
postać staje się kartą `pc` w wiki kampanii i aktorem w silniku kampanii (statystyki widoczne w
**Postaciach** i używane przez wydarzenia).

Silnik nie zna żadnego systemu RPG. Zna tylko pojęcia: **statystyka, umiejętność, wartość
pochodna, rola, tabela losowa, pula punktów, szablon**. Konkretny system (np. Cyberpunk RED,
Daggerheart, system autorski) to **pakiet zasad** — plik danych, który MG wgrywa do kampanii.
Zmiana systemu = zmiana pliku, zero zmian w kodzie. Pakiety zasad komercyjnych systemów są
prywatne dla kampanii i nie trafiają do repozytorium.

## Sprzęt i wszczepy (od pierwszej wersji)

Pakiet zawiera **katalog**: broń, pancerze, ekwipunek, ubrania i wszczepy z ceną, kosztem w
**pulach** (np. Humanity), slotami i wymaganiami (opcja wymaga wszczepu bazowego z wolnym slotem).
Kreator pilnuje budżetu, slotów i wymagań, liczy utratę z pul (stałą albo rzutem serwera) i
pokazuje, jak spada wartość zależna (np. empatia). Pakiety ról mogą dawać gotowy sprzęt z
wyborem „jeden z”.

## Pętla gracza

1. Otwiera kampanię → **Stwórz postać** (widoczne, gdy kampania ma pakiet zasad).
2. Wybiera metodę tworzenia oferowaną przez pakiet (np. szybka z szablonu, półlosowa, pełna).
3. Przechodzi kroki metody — na każdym widzi: co wybiera, ile punktów zostało, co jest
   jeszcze niepoprawne. Rzuty kośćmi wykonuje **serwer**; wynik jest zapisany i nie da się go
   „przelosować odświeżeniem”.
4. Widzi podsumowanie karty (statystyki, pochodne, umiejętności, historia z tabel) → **Wyślij do MG**.
5. Dostaje decyzję: zatwierdzona (karta w kampanii) albo odesłana z uwagą (poprawia i wysyła ponownie).

## Pętla MG

1. Wgrywa pakiet zasad do kampanii (UI albo MCP), widzi jego podsumowanie i błędy walidacji.
2. Na pulpicie kampanii widzi **Postacie do zatwierdzenia** z licznikiem.
3. Otwiera szkic: karta + historia rzutów i wyborów → **Zatwierdź** / **Odeślij z uwagą**.
4. Może przypiąć zatwierdzoną postać do gracza i opublikować mu jej kartę.

## Zasady projektowe

- **Silnik ogólny, treść w danych** — żadnych nazw z konkretnego systemu w kodzie.
- **Walidacja po stronie serwera** — UI podpowiada, serwer rozstrzyga. Niepoprawny szkic nie
  może zostać wysłany do MG.
- **Rzuty i wybory jako zdarzenia** — szkic to log zdarzeń (wybór metody, rzut, przydział
  punktów, wybór szablonu); stan karty jest z nich wyliczany. MG widzi, *co się wydarzyło*.
- **Prostota dla gracza** — jedno działanie na ekran, język systemu z pakietu (np. „STATY”,
  „Rola”), licznik pozostałych punktów zawsze widoczny.
- **Pełne CRUD od pierwszej wersji** — gracz: lista swoich szkiców, nowy, edycja, usunięcie
  szkicu przed zatwierdzeniem; MG: pakiet zasad dodaj/podmień/usuń, szkice zatwierdź/odeślij.

## Poza zakresem pierwszej wersji

- Rozwój postaci po grze (punkty doświadczenia).
- Edytor pakietu zasad w UI (v1: import pliku JSON + walidacja).

Architektura danych: [[02-architektura-danych]].
