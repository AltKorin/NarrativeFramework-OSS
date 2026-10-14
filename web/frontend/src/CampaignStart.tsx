import { useState } from "react";

type Entry = { id: string; title: string };
/** Progress comes from saved campaign data, never a separate tutorial database. */
export default function CampaignStart({
  pl,
  pages,
  connected,
  graph,
  add,
  link,
  showMap,
}: {
  pl: boolean;
  pages: Entry[];
  connected: boolean;
  graph: boolean;
  add: (kind: string) => void;
  link: (source: string, target: string, label: string) => Promise<void>;
  showMap: () => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [hidden, setHidden] = useState(false);
  const [source, setSource] = useState("");
  const [target, setTarget] = useState("");
  const [label, setLabel] = useState("");
  const step =
    pages.length === 0 ? 0 : pages.length === 1 ? 1 : !connected ? 2 : 3;
  const titles = [
    t("Dodaj pierwszy wpis", "Add your first entry"),
    t("Dodaj drugi wpis", "Add another entry"),
    t("Połącz dwa wpisy", "Connect two entries"),
    t("Twoja mapa już działa", "Your map is ready"),
  ];
  if (hidden)
    return (
      <button className="wiki-guide-reopen" onClick={() => setHidden(false)}>
        {t("Pokaż pierwsze kroki", "Show first steps")}
      </button>
    );
  return (
    <section
      className="wiki-start"
      aria-label={t("Pierwsze kroki", "First steps")}
    >
      <div className="wiki-start-heading">
        <span>{t("OD POMYSŁU DO MAPY", "FROM IDEA TO MAP")}</span>
        <button onClick={() => setHidden(true)}>
          {t("Ukryj wskazówki", "Hide tips")}
        </button>
      </div>
      <ol className="wiki-start-progress" aria-label={t("Postęp", "Progress")}>
        {[
          t("Kampania", "Campaign"),
          t("Dwa wpisy", "Two entries"),
          t("Połączenie", "Connection"),
          t("Mapa", "Map"),
        ].map((title, i) => (
          <li
            key={i}
            aria-current={i === (step < 2 ? 1 : step) ? "step" : undefined}
            data-done={
              i === 0 ||
              (i === 1 && pages.length >= 2) ||
              (i === 2 && connected) ||
              (i === 3 && connected && graph)
            }
          >
            {i + 1}. {title}
          </li>
        ))}
      </ol>
      <h3>{titles[step]}</h3>
      <p>
        {step < 2
          ? t(
              pages.length
                ? "Masz już pierwszy wpis. Dodaj kogoś lub coś, co jest z nim związane — na przykład karczmarza i jego karczmę."
                : "Zacznij od jednej postaci albo miejsca z najbliższej sesji. Wystarczą nazwa i jedno zdanie. Resztę dopiszesz później.",
              pages.length
                ? "You have your first entry. Add someone or something related to it — an innkeeper and their inn, for example."
                : "Start with one character or place from your next session. A name and one sentence are enough. Add the rest later.",
            )
          : step === 2
            ? t(
                "Wybierz dwa wpisy i nazwij ich relację. Czytaj ją od lewej do prawej: karczmarz → pracuje w → karczma.",
                "Choose two entries and name their relationship. Read it left to right: innkeeper → works at → inn.",
              )
            : t(
                "Każdy wpis jest punktem, a każda relacja linią ze strzałką. Kliknij punkt na mapie, aby otworzyć jego opis. Możesz dalej dodawać własne wpisy i połączenia.",
                "Each entry is a node; each relationship is an arrow. Click a node on the map to open its description. Keep adding your own entries and connections.",
              )}
      </p>
      {step < 2 ? (
        <div className="wiki-start-actions">
          <button className="wiki-primary" onClick={() => add("npc")}>
            {t("Dodaj postać", "Add a character")}
          </button>
          <button onClick={() => add("location")}>
            {t("Dodaj miejsce", "Add a place")}
          </button>
          <button onClick={() => add("lore")}>
            {t("Dodaj inny wpis", "Add another kind of entry")}
          </button>
        </div>
      ) : (
        <>
          {step === 3 && !graph && (
            <button className="wiki-primary" onClick={showMap}>
              {t("Zobacz swoją mapę", "See your map")}
            </button>
          )}
          <details open={step === 2} className="wiki-start-link">
            <summary>{t("Nowe połączenie", "New connection")}</summary>
            <form
              onSubmit={async (e) => {
                e.preventDefault();
                await link(source || pages[0].id, target, label.trim());
              }}
            >
              <label>
                {t("Od wpisu", "From entry")}
                <select
                  aria-label={t("Od wpisu", "From entry")}
                  value={source || pages[0].id}
                  onChange={(e) => {
                    setSource(e.target.value);
                    setTarget("");
                  }}
                >
                  {pages.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.title}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                {t("Relacja", "Relationship")}
                <input
                  required
                  maxLength={80}
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                  placeholder={t("np. pracuje w", "e.g. works at")}
                />
              </label>
              <label>
                {t("Do wpisu", "To entry")}
                <select
                  aria-label={t("Do wpisu", "To entry")}
                  required
                  value={target}
                  onChange={(e) => setTarget(e.target.value)}
                >
                  <option value="">
                    {t("Wybierz drugi wpis", "Choose the other entry")}
                  </option>
                  {pages
                    .filter((p) => p.id !== (source || pages[0].id))
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.title}
                      </option>
                    ))}
                </select>
              </label>
              <button
                className="wiki-primary"
                disabled={!label.trim() || !target}
              >
                {t("Połącz i pokaż mapę", "Connect and show map")}
              </button>
            </form>
          </details>
          <div className="wiki-start-actions">
            <button onClick={() => add("npc")}>
              {t("Dodaj postać", "Add a character")}
            </button>
            <button onClick={() => add("location")}>
              {t("Dodaj miejsce", "Add a place")}
            </button>
          </div>
        </>
      )}
      <p className="wiki-hint">
        {t(
          "Nowe wpisy i połączenia są prywatne dla MG. Gracze zobaczą je dopiero po publikacji. Współprowadzący mają dostęp do sekretów.",
          "New entries and connections are private to GMs. Players see them only after publication. Co-GMs can access secrets.",
        )}
      </p>
    </section>
  );
}
