---
tags: [design, narrative-framework, obrazy]
status: draft
aliases: ["Obrazy kampanii", "Campaign images"]
---

# Obrazy kampanii — zasady

Obrazy pokazują graczom klimat i to, w co grają: okładka kampanii, ilustracje stron wiki,
galeria oraz obrazki ról i przedmiotów w kreatorze postaci ([[02-architektura-danych]] kreatora).

## Prywatność — zasady nadrzędne

1. **Obraz należy do jednej kampanii** i nigdy nie ma publicznego adresu. Każde pobranie
   przechodzi przez API z aktualnym sprawdzeniem członkostwa; brak dostępu = 404.
2. **Pliki w bazie, nie na dysku publicznym.** Brak katalogu `media/` serwowanego przez proxy;
   kopia bazy obejmuje obrazy (traktuj ją jako poufną).
3. **Odpowiedzi prywatne:** `Cache-Control: private, no-store`, `X-Robots-Tag: noindex`,
   `X-Content-Type-Options: nosniff`, `Content-Disposition: inline`.
4. **Nic nie trafia do repozytorium ani eksportów publicznych.** Eksport wiki zawiera tylko
   identyfikatory obrazów, nie ich treść.
5. **Prawa do obrazów należą do osoby, która je wgrywa.** Aplikacja nie ocenia źródła; pilnuje,
   żeby obraz nie wyszedł poza kampanię.

## Kto widzi obraz

| Kto | Widzi |
|---|---|
| Właściciel, współprowadzący | Wszystkie obrazy kampanii |
| Gracz | Obraz, który: jest w galerii z widocznością „gracze”, jest okładką kampanii, ilustruje stronę opublikowaną temu graczowi, albo jest użyty w pakiecie zasad (kreator) |

Widoczność wynika z kontekstu, więc obraz roboczy MG pozostaje ukryty, dopóki nie zostanie
użyty w czymś, co gracz już widzi. Wycofanie publikacji strony od razu ukrywa jej ilustrację
(o ile nie jest używana gdzie indziej).

## Wgrywanie

- Formaty wejściowe: PNG, JPEG, WebP, GIF (pierwsza klatka). Limit pliku wejściowego 8 MB,
  limit pikseli 40 Mpx (ochrona przed „bombami dekompresji”).
- Serwer **dekoduje i zapisuje obraz od nowa** (WebP, najdłuższy bok ≤ 1600 px, bez metadanych
  EXIF/GPS). Oryginalny plik nie jest przechowywany.
- Limity kampanii: 300 obrazów, 200 MB po przetworzeniu.
- Przeglądarka: formularz w galerii (multipart). MCP: `upload_campaign_image` z danymi base64 —
  limit żądania MCP to 300 KB, więc przez MCP wgrywa się obrazy już pomniejszone.

## Dane

`CampaignImage`: kampania, UUID (klienta — bezpieczne ponowienie), podpis ≤ 200 znaków,
`gallery` (`gm` | `players`), bajty WebP, szerokość, wysokość, rozmiar, data.
Odniesienia: `Campaign.cover`, `WikiPage.image` (opublikowana migawka strony zapamiętuje ID
obrazu), pole `image` w pakiecie zasad (role, przedmioty katalogu, metody) — ID obrazu kampanii;
`set_ruleset` odrzuca pakiet z nieistniejącym ID.

## Usuwanie

Usunięcie obrazu: potwierdzenie z listą miejsc użycia („okładka, 2 strony, pakiet zasad”).
Obraz używany przez pakiet zasad jest zablokowany (najpierw zmień pakiet). Okładka i ilustracje
stron są odpinane automatycznie.

## Otwarte pytania do dograć

- Czy gracze mogą wgrywać obrazy (portret własnej postaci w kreatorze)? v1: nie — tylko MG.
- Czy galeria potrzebuje kategorii/kolejności? v1: kolejność od najnowszych, podpis jako opis.
