# Poradniki, SEO i opcjonalna analityka

[English](../en/publishing-and-analytics.md)

## Dodawanie artykułu

Sekcja `/pl/blog/` i `/en/blog/` jest generowana podczas builda, bez osobnego CMS-a i serwera Node. Aktualizacja to zmiana plików w repozytorium i przebudowanie frontendu.

1. Dodaj metadane do `web/frontend/src/content/posts.json`: unikalne `id`, `date` (pierwsza publikacja), `updated` (rzeczywista aktualizacja) oraz polskie/angielskie `slug`, `title`, `description`, `category`.
2. Dodaj `web/frontend/src/content/posts/<id>.pl.md` i `.en.md`. Treść pisz jako Markdown; tytuł H1 pochodzi z metadanych, sekcje zaczynaj od `##`. Nie dopisuj fikcyjnych wyników/testymoniali. Lokalne obrazy mają ścieżki `/images/`.
3. Zachowaj stare slugi. Zmiana opublikowanego adresu wymaga przekierowania; sama zmiana tytułu nie wymaga nowego URL.
4. Uruchom `npm --prefix web/frontend run build`. Build sprawdza kompletność tekstów i generuje statyczny HTML, canonical/hreflang, Open Graph, BlogPosting/BreadcrumbList JSON-LD, sitemapę oraz RSS.
5. Sprawdź `python3 web/tests/content_http.py` i `web/tests/blog_browser.py` na lokalnym Compose. Testy obejmują również prawdziwe 404 i treść bez JavaScriptu.

Sitemap: `https://narrativeframework.com/sitemap.xml`. RSS: `/pl/feed.xml` i `/en/feed.xml`. Prywatna aplikacja nie trafia do sitemapy; HTML aplikacji ma noindex, a API nadal wymaga uprawnień. Robots/noindex nie zastępują autoryzacji. Nieistniejące strony zwracają HTTP 404 zamiast 200 z landingiem. `www.narrativeframework.com` przekierowuje 301 na domenę główną z zachowaniem adresu.

## Search Console i promocja

W Google Search Console dodaj/zweryfikuj domenę i zgłoś `/sitemap.xml`. Sprawdzaj indeksowanie i zapytania; wgranie plików nie gwarantuje pozycji ani terminu indeksacji. Publikuj kolejne teksty odpowiadające na pytania MG, z przykładami i uczciwym opisem ograniczeń.

Pierwsze poradniki dotyczą wiki bez spoilerów, downtime i własnej instalacji. Udostępniaj konkretny poradnik zamiast samego hasła „nowe narzędzie AI”. W społecznościach stosuj ich zasady promocji. Szkice komunikatów są poniżej; nie wysyłamy nic automatycznie.

**Propozycja PL:** „Udostępniłem Narrative Framework na MIT: graf przygód RPG, wspólna wiki z publikacjami dla wybranych graczy i indywidualne downtime przez zewnętrznego agenta MCP. Można przetestować demo bez konta albo postawić własny Docker. Opisałem też, jak prowadzić wiki bez spoilerów: https://narrativeframework.com/pl/blog/wiki-kampanii-rpg-bez-spoilerow/. Kod: https://github.com/AltKorin/NarrativeFramework-OSS. Projekt jest w fazie testowej; chętnie zbiorę uwagi MG.”

Do linków promocji można dodać kontrolowane etykiety:

`?utm_source=community&utm_medium=post&utm_campaign=campaign_wiki`

Obsługiwane wartości: source `github`, `community`, `social`; medium `post`, `profile`, `link`; campaign `public_preview`, `campaign_wiki`, `downtime`. Kod przekazuje tylko te jawne etykiety, bez dowolnych parametrów URL. GA nadal wymaga zgody. Brak zgody oznacza brak pomiaru, więc wyniki nie obejmują wszystkich odwiedzających. Ruch z wyszukiwarki oceniaj także w Search Console; własny pomiar nie przekazuje referrera.

## Konfiguracja GA4

Domyślne `web/frontend/public/analytics-config.json` ma puste identyfikatory i listę hostów. Własna instalacja niczego nie wysyła do konta twórcy projektu. Ustaw własne wartości świadomie:

