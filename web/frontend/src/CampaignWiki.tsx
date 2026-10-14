import { useEffect, useRef, useState } from "react";
import Markdown, { defaultUrlTransform } from "react-markdown";
import remarkGfm from "remark-gfm";
import { ReactFlow, Background, Controls, MarkerType } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { initialLanguage, translate, type Language } from "./i18n";
import { newUUID } from "./uuid";
import "./wiki.css";

type Meta = {
  id: string;
  campaign_id: string;
  title: string;
  kind: string;
  version: number;
};
type Page = Meta & { body: string; gm_notes: string };
type Relation = { id: string; source: string; target: string; label: string };
type Campaign = {
  id: string;
  title: string;
  summary: string;
  version: number;
  pages: Meta[];
  relations: Relation[];
};
type Draft = { title: string; kind: string; body: string; gm_notes: string };
const kinds = [
  "npc",
  "location",
  "organization",
  "item",
  "lore",
  "journal",
  "scene",
  "clue",
];
const categories: Record<string, [string, string]> = {
  npc: ["Postacie", "Characters"],
  location: ["Miejsca", "Locations"],
  organization: ["Organizacje", "Organizations"],
  item: ["Przedmioty", "Items"],
  lore: ["Wiedza o świecie", "World lore"],
  journal: ["Dziennik", "Journal"],
  scene: ["Sceny", "Scenes"],
  clue: ["Wskazówki", "Clues"],
};
const emptyDraft = (): Draft => ({
  title: "",
  kind: "lore",
  body: "",
  gm_notes: "",
});
const messages: Record<string, string> = {
  "Creation conflict":
    "Ten identyfikator został już użyty. Odśwież listę i sprawdź zapis przed ponowieniem.",
  "Wiki version conflict; reload before saving":
    "Artykuł zmienił się na serwerze. Twój szkic został zachowany. Odśwież artykuł i uzgodnij zmiany.",
  "Wiki page already copied to this adventure":
    "Ta strona jest już skopiowana do tej przygody.",
  "Campaign version conflict; reload before saving":
    "Kampania zmieniła się na serwerze. Odśwież kampanię przed zapisem ustawień.",
  "Campaign not found": "Nie znaleziono kampanii.",
  "Wiki page not found": "Nie znaleziono artykułu.",
  "Invalid wiki text length": "Sprawdź długość i tytuł tekstu.",
  "Choose another wiki page": "Wybierz inny artykuł.",
  "Scenario not found": "Nie znaleziono przygody.",
  "Campaign limit reached": "Osiągnięto limit 20 kampanii.",
  "Wiki page limit reached": "Osiągnięto limit 1000 artykułów w kampanii.",
  "Wiki relation limit reached": "Osiągnięto limit 2000 powiązań w kampanii.",
};
export default function CampaignWiki() {
  const [language, setLanguage] = useState<Language>(initialLanguage);
  const pl = language === "pl";
  const t = (a: string, b: string) => (pl ? a : b);
  const category = (kind: string) => categories[kind]?.[pl ? 0 : 1] || kind;
  const [user, setUser] = useState<string | null | undefined>(undefined);
  const csrf = useRef("");
  const [campaigns, setCampaigns] = useState<{ id: string; title: string }[]>(
    [],
  );
  const [campaign, setCampaign] = useState<Campaign | null>(null);
  const [page, setPage] = useState<Page | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [newId, setNewId] = useState("");
  const [newCampaign, setNewCampaign] = useState("");
  const campaignReceipt = useRef(newUUID());
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [graph, setGraph] = useState(false);
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<Meta[] | null>(null);
  const [target, setTarget] = useState("");
  const [relationLabel, setRelationLabel] = useState("");
  const relationReceipt = useRef(newUUID());
  const [scenarios, setScenarios] = useState<{ id: string; title: string }[]>(
    [],
  );
  const [scenarioId, setScenarioId] = useState("");
  const [copiedTo, setCopiedTo] = useState("");
  const dirty =
    !!draft &&
    (!page ||
      ["title", "kind", "body", "gm_notes"].some(
        (k) => draft[k as keyof Draft] !== page[k as keyof Draft],
      ));
  async function api(
    path: string,
    method = "GET",
    body?: unknown,
    signal?: AbortSignal,
  ) {
    const r = await fetch(`/api/${path}`, {
      method,
      signal,
      headers: {
        "Content-Type": "application/json",
        "X-CSRFToken": csrf.current,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const result = await r.json();
    if (!r.ok) throw Error(result.error || result.detail || "Request failed");
    return result;
  }
  async function run(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setNotice("");
    try {
      await fn();
    } catch (e) {
      setNotice(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }
  const mayLeave = () =>
    !dirty ||
    confirm(
      t(
        "Odrzucić niezapisane zmiany artykułu?",
        "Discard unsaved article changes?",
      ),
    );
  function changeURL(campaignId: string, pageId?: string) {
    const url = new URL(location.href);
    url.searchParams.set("campaign", campaignId);
    if (pageId) url.searchParams.set("page", pageId);
    else url.searchParams.delete("page");
    history.replaceState(null, "", url.pathname + url.search);
  }
  async function refresh(id: string) {
    const value = await api(`campaigns/${id}/`);
    setCampaign(value);
    return value as Campaign;
  }
  async function openCampaign(id: string, pageId?: string) {
    const c = await refresh(id);
    setQuery("");
    setHits(null);
    setTarget("");
    setCopiedTo("");
    const first = pageId
      ? c.pages.some((p) => p.id === pageId)
        ? pageId
        : undefined
      : c.pages[0]?.id;
    if (pageId && !first) setNotice("Wiki page not found");
    if (first) await openPage(id, first);
    else {
      setPage(null);
      setDraft(null);
      setEditing(false);
      changeURL(id);
    }
  }
  async function openPage(campaignId: string, id: string) {
    const p = await api(`campaigns/${campaignId}/pages/${id}/`);
    setPage(p);
    setDraft({
      title: p.title,
      kind: p.kind,
      body: p.body,
      gm_notes: p.gm_notes,
    });
    setEditing(false);
    setTarget("");
    setCopiedTo("");
    changeURL(campaignId, id);
  }
  useEffect(() => {
    document.documentElement.lang = language;
    document.title = t(
      "Wiki kampanii — Narrative Framework",
      "Campaign wiki — Narrative Framework",
    );
    try {
      localStorage.setItem("narrative-framework-language", language);
    } catch {}
  }, [language]);
  useEffect(() => {
    api("session/")
      .then(async (s) => {
        csrf.current = s.csrf;
        setUser(s.user);
        if (!s.user) return;
        const [list, stories] = await Promise.all([
          api("campaigns/"),
          api("scenarios/"),
        ]);
        setCampaigns(list);
        setScenarios(stories);
        const params = new URLSearchParams(location.search);
        const requested = params.get("campaign");
        const initial = requested
          ? list.find((c: { id: string }) => c.id === requested)
          : list[0];
        if (requested && !initial) setNotice("Campaign not found");
        if (initial)
          await openCampaign(initial.id, params.get("page") || undefined);
      })
      .catch((e) => setNotice(e.message));
  }, []);
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
  useEffect(() => {
    if (!campaign || !query.trim()) {
      setHits(null);
      return;
    }
    const control = new AbortController();
    const timer = setTimeout(
      () =>
        api(
          `campaigns/${campaign.id}/pages/?q=${encodeURIComponent(query)}`,
          "GET",
          undefined,
          control.signal,
        )
          .then(setHits)
          .catch((e) => {
            if (!control.signal.aborted) setNotice(e.message);
          }),
      250,
    );
    return () => {
      clearTimeout(timer);
      control.abort();
    };
  }, [query, campaign?.id, campaign?.version]);
  function download(name: string, contents: string, mime: string) {
    const url = URL.createObjectURL(new Blob([contents], { type: mime }));
    const a = document.createElement("a");
    a.href = url;
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  const goPage = (id: string) => {
    if (campaign && mayLeave()) run(() => openPage(campaign.id, id));
  };
  async function save() {
    if (!campaign || !draft) return;
    const value = page
      ? await api(`campaigns/${campaign.id}/pages/${page.id}/`, "PUT", {
          ...draft,
          version: page.version,
        })
      : await api(`campaigns/${campaign.id}/pages/`, "POST", {
          ...draft,
          id: newId,
        });
    setPage(value);
    setDraft({
      title: value.title,
      kind: value.kind,
      body: value.body,
      gm_notes: value.gm_notes,
    });
    setEditing(false);
    await refresh(campaign.id);
    changeURL(campaign.id, value.id);
    setNotice(t("Zapisano artykuł.", "Article saved."));
  }
  const visible = hits || campaign?.pages || [];
  const connections =
    campaign?.relations.filter(
      (r) => r.source === page?.id || r.target === page?.id,
    ) || [];
  return (
    <div className="wiki-app">
      <header className="wiki-header">
        <a href="/" className="wiki-brand">
          Narrative Framework
        </a>
        <nav>
          <a href={`/workshop/?lang=${language}`}>
            {t("Warsztat przygód", "Adventure workshop")}
          </a>
          <div className="wiki-language" aria-label={t("Język", "Language")}>
            {(["pl", "en"] as const).map((lang) => (
              <button
                key={lang}
                aria-pressed={language === lang}
                onClick={() => setLanguage(lang)}
              >
                {lang.toUpperCase()}
              </button>
            ))}
          </div>
          <span>{user || ""}</span>
        </nav>
      </header>
      <div className="wiki-heading">
        <div>
          <p>
            {t(
              "WSPÓLNY ŚWIAT TWOICH PRZYGÓD",
              "THE SHARED WORLD OF YOUR ADVENTURES",
            )}
          </p>
          <h1>{t("Wiki kampanii", "Campaign wiki")}</h1>
        </div>
        {user && (
          <div className="wiki-switch">
            <button aria-pressed={!graph} onClick={() => setGraph(false)}>
              {t("Artykuły", "Articles")}
            </button>
            <button aria-pressed={graph} onClick={() => setGraph(true)}>
              {t("Mapa powiązań", "Relationship map")}
            </button>
          </div>
        )}
      </div>
      {notice && (
        <div className="wiki-notice" role="status">
          {pl ? messages[notice] || translate(language, notice) : notice}
          <button
            aria-label={t("Zamknij komunikat", "Dismiss message")}
            onClick={() => setNotice("")}
          >
            ×
          </button>
        </div>
      )}
      {user === undefined ? (
        <p className="wiki-empty">{t("Otwieranie wiki…", "Opening wiki…")}</p>
      ) : !user ? (
        <section className="wiki-empty">
          <h2>
            {t(
              "Twój świat, wspólny dla wielu przygód.",
              "Your world, shared across adventures.",
            )}
          </h2>
          <p>
            {t(
              "Prywatna wiki MG wymaga zalogowania. Materiały kampanii i sekrety pozostają na Twoim koncie.",
              "The private GM wiki requires an account. Campaign material and secrets stay with your account.",
            )}
          </p>
          <a href={`/workshop/?account=login&lang=${language}`}>
            {t("Zaloguj się lub załóż konto", "Log in or create an account")}
          </a>
        </section>
      ) : (
        <main className="wiki-layout" inert={busy}>
          <aside className="wiki-sidebar">
            <label>
              {t("Kampania", "Campaign")}
              <select
                aria-label={t("Kampania", "Campaign")}
                value={campaign?.id || ""}
                onChange={(e) => {
                  if (e.target.value && mayLeave())
                    run(() => openCampaign(e.target.value));
                }}
              >
                <option value="">
                  {t("Wybierz kampanię", "Choose a campaign")}
                </option>
                {campaigns.map((c) => (
                  <option value={c.id} key={c.id}>
                    {c.title}
                  </option>
                ))}
              </select>
            </label>
            <form
              className="wiki-new-campaign"
              onSubmit={(e) => {
                e.preventDefault();
                if (!mayLeave()) return;
                run(async () => {
                  const c = await api("campaigns/", "POST", {
                    id: campaignReceipt.current,
                    title: newCampaign,
                  });
                  campaignReceipt.current = newUUID();
                  setNewCampaign("");
                  setCampaigns(await api("campaigns/"));
                  await openCampaign(c.id);
                });
              }}
            >
              <label>
                {t("Nazwa nowej kampanii", "New campaign name")}
                <input
                  required
                  maxLength={200}
                  value={newCampaign}
                  onChange={(e) => setNewCampaign(e.target.value)}
                />
              </label>
              <button type="submit">
                {t("Utwórz kampanię", "Create campaign")}
              </button>
            </form>
            {campaign && (
              <>
                <label>
                  {t("Szukaj w tytułach i treści", "Search titles and content")}
                  <input
                    type="search"
                    maxLength={200}
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                  />
                </label>
                <button
                  className="wiki-primary"
                  onClick={() => {
                    if (!mayLeave()) return;
                    setNewId(newUUID());
                    setPage(null);
                    setDraft(emptyDraft());
                    setEditing(true);
                    setGraph(false);
                    setCopiedTo("");
                  }}
                >
                  {t("Nowy artykuł", "New article")}
                </button>
                <details className="wiki-settings">
                  <summary>
                    {t("Ustawienia kampanii", "Campaign settings")}
                  </summary>
                  <form
                    key={campaign.id + campaign.title}
                    onSubmit={(e) => {
                      e.preventDefault();
                      const fields = new FormData(e.currentTarget);
                      run(async () => {
                        await api(`campaigns/${campaign.id}/`, "PUT", {
                          version: campaign.version,
                          title: fields.get("title"),
                          summary: fields.get("summary"),
                        });
                        await refresh(campaign.id);
                        setCampaigns(await api("campaigns/"));
                        setNotice(
                          t(
                            "Zapisano ustawienia kampanii.",
                            "Campaign settings saved.",
                          ),
                        );
                      });
                    }}
                  >
                    <label>
                      {t("Nazwa kampanii", "Campaign title")}
                      <input
                        name="title"
                        required
                        maxLength={200}
                        defaultValue={campaign.title}
                      />
                    </label>
                    <label>
                      {t("Opis kampanii", "Campaign description")}
                      <textarea
                        name="summary"
                        rows={4}
                        maxLength={4000}
                        defaultValue={campaign.summary}
                      />
                    </label>
                    <button>{t("Zapisz ustawienia", "Save settings")}</button>
                  </form>
                  <button
                    onClick={() => {
                      if (mayLeave())
                        run(() => openCampaign(campaign.id, page?.id));
                    }}
                  >
                    {t("Odśwież kampanię", "Reload campaign")}
                  </button>
                  <button
                    className="wiki-danger"
                    onClick={() => {
                      if (
                        confirm(
                          t(
                            "Usunąć kampanię oraz wszystkie jej artykuły i powiązania? Kopie w przygodach pozostaną.",
                            "Delete this campaign and all its articles and relationships? Adventure copies remain.",
                          ),
                        )
                      )
                        run(async () => {
                          await api(`campaigns/${campaign.id}/`, "DELETE", {
                            version: campaign.version,
                          });
                          const list = await api("campaigns/");
                          setCampaigns(list);
                          setCampaign(null);
                          setPage(null);
                          setDraft(null);
                          history.replaceState(
                            null,
                            "",
                            `/campaigns/?lang=${language}`,
                          );
                          if (list[0]) await openCampaign(list[0].id);
                        });
                    }}
                  >
                    {t("Usuń kampanię", "Delete campaign")}
                  </button>
                </details>
                <div className="wiki-index">
                  {kinds.map((kind) => {
                    const entries = visible.filter((p) => p.kind === kind);
                    return (
                      entries.length > 0 && (
                        <section key={kind}>
                          <h2>
                            {category(kind)} <span>{entries.length}</span>
                          </h2>
                          {entries.map((p) => (
                            <button
                              aria-current={
                                page?.id === p.id ? "page" : undefined
                              }
                              key={p.id}
                              onClick={() => goPage(p.id)}
                            >
                              {p.title}
                            </button>
                          ))}
                        </section>
                      )
                    );
                  })}
                  {query && hits?.length === 0 && (
                    <p>{t("Brak wyników.", "No results.")}</p>
                  )}
                </div>
                <button
                  onClick={() =>
                    run(async () => {
                      const data = await api(
                        `campaigns/${campaign.id}/export/`,
                      );
                      download(
                        "campaign-wiki.json",
                        JSON.stringify(data, null, 2),
                        "application/json",
                      );
                    })
                  }
                >
                  {t("Eksport kampanii (MG)", "Export campaign (GM)")}
                </button>
              </>
            )}
          </aside>
          <section className="wiki-content">
            {!campaign ? (
              <div className="wiki-empty">
                <h2>{t("Zacznij od kampanii.", "Start with a campaign.")}</h2>
                <p>
                  {t(
                    "Utwórz świat, do którego będziesz wracać w kolejnych przygodach.",
                    "Create a world you can return to in future adventures.",
                  )}
                </p>
              </div>
            ) : (
              <>
                <div className="wiki-campaign-title">
                  <h2>{campaign.title}</h2>
                  {campaign.summary && <p>{campaign.summary}</p>}
                  <p>
                    {t("Prywatne materiały MG", "Private GM material")} ·{" "}
                    {campaign.pages.length} {t("artykułów", "articles")}
                  </p>
                </div>
                {graph && (
                  <div
                    className="wiki-map"
                    aria-label={t("Graf kampanii", "Campaign graph")}
                  >
                    <ReactFlow
                      nodes={campaign.pages.map((p, i) => ({
                        id: p.id,
                        position: {
                          x: (i % 4) * 260,
                          y: Math.floor(i / 4) * 140,
                        },
                        data: {
                          label: (
                            <>
                              <small>{category(p.kind)}</small>
                              <strong>{p.title}</strong>
                            </>
                          ),
                        },
                        selected: page?.id === p.id,
                        style: {
                          background: page?.id === p.id ? "#e1ece2" : "#fffef9",
                          border: "1px solid #a5b5a2",
                          borderRadius: 4,
                          width: 200,
                          padding: 15,
                        },
                      }))}
                      edges={campaign.relations.map((r) => ({
                        id: r.id,
                        source: r.source,
                        target: r.target,
                        label: r.label,
                        markerEnd: { type: MarkerType.ArrowClosed },
                      }))}
                      onlyRenderVisibleElements
                      nodesDraggable={false}
                      nodesConnectable={false}
                      fitView
                      minZoom={0.15}
                      maxZoom={1.5}
                      onNodeClick={(_, node) => {
                        goPage(node.id);
                        setGraph(false);
                      }}
                    >
                      <Background gap={24} />
                      <Controls showInteractive={false} />
                    </ReactFlow>
                  </div>
                )}
                {!graph &&
                  (!draft ? (
                    <div className="wiki-empty">
                      <h3>
                        {t(
                          "Pierwszy artykuł czeka.",
                          "Your first article awaits.",
                        )}
                      </h3>
                      <p>
                        {t(
                          "Dodaj postać, miejsce albo wiedzę o świecie. Relacje między artykułami pojawią się też na mapie.",
                          "Add a character, a place or world lore. Article relationships also appear on the map.",
                        )}
                      </p>
                    </div>
                  ) : (
                    <>
                      <div className="wiki-article-toolbar">
                        <span>
                          {category(draft.kind)}
                          {page ? ` · v${page.version}` : ""}
                          {dirty
                            ? ` · ${t("Niezapisane zmiany", "Unsaved changes")}`
                            : ""}
                        </span>
                        <div>
                          <button onClick={() => setEditing(!editing)}>
                            {editing
                              ? t("Podgląd", "Preview")
                              : t("Edytuj", "Edit")}
                          </button>
                          {page && (
                            <button
                              onClick={() => {
                                if (mayLeave())
                                  run(async () => {
                                    await refresh(campaign.id);
                                    await openPage(campaign.id, page.id);
                                  });
                              }}
                            >
                              {t("Odśwież", "Reload")}
                            </button>
                          )}
                          <button
                            className="wiki-primary"
                            disabled={!dirty}
                            onClick={() => run(save)}
                          >
                            {t("Zapisz artykuł", "Save article")}
                          </button>
                        </div>
                      </div>
                      {editing ? (
                        <div className="wiki-editor">
                          <label>
                            {t("Tytuł artykułu", "Article title")}
                            <input
                              required
                              maxLength={200}
                              value={draft.title}
                              onChange={(e) =>
                                setDraft({ ...draft, title: e.target.value })
                              }
                            />
                          </label>
                          <label>
                            {t("Kategoria", "Category")}
                            <select
                              aria-label={t("Kategoria", "Category")}
                              value={draft.kind}
                              onChange={(e) =>
                                setDraft({ ...draft, kind: e.target.value })
                              }
                            >
                              {kinds.map((kind) => (
                                <option key={kind} value={kind}>
                                  {category(kind)}
                                </option>
                              ))}
                            </select>
                          </label>
                          <label>
                            {t("Treść (Markdown)", "Content (Markdown)")}
                            <textarea
                              aria-label={t(
                                "Treść (Markdown)",
                                "Content (Markdown)",
                              )}
                              rows={16}
                              maxLength={12000}
                              value={draft.body}
                              onChange={(e) =>
                                setDraft({ ...draft, body: e.target.value })
                              }
                            />
                          </label>
                          <label className="wiki-private-label">
                            {t("Prywatne notatki MG", "Private GM notes")}
                            <textarea
                              aria-label={t(
                                "Prywatne notatki MG",
                                "Private GM notes",
                              )}
                              rows={5}
                              maxLength={12000}
                              value={draft.gm_notes}
                              onChange={(e) =>
                                setDraft({ ...draft, gm_notes: e.target.value })
                              }
                            />
                          </label>
                          <p className="wiki-hint">
                            {t(
                              "Nagłówki: ## Tytuł · pogrubienie: **tekst** · listy: - element. Maksymalnie 12 000 znaków w każdym polu tekstowym.",
                              "Headings: ## Title · bold: **text** · lists: - item. Up to 12,000 characters in each text field.",
                            )}
                          </p>
                        </div>
                      ) : (
                        <article className="wiki-article">
                          <h2 className="wiki-article-title">
                            {draft.title || t("Bez tytułu", "Untitled")}
                          </h2>
                          <div className="wiki-markdown">
                            <Markdown
                              remarkPlugins={[remarkGfm]}
                              skipHtml
                              urlTransform={defaultUrlTransform}
                              components={{
                                img: () => null,
                                h1: ({ children }) => <h2>{children}</h2>,
                                a: ({ href, children }) => (
                                  <a href={href} rel="noopener noreferrer">
                                    {children}
                                  </a>
                                ),
                              }}
                            >
                              {draft.body ||
                                t(
                                  "*Ten artykuł nie ma jeszcze treści.*",
                                  "*This article has no content yet.*",
                                )}
                            </Markdown>
                          </div>
                          {draft.gm_notes && (
                            <details className="wiki-private-notes">
                              <summary>
                                {t("Prywatne notatki MG", "Private GM notes")}
                              </summary>
                              <p>{draft.gm_notes}</p>
                            </details>
                          )}
                        </article>
                      )}
                      {page && (
                        <section className="wiki-relations">
                          <h3>
                            {t(
                              "Powiązania i odnośniki zwrotne",
                              "Relationships and backlinks",
                            )}
                          </h3>
                          {connections.length === 0 && (
                            <p>
                              {t(
                                "Ten artykuł nie ma jeszcze powiązań.",
                                "This article has no relationships yet.",
                              )}
                            </p>
                          )}
                          {connections.map((r) => {
                            const other =
                              r.source === page.id ? r.target : r.source;
                            const meta = campaign.pages.find(
                              (p) => p.id === other,
                            );
                            return (
                              <div className="wiki-relation" key={r.id}>
                                <button onClick={() => goPage(other)}>
                                  {r.source === page.id ? "→" : "←"}{" "}
                                  {meta?.title || "?"}
                                </button>
                                <span>{r.label}</span>
                                <button
                                  aria-label={t(
                                    "Usuń powiązanie",
                                    "Remove relationship",
                                  )}
                                  onClick={() =>
                                    run(async () => {
                                      await api(
                                        `campaigns/${campaign.id}/relations/${r.id}/`,
                                        "DELETE",
                                      );
                                      await refresh(campaign.id);
                                    })
                                  }
                                >
                                  ×
                                </button>
                              </div>
                            );
                          })}
                          <form
                            onSubmit={(e) => {
                              e.preventDefault();
                              run(async () => {
                                await api(
                                  `campaigns/${campaign.id}/relations/`,
                                  "POST",
                                  {
                                    id: relationReceipt.current,
                                    source: page.id,
                                    target,
                                    label: relationLabel,
                                  },
                                );
                                relationReceipt.current = newUUID();
                                setTarget("");
                                setRelationLabel("");
                                await refresh(campaign.id);
                              });
                            }}
                          >
                            <label>
                              {t("Powiąż z artykułem", "Link to article")}
                              <select
                                aria-label={t(
                                  "Powiąż z artykułem",
                                  "Link to article",
                                )}
                                required
                                value={target}
                                onChange={(e) => setTarget(e.target.value)}
                              >
                                <option value="">
                                  {t("Wybierz artykuł", "Choose an article")}
                                </option>
                                {campaign.pages
                                  .filter((p) => p.id !== page.id)
                                  .map((p) => (
                                    <option value={p.id} key={p.id}>
                                      {p.title}
                                    </option>
                                  ))}
                              </select>
                            </label>
                            <label>
                              {t("Opis relacji", "Relationship label")}
                              <input
                                required
                                maxLength={80}
                                placeholder={t(
                                  "np. należy do, mieszka w",
                                  "e.g. belongs to, lives in",
                                )}
                                value={relationLabel}
                                onChange={(e) =>
                                  setRelationLabel(e.target.value)
                                }
                              />
                            </label>
                            <button>
                              {t("Dodaj powiązanie", "Add relationship")}
                            </button>
                          </form>
                        </section>
                      )}
                      {page && (
                        <section className="wiki-use">
                          <h3>
                            {t(
                              "Wykorzystaj w przygodzie",
                              "Use in an adventure",
                            )}
                          </h3>
                          <p>
                            {t(
                              "Kopia zachowuje wersję źródła. Kolejne zmiany wiki nie zmienią przygody ani trwającej rozgrywki. Notatki MG trafią do sekretu węzła.",
                              "The copy records its source version. Later wiki changes do not rewrite the adventure or ongoing play. GM notes become the node's secret.",
                            )}
                          </p>
                          <form
                            onSubmit={(e) => {
                              e.preventDefault();
                              if (dirty) {
                                setNotice(
                                  t(
                                    "Najpierw zapisz artykuł.",
                                    "Save the article first.",
                                  ),
                                );
                                return;
                              }
                              run(async () => {
                                const s = await api(`scenarios/${scenarioId}/`);
                                await api(
                                  `campaigns/${campaign.id}/pages/${page.id}/use/`,
                                  "POST",
                                  {
                                    scenario_id: s.id,
                                    version: s.version,
                                    source_version: page.version,
                                  },
                                );
                                setCopiedTo(s.id);
                                setNotice(
                                  t(
                                    "Skopiowano zapisany artykuł do przygody.",
                                    "Saved article copied to the adventure.",
                                  ),
                                );
                              });
                            }}
                          >
                            <label>
                              {t("Przygoda docelowa", "Target adventure")}
                              <select
                                aria-label={t(
                                  "Przygoda docelowa",
                                  "Target adventure",
                                )}
                                required
                                value={scenarioId}
                                onChange={(e) => setScenarioId(e.target.value)}
                              >
                                <option value="">
                                  {t(
                                    "Wybierz zapisaną przygodę",
                                    "Choose a saved adventure",
                                  )}
                                </option>
                                {scenarios.map((s) => (
                                  <option key={s.id} value={s.id}>
                                    {s.title}
                                  </option>
                                ))}
                              </select>
                            </label>
                            <button>
                              {t("Skopiuj do przygody", "Copy to adventure")}
                            </button>
                          </form>
                          {copiedTo && (
                            <a
                              href={`/workshop/?scenario=${copiedTo}&lang=${language}`}
                            >
                              {t("Otwórz przygodę", "Open adventure")} ↗
                            </a>
                          )}
                          <div className="wiki-page-actions">
                            <button
                              onClick={() =>
                                download(
                                  "wiki-article.md",
                                  `# ${draft.title}\n\n${draft.body}\n\n## ${t("Prywatne notatki MG", "Private GM notes")}\n\n${draft.gm_notes}`,
                                  "text/markdown",
                                )
                              }
                            >
                              {t(
                                "Pobierz Markdown (MG)",
                                "Download Markdown (GM)",
                              )}
                            </button>
                            <button
                              className="wiki-danger"
                              onClick={() => {
                                if (
                                  confirm(
                                    t(
                                      "Usunąć artykuł i jego powiązania? Kopie w przygodach pozostaną.",
                                      "Delete this article and its relationships? Adventure copies remain.",
                                    ),
                                  )
                                )
                                  run(async () => {
                                    await api(
                                      `campaigns/${campaign.id}/pages/${page.id}/`,
                                      "DELETE",
                                      { version: page.version },
                                    );
                                    await openCampaign(campaign.id);
                                  });
                              }}
                            >
                              {t("Usuń artykuł", "Delete article")}
                            </button>
                          </div>
                        </section>
                      )}
                    </>
                  ))}
              </>
            )}
          </section>
        </main>
      )}
    </div>
  );
}
