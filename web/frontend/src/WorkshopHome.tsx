import { useEffect, useRef, useState } from "react";
import type { Language } from "./i18n";
import { newUUID } from "./uuid";
import "./workshop-home.css";

type Action = "campaign" | "adventure";
type Entry = { id: string; title: string; role?: string };
type Request = (path: string, method?: string, body?: unknown) => Promise<any>;
export default function WorkshopHome({
  language,
  user,
  request,
  initialAction,
  authenticate,
  openDemo,
  openPlays,
  resume,
  continueEditing,
}: {
  language: Language;
  user: string | null;
  request: Request;
  initialAction: Action | null;
  authenticate: (action: Action) => void;
  openDemo: () => void;
  openPlays: () => void;
  resume: { title: string; unsaved: boolean } | null;
  continueEditing: () => void;
}) {
  const pl = language === "pl",
    t = (a: string, b: string) => (pl ? a : b);
  const [campaigns, setCampaigns] = useState<Entry[] | null>(null);
  const [adventures, setAdventures] = useState<Entry[] | null>(null);
  const [loading, setLoading] = useState(!!user);
  const [retry, setRetry] = useState(0);
  const [action, setAction] = useState<Action | null>(
    user ? initialAction : null,
  );
  const [title, setTitle] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState("");
  const receipt = useRef(newUUID());
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (!user) return;
    let closed = false,
      sequence = 0;
    const load = async () => {
      const current = ++sequence;
      setLoading(true);
      const results = await Promise.allSettled([
        request("campaigns/"),
        request("scenarios/"),
      ]);
      if (closed || current !== sequence) return;
      setCampaigns(results[0].status === "fulfilled" ? results[0].value : null);
      setAdventures(
        results[1].status === "fulfilled" ? results[1].value : null,
      );
      setLoading(false);
    };
    void load();
    window.addEventListener("focus", load);
    return () => {
      closed = true;
      window.removeEventListener("focus", load);
    };
  }, [user, request, retry]);
  useEffect(() => {
    if (action) input.current?.focus();
  }, [action]);
  function start(next: Action) {
    if (!user) {
      authenticate(next);
      return;
    }
    setTitle("");
    setError("");
    receipt.current = newUUID();
    setAction(next);
  }
  async function create() {
    if (!action || !title.trim() || creating) return;
    setCreating(true);
    setError("");
    try {
      const first = "opening";
      const result = await request(
        action === "campaign" ? "campaigns/" : "scenarios/",
        "POST",
        {
          id: receipt.current,
          title: title.trim(),
          ...(action === "adventure"
            ? {
                document: {
                  schema: "narrative-web/1",
                  start: first,
                  nodes: [
                    {
                      id: first,
                      kind: "scene",
                      title: t("Scena początkowa", "Opening scene"),
                      text: "",
                      secret: "",
                      x: 120,
                      y: 120,
                    },
                  ],
                  edges: [],
                },
              }
            : {}),
        },
      );
      location.assign(
        action === "campaign"
          ? `/campaigns/?lang=${language}&campaign=${encodeURIComponent(result.id)}`
          : `/workshop/?lang=${language}&scenario=${encodeURIComponent(result.id)}`,
      );
    } catch {
      setError(
        t(
          "Nie udało się utworzyć materiału. Sprawdź połączenie i spróbuj ponownie. Jeśli masz już wiele materiałów, sprawdź limit konta.",
          "Could not create the material. Check your connection and try again. If you already have many items, check your account limit.",
        ),
      );
      setCreating(false);
    }
  }
  return (
    <main
      className="workshop-home"
      aria-labelledby="workshop-home-title"
      inert={creating}
      aria-busy={creating}
    >
      <div className="wh-heading">
        <p className="eyebrow">{t("WARSZTAT / START", "WORKSHOP / HOME")}</p>
        <h1 id="workshop-home-title">
          {t("Twój warsztat RPG", "Your RPG workshop")}
        </h1>
        <p>
          {t(
            "Zacznij od kampanii albo wróć do swoich materiałów. Jedna postać i jedno miejsce wystarczą na początek.",
            "Start a campaign or return to your material. One character and one place are enough to begin.",
          )}
        </p>
      </div>
      <section
        className="wh-start"
        aria-label={t(
          "Co chcesz przygotować?",
          "What would you like to prepare?",
        )}
      >
        <div>
          <h2>{t("Świat na wiele sesji", "A world for many sessions")}</h2>
          <p>
            {t(
              "Kampania łączy postacie, miejsca, wiki i mapę powiązań. Tutaj zacznij, jeśli budujesz świat dla swojej drużyny.",
              "A campaign brings together characters, places, a wiki and a relationship map. Start here to build your party's world.",
            )}
          </p>
          <button className="primary" onClick={() => start("campaign")}>
            {t("Nowa kampania", "New campaign")}
          </button>
        </div>
        <div>
          <h2>
            {t("Przygoda na jedną sesję", "An adventure for one session")}
          </h2>
          <p>
            {t(
              "Zaplanuj sceny, wskazówki i drogi między nimi. To osobny scenariusz, który możesz przygotować także bez kampanii.",
              "Plan scenes, clues and paths between them. This is a separate scenario you can prepare with or without a campaign.",
            )}
          </p>
          <button onClick={() => start("adventure")}>
            {t("Nowa przygoda", "New adventure")}
          </button>
        </div>
      </section>
      {!user && (
        <p className="wh-account-note">
          {t(
            "Własne kampanie i przygody zapiszesz po zalogowaniu. Demo możesz wypróbować od razu, bez konta.",
            "Log in to save your own campaigns and adventures. Try the demo right away without an account.",
          )}
        </p>
      )}
      {action && user && (
        <section
          className="wh-create"
          aria-label={t("Tworzenie materiału", "Create material")}
        >
          <h2>
            {action === "campaign"
              ? t("Jak nazywa się Twój świat?", "What is your world called?")
              : t(
                  "Jak nazywa się Twoja przygoda?",
                  "What is your adventure called?",
                )}
          </h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void create();
            }}
          >
            <label>
              {action === "campaign"
                ? t("Nazwa kampanii", "Campaign name")
                : t("Nazwa przygody", "Adventure name")}
              <input
                ref={input}
                required
                maxLength={200}
                value={title}
                onChange={(e) => {
                  setTitle(e.target.value);
                  receipt.current = newUUID();
                }}
                placeholder={
                  action === "campaign"
                    ? t("np. Cienie nad Zatoką", "e.g. Shadows over the Bay")
                    : t("np. Dzwon pod wodą", "e.g. The Sunken Bell")
                }
                disabled={creating}
              />
            </label>
            <div>
              <button className="primary" disabled={creating || !title.trim()}>
                {creating
                  ? t("Zapisywanie…", "Saving…")
                  : action === "campaign"
                    ? t("Utwórz kampanię", "Create campaign")
                    : t("Utwórz przygodę", "Create adventure")}
              </button>
              <button
                type="button"
                disabled={creating}
                onClick={() => {
                  setAction(null);
                  const url = new URL(location.href);
                  url.searchParams.delete("new");
                  history.replaceState(
                    null,
                    "",
                    url.pathname + url.search + url.hash,
                  );
                }}
              >
                {t("Anuluj", "Cancel")}
              </button>
            </div>
          </form>
          <p>
            {action === "campaign"
              ? t(
                  "Po zapisaniu dodasz pierwszą postać lub miejsce. Nowa kampania jest prywatna.",
                  "After saving, add your first character or place. Your new campaign is private.",
                )
              : t(
                  "Zaczniesz od pustej sceny początkowej, którą rozwiniesz po swojemu.",
                  "Start with an empty opening scene and make it your own.",
                )}
          </p>
          {error && <p role="alert">{error}</p>}
        </section>
      )}
      {resume && (
        <section className="wh-resume">
          <div>
            <h2>{t("Otwarta przygoda", "Open adventure")}</h2>
            <p>
              {resume.title}
              {resume.unsaved
                ? t(" · niezapisane zmiany", " · unsaved changes")
                : ""}
            </p>
          </div>
          <button onClick={continueEditing}>
            {t("Kontynuuj edycję", "Continue editing")}
          </button>
        </section>
      )}
      {user && (
        <section
          className="wh-library"
          aria-label={t("Twoje materiały", "Your material")}
          aria-busy={loading}
        >
          <div className="wh-library-heading">
            <h2>{t("Wróć do materiałów", "Return to your material")}</h2>
            <button disabled={loading} onClick={() => setRetry((n) => n + 1)}>
              {t("Odśwież listy", "Refresh lists")}
            </button>
          </div>
          <div className="wh-lists">
            {(
              [
                ["campaign", campaigns],
                ["adventure", adventures],
              ] as const
            ).map(([kind, entries]) => (
              <section key={kind}>
                <h3>
                  {kind === "campaign"
                    ? t("Twoje kampanie", "Your campaigns")
                    : t("Twoje przygody", "Your adventures")}
                </h3>
                {loading ? (
                  <p role="status">
                    {t("Wczytywanie materiałów…", "Loading material…")}
                  </p>
                ) : entries === null ? (
                  <p role="alert">
                    {t(
                      "Nie udało się wczytać tej listy. Użyj przycisku „Odśwież listy”.",
                      "Could not load this list. Use Refresh lists to try again.",
                    )}
                  </p>
                ) : entries.length ? (
                  <ul>
                    {entries.map((entry) => (
                      <li key={entry.id}>
                        <a
                          href={
                            kind === "campaign"
                              ? `/campaigns/?lang=${language}&campaign=${encodeURIComponent(entry.id)}`
                              : `/workshop/?lang=${language}&scenario=${encodeURIComponent(entry.id)}`
                          }
                        >
                          {entry.title}
                        </a>
                        {entry.role && (
                          <small>
                            {entry.role === "owner"
                              ? t("Właściciel", "Owner")
                              : entry.role === "gm"
                                ? t("Współprowadzący", "Co-GM")
                                : t("Gracz", "Player")}
                          </small>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <>
                    <p>
                      {kind === "campaign"
                        ? t(
                            "Nie masz jeszcze kampanii. Nadaj światu nazwę i dodaj pierwszy wpis.",
                            "No campaigns yet. Name your world and add the first entry.",
                          )
                        : t(
                            "Nie masz jeszcze przygód. Możesz zacząć od pustego scenariusza albo obejrzeć demo.",
                            "No adventures yet. Start with an empty scenario or explore the demo.",
                          )}
                    </p>
                    <button onClick={() => start(kind)}>
                      {kind === "campaign"
                        ? t(
                            "Utwórz pierwszą kampanię",
                            "Create your first campaign",
                          )
                        : t(
                            "Utwórz pierwszą przygodę",
                            "Create your first adventure",
                          )}
                    </button>
                  </>
                )}
              </section>
            ))}
          </div>
        </section>
      )}
      <section className="wh-first-steps">
        <div>
          <h2>
            {t(
              "Pierwsza mapa w czterech krokach",
              "Your first map in four steps",
            )}
          </h2>
          <ol>
            <li>{t("Nazwij kampanię.", "Name your campaign.")}</li>
            <li>
              {t("Dodaj postać i miejsce.", "Add a character and a place.")}
            </li>
            <li>
              {t(
                "Nazwij relację między nimi.",
                "Name the relationship between them.",
              )}
            </li>
            <li>
              {t(
                "Otwórz mapę i rozwijaj swój świat.",
                "Open the map and grow your world.",
              )}
            </li>
          </ol>
        </div>
        <div>
          <h2>
            {t(
              "Najpierw chcesz zobaczyć przykład?",
              "Want to see an example first?",
            )}
          </h2>
          <p>
            {t(
              "Demo to osobna, przykładowa przygoda. Twoje zmiany w demie zostają w tej przeglądarce do czasu zapisania kopii na koncie.",
              "The demo is a separate sample adventure. Demo edits stay in this browser until you save a copy to your account.",
            )}
          </p>
          <button onClick={openDemo}>
            {t("Otwórz demo przygody", "Open adventure demo")}
          </button>
          <a href={`/docs/${language}/campaign-wiki.md`}>
            {t("Jak zacząć", "Getting started")}
          </a>
        </div>
      </section>
      <section className="wh-play-entry">
        <h2>{t("Między sesjami", "Between sessions")}</h2>
        <p>
          {t(
            "Otwórz przypisaną przygodę solo lub downtime albo przygotuj indywidualną rozgrywkę dla gracza.",
            "Open an assigned solo or downtime adventure, or prepare individual play for a player.",
          )}
        </p>
        <button onClick={openPlays}>
          {t("Otwórz rozgrywki indywidualne", "Open individual play")}
        </button>
      </section>
    </main>
  );
}