```json
{
  "landingMeasurementId": "G-YOURPUBLICID",
  "appMeasurementId": "G-YOURAPPID",
  "appManualOnlyConfirmed": false,
  "allowedHosts": ["twoja-domena.example", "app.twoja-domena.example"]
}
```

Publiczny landing, poradniki i polityki korzystają z identyfikatora publicznego. `/workshop/` i `/campaigns/` mają osobny identyfikator, aktywny wyłącznie po ustawieniu `appManualOnlyConfirmed: true`. **Zanim włączysz tę flagę, wyłącz Pomiar zaawansowany w strumieniu aplikacji w panelu GA4.** Zmiany w panelu Google nie są wykonywane przez ten kod. Automatyczne śledzenie formularzy, plików czy historii aplikacji mogłoby zbierać prywatny kontekst.

W publicznym wdrożeniu przygotowano ID aplikacji, ale pomiar pozostawiono wyłączony na życzenie operatora do czasu tej konfiguracji. Identyfikatory pomiaru są publiczną konfiguracją, nie sekretami. Nie umieszczaj tu kluczy Measurement Protocol ani API.

Nawet aktywna konfiguracja ładuje tag dopiero po zgodzie (basic consent mode). Odmowa jest równoważnym przyciskiem, działa też zmiana decyzji. Preferencja w localStorage jest ważna 180 dni osobno na każdym hoście. Wycofanie zgody wyłącza tag, usuwa dostępne cookies `_ga` i przeładowuje stronę, aby usunąć nasłuchy Google. Usunięcie preferencji przez przeglądarkę przywraca pytanie o zgodę.

Kod używa ręcznego page_view bez duplikatu automatycznej konfiguracji. Publiczne zdarzenia przycisków to `open_workshop`, `sign_up_click`, `github_click`, `support_click`. To kliknięcia, nie potwierdzone rejestracje czy płatności. Aplikacja przekazuje tylko ogólną nazwę obszaru i adres bez ID/parametrów/fragmentu; nie dodaje loginów, tytułów kampanii, treści, zaproszeń ani raportów. Sygnały i personalizacja reklam są wyłączone. Testy interceptują bibliotekę Google i sprawdzają kolejkę konfiguracji/zdarzeń, bez wysyłania syntetycznego ruchu do raportów. Nie potwierdzają dostarczenia danych w panelu GA; to trzeba sprawdzić w danej właściwości po dobrowolnej zgodzie rzeczywistej wizyty.

CSP dopuszcza jedynie potrzebne domeny tagu/pomiaru Google; nie włącza `unsafe-eval` ani skryptów inline. Polityki prywatności i cookies opisują faktyczną konfigurację. Przy aktualizacji źródeł zachowaj `.env`, `site-config.json` i własny `analytics-config.json`; pominięcie ostatniego przy ekstrakcji chroni ustawienia instancji. Sam plik analityki jest pobierany z `cache: no-store`, więc zmiana nie wymaga przebudowania Reacta.

Źródła techniczne: [basic consent mode](https://developers.google.com/tag-platform/security/guides/consent?consentmode=basic), [ręczne pageviews](https://developers.google.com/analytics/devguides/collection/ga4/views), [CSP](https://developers.google.com/tag-platform/security/guides/csp), [Article JSON-LD](https://developers.google.com/search/docs/appearance/structured-data/article).


W Dockerze konfiguracja jest kopiowana do obrazu: po zmianie pliku przebuduj usługę `web` (`docker compose up --build --no-deps -d web`).


## Infografiki i materiały do udostępniania

Na stronie głównej są dwie serie PL/EN: pierwsza kampania i sekrety/publikacja. Podglądy oraz PNG do pobrania znajdują się w `web/frontend/public/images/social/`. Źródła `.pcraft`, PSD, ilustracje i opisy są w [marketing/social](https://github.com/AltKorin/NarrativeFramework-OSS/tree/main/web/marketing/social). Teksty i kształty edytuje się jako warstwy; po zmianie wyeksportuj grafikę ponownie i przebuduj frontend. Zachowaj pliki dla obu języków, wymiary 1080 × 1350 oraz sens opisów alternatywnych i tekstowego podsumowania na stronie. Ilustracje są koncepcyjne, nie są zrzutami interfejsu.
