# First campaign — social infographic / infografika

- [Polski PNG](first-campaign-pl.png)
- [English PNG](first-campaign-en.png)

Portrait 4:5 artwork for social posts; upload the original PNG without cropping. Review the platform's preview before posting. These are explanatory illustrations, not screenshots of the interface. The graph depicts a directed relationship, not a geographical map. No automatic AI generation inside the application is implied.

Created with the built-in `image_gen` tool on 2026-10-14. Exact prompts: [generation-prompts.json](generation-prompts.json). Illustrations and wording were visually reviewed, including Polish diacritics, arrow direction and domain spelling. The PL/EN compositions are localized variants, not pixel-identical translations. Brand use remains governed by the repository's BRANDING.md. No external posts were published.

## Propozycja posta PL

Masz pomysł na kampanię? Zacznij od postaci i miejsca. Połącz je własną relacją i zobacz, jak powstaje mapa Twojego świata.

Narrative Framework to otwarty warsztat RPG: wiki kampanii, mapa powiązań i materiały udostępniane graczom. Interfejs po polsku i angielsku. Możesz przetestować naszą instancję albo uruchomić własną w Dockerze.

https://narrativeframework.com

Repozytorium: https://github.com/AltKorin/NarrativeFramework-OSS

Opis alternatywny: Infografika Narrative Framework pokazuje cztery kroki: nazwanie kampanii, dodanie postaci i miejsca, utworzenie własnej relacji oraz obejrzenie mapy. Na przykładzie postać Mira jest połączona strzałką z karczmą; relacja brzmi „pracuje w”. Na dole adres narrativeframework.com oraz informacja Open Source i PL/EN.

## Suggested English post

Have an idea for a campaign? Start with a character and a place. Add your own relationship and watch your world's map take shape.

Narrative Framework is an open-source RPG workshop with a campaign wiki, a relationship map and material you can share with players. Available in English and Polish. Try our hosted instance or run your own with Docker.

https://narrativeframework.com

Repository: https://github.com/AltKorin/NarrativeFramework-OSS

Alt text: A Narrative Framework infographic shows four steps: name a campaign, add a character and place, create a custom relationship, and view the map. The example connects Mira to The Inn with a directed arrow labeled “works at”. The footer contains narrativeframework.com, Open Source and PL/EN.

## Edytowalne projekty Photocraft / Editable projects

Do ręcznych zmian otwieraj plik **.pcraft**, a nie spłaszczony PNG:

| Język / Language | Photocraft | PSD | PNG 1080 × 1350 |
| --- | --- | --- | --- |
| PL | [Projekt](editable/first-campaign-pl.pcraft) | [PSD](editable/first-campaign-pl.psd) | [Eksport](editable/first-campaign-pl.png) |
| EN | [Project](editable/first-campaign-en.pcraft) | [PSD](editable/first-campaign-en.psd) | [Export](editable/first-campaign-en.png) |

Każdy projekt zawiera 34 edytowalne warstwy tekstowe, 24 warstwy kształtów oraz cztery osobne ilustracje rastrowe z maskami wektorowymi i tło. Sześć grup porządkuje nagłówek, kampanię, wpisy, połączenie, mapę i stopkę. Krój pisma: **DejaVu Sans**, z polskimi znakami. Ilustracje pochodzą z wcześniejszej grafiki wygenerowanej przez imagegen; tekst i układ zostały ponownie złożone natywnymi poleceniami Photocrafta. Edytowalny wariant ma uproszczony układ, nie jest automatycznym rozwarstwieniem oryginalnej ilustracji.

1. Otwórz wybraną wersję `.pcraft` i rozwiń grupę danego kroku.
2. Zaznacz warstwę tekstową i zmień jej treść narzędziem tekstowym. Możesz osobno zmienić nazwę, opis, podpis relacji lub adres strony.
3. Kształty i ilustracje przesuwaj jako osobne warstwy; obrazy źródłowe znajdują się w [assets/](assets/).
4. Zapisz projekt jako `.pcraft`, aby zachować warstwy. Do publikacji wyeksportuj PNG. PSD jest dodatkową kopią warstwową.

Both `.pcraft` and PSD files were reopened in Photocraft and retained all 69 layer-tree entries (6 groups, 34 type layers, 24 shapes, 5 pixel layers including the background). Editing a relationship label after reopening was verified on a disposable open copy without modifying the saved source. PNG exports are intentionally flattened. PSD was validated in Photocraft; it was not separately tested in Photoshop or other editors.

## GM secrets — rugged RPG edition / Sekrety MG

Nowa seria ma surowszy klimat dark fantasy: zwiadowca, krasnolud, stal, kamień i wyprawa. Zachowuje neutralną informację o działających funkcjach: szkic MG → wybór publikacji i odbiorców → widok gracza. Ilustracja nie jest zrzutem aplikacji. Materiały robocze są dostępne również współprowadzącym; publikacja dla graczy jest osobną czynnością.

| Language | Editable Photocraft | Layered PSD | Social PNG 1080 × 1350 |
| --- | --- | --- | --- |
| PL | [Project](editable/gm-secrets-pl.pcraft) | [PSD](editable/gm-secrets-pl.psd) | [PNG](exports/narrative-framework-gm-secrets-pl.png) |
| EN | [Project](editable/gm-secrets-en.pcraft) | [PSD](editable/gm-secrets-en.psd) | [PNG](exports/narrative-framework-gm-secrets-en.png) |

Teksty, numery i kształty są osobnymi warstwami Photocrafta, pogrupowanymi według nagłówka, ilustracji, treści, kroków publikacji i stopki. Źródłowa ilustracja bez tekstu: [expedition.png](assets/expedition.png). Ilustrację wygenerowano narzędziem `image_gen`; skład i eksporty wykonano w Photocrafcie. Dokładny prompt zapisano w [expedition-generation.json](expedition-generation.json). Obie serie są używane na landing page PL/EN, a źródła pozostają w repozytorium do ręcznej edycji. Zmiana `.pcraft` wymaga ponownego eksportu odpowiedniego pliku w `web/frontend/public/images/social/` i przebudowania strony.

Do social mediów używaj plików z `exports/`. Na stronie lżejszy WebP nowej serii jest podglądem, a PNG służy do pobrania. Starsze grafiki wygenerowane w całości pozostają jako wariant koncepcyjny; bieżąca strona korzysta z eksportów warstwowych projektów Photocrafta.
