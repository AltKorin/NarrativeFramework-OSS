import type { Language } from "./i18n";

export default function Infographics({ language }: { language: Language }) {
  const pl = language === "pl";
  const t = (a: string, b: string) => (pl ? a : b);
  const guides = [
    {
      id: "first-campaign-illustrated",
      title: t("Pierwsza kampania", "Your first campaign"),
      extension: "webp",
      alt: t(
        "Cztery kroki: nazwij kampanię, dodaj postać i miejsce, nazwij ich relację i otwórz mapę. Mira pracuje w karczmie.",
        "Four steps: name your campaign, add a character and place, name their relationship and open the map. Mira works at the inn.",
      ),
      steps: [
        t("Nadaj nazwę swojemu światu.", "Name your world."),
        t(
          "Dodaj własne wpisy i połączenia.",
          "Add your own entries and connections.",
        ),
        t(
          "Wracaj do opisów prosto z mapy.",
          "Open descriptions straight from the map.",
        ),
      ],
    },
    {
      id: "gm-secrets",
      title: t("Sekrety i publikacja", "Secrets and publication"),
      extension: "webp",
      alt: t(
        "Zwiadowca i krasnolud planują wyprawę przy mapie. Trzy kroki: szkic MG, świadoma publikacja i widok gracza z udostępnionymi materiałami.",
        "A ranger and dwarf plan an expedition over a map. Three steps: GM draft, deliberate publication and a player view of shared material.",
      ),
      steps: [
        t("Przygotuj szkic dla prowadzących.", "Prepare a draft for the GMs."),
        t(
          "Wybierz materiały i ich odbiorców.",
          "Choose the material and its audience.",
        ),
        t(
          "Sprawdź, co zobaczy konkretny gracz.",
          "Preview what a particular player will see.",
        ),
      ],
    },
  ];
  return (
    <section
      id="campaign-guides"
      className="nf-infographics nf-container"
      aria-labelledby="infographics-title"
    >
      <div className="nf-infographics-heading">
        <h2 id="infographics-title">
          {t("Twoja kampania, krok po kroku.", "Your campaign, step by step.")}
        </h2>
        <p>
          {t(
            "Od pierwszej postaci do świata, który odkrywa drużyna. Zobacz, jak zacząć i zachować kontrolę nad sekretami MG.",
            "From your first character to a world your party discovers. See how to start and keep control of GM secrets.",
          )}
        </p>
      </div>
      <div className="nf-infographics-grid">
        {guides.map((guide) => {
          const download = `/images/social/${guide.id}-${language}.png`;
          return (
            <article key={guide.id}>
              <a
                className="nf-infographic-image"
                href={download}
                aria-label={t(
                  `Otwórz pełną infografikę: ${guide.title}`,
                  `Open full infographic: ${guide.title}`,
                )}
              >
                <img
                  src={`/images/social/${guide.id}-${language}.${guide.extension}`}
                  width={
                    guide.id === "first-campaign-illustrated" ? 1122 : 1080
                  }
                  height={
                    guide.id === "first-campaign-illustrated" ? 1402 : 1350
                  }
                  loading="lazy"
                  decoding="async"
                  alt={guide.alt}
                />
              </a>
              <h3>{guide.title}</h3>
              <ol>
                {guide.steps.map((step) => (
                  <li key={step}>{step}</li>
                ))}
              </ol>
              <a className="nf-text-link" href={download} download>
                {t("Pobierz PNG do udostępnienia", "Download PNG to share")}{" "}
                <span aria-hidden="true">↓</span>
              </a>
              {guide.id === "first-campaign-illustrated" && (
                <p className="nf-note">
                  <a
                    href={`/images/social/first-campaign-${language}.png`}
                    download
                  >
                    {t(
                      "Pobierz uproszczony wariant PNG",
                      "Download the simplified PNG variant",
                    )}
                  </a>
                </p>
              )}
            </article>
          );
        })}
      </div>
      <div className="nf-infographics-footer">
        <a
          className="nf-button"
          href={`/campaigns/?lang=${language}`}
          data-analytics="open_workshop"
        >
          {t("Zacznij własną kampanię", "Start your own campaign")}{" "}
          <span aria-hidden="true">↗</span>
        </a>
        <p className="nf-note">
          {t(
            "Kliknij grafikę, aby otworzyć pełny rozmiar. Edytowalne źródła znajdziesz w ",
            "Click an image to open it full size. Editable sources are in the ",
          )}
          <a href="https://github.com/AltKorin/NarrativeFramework-OSS/tree/main/web/marketing/social">
            {t("repozytorium", "repository")}
          </a>
          .
        </p>
      </div>
    </section>
  );
}
