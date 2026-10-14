import { useEffect, useRef, useState } from "react";
import Markdown, { defaultUrlTransform } from "react-markdown";
import remarkGfm from "remark-gfm";
import {
  ReactFlow,
  Background,
  Controls,
  MarkerType,
  useReactFlow,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { initialLanguage, translate, type Language } from "./i18n";
import { newUUID } from "./uuid";
import "./wiki.css";
import CampaignStart from "./CampaignStart";
import {
  CampaignSharing,
  PublicationControls,
  type Publication,
} from "./WikiSharing";

type Meta = {
  id: string;
  campaign_id: string;
  title: string;
  kind: string;
  version: number;
};
type Page = Meta & Publication & { body: string; gm_notes?: string };
type Relation = Publication & {
  id: string;
  source: string;
  target: string;
  label: string;
};
type Campaign = {
  id: string;
  title: string;
  summary: string;
  version: number;
  role: string;
  actual_role: string;
  preview: boolean;
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
  "Invitation unavailable":
    "Zaproszenie wygasło, zostało użyte lub unieważnione.",
  "Already a campaign member":
    "Należysz już do tej kampanii. Rolę może zmienić jej właściciel.",
  "Invitation rate limit reached":
    "Za dużo zaproszeń. Spróbuj ponownie później.",
  "Invitation limit reached": "Osiągnięto limit 20 aktywnych zaproszeń.",
  "Campaign member limit reached":
    "Osiągnięto limit 50 zaproszonych członków kampanii.",
  "Invalid campaign role": "Wybierz poprawną rolę w kampanii.",
  "Member not found": "Nie znaleziono członka kampanii.",
  "Invitation not found": "Nie znaleziono zaproszenia.",
  "Invalid publication audience": "Wybierz odbiorców publikacji.",
  "Invalid publication recipients":
    "Wybierz aktualnych graczy tej kampanii i sprawdź odbiorców publikacji.",
  "The owner cannot leave the campaign":
    "Właściciel nie może opuścić swojej kampanii.",
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
function FitCampaignMap() {
  const flow = useReactFlow();
  useEffect(() => {
    let frame = 0;
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        void flow.fitView({ padding: 0.2 });
      });
    });
    const element = document.querySelector(".wiki-map");
    if (element) observer.observe(element);
    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame);
    };
  }, [flow]);
  return null;
}
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
  const [preview, setPreview] = useState("");
  const previewRef = useRef("");
  const accessEpoch = useRef(0);
  const [inviteToken, setInviteToken] = useState(
    () => new URLSearchParams(location.hash.slice(1)).get("invite") || "",
  );
  const [invitation, setInvitation] = useState<{
    title: string;
    role: string;
  } | null>(null);
  const [inviteError, setInviteError] = useState("");
  const canEdit = !!campaign && campaign.role !== "player";
  const actualGM =
    !!campaign && (campaign.actual_role || campaign.role) !== "player";
  const dirty =
    canEdit &&
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
    const epoch = accessEpoch.current;
    if (
      method === "GET" &&
      previewRef.current &&
      /^campaigns\/[^/]+\/(?:$|pages\/|export\/)/.test(path)
    ) {
      path +=
        (path.includes("?") ? "&" : "?") +
        "preview=" +
        encodeURIComponent(previewRef.current);
    }
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
    if (epoch !== accessEpoch.current)
      throw Error(
        t(
          "Uprawnienia się zmieniły. Odśwież widok.",
          "Access changed. Reload the view.",
        ),
      );
    if (!r.ok) {
      if (
        r.status === 401 ||
        r.status === 403 ||
        (r.status === 404 && /^campaigns\/[^/]+\/($|\?)/.test(path))
      ) {
        accessEpoch.current += 1;
        const denied = /^campaigns\/([^/]+)\//.exec(path)?.[1];
        setCampaigns((items) =>
          denied ? items.filter((c) => c.id !== denied) : [],
        );
        setCampaign(null);
        setPage(null);
        setDraft(null);
        setHits(null);
        setEditing(false);
        setCopiedTo("");
      }
      throw Error(result.error || result.detail || "Request failed");
    }
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
      gm_notes: p.gm_notes || "",
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
  useEffect(() => {
    const changed = () => {
      setInviteToken(
        new URLSearchParams(location.hash.slice(1)).get("invite") || "",
      );
      setInvitation(null);
      setInviteError("");
    };
    window.addEventListener("hashchange", changed);
    return () => window.removeEventListener("hashchange", changed);
  }, []);
  useEffect(() => {
    if (!user || !inviteToken) return;
    api("campaign-invitations/inspect/", "POST", { token: inviteToken })
      .then(setInvitation)
      .catch((e) => setInviteError(e.message));
  }, [user, inviteToken]);
  useEffect(() => {
    if (!campaign) return;
    let pending = false;
    const recheck = async () => {
      if (pending || document.visibilityState === "hidden" || busy) return;
      pending = true;
      try {
        const current = await api(`campaigns/${campaign.id}/`);
        if (current.role !== campaign.role) {
          accessEpoch.current += 1;
          setPage(null);
          setDraft(null);
          setHits(null);
          setEditing(false);
          setCopiedTo("");
          setNotice(
            t(
              "Zmieniły się Twoje uprawnienia. Otwórz artykuł ponownie.",
              "Your permissions changed. Open the article again.",
            ),
          );
        }
        setCampaign(current);
        if (current.role === "player") {
          setHits(null);
          if (query.trim())
            setHits(
              await api(
                `campaigns/${campaign.id}/pages/?q=${encodeURIComponent(query)}`,
              ),
            );
        }
        if (page && !current.pages.some((p: Meta) => p.id === page.id)) {
          accessEpoch.current += 1;
          setPage(null);
          setDraft(null);
          setHits(null);
          setEditing(false);
        } else if (current.role === "player" && page) {
          const value = await api(`campaigns/${campaign.id}/pages/${page.id}/`);
          setPage(value);
          setDraft({
            title: value.title,
            kind: value.kind,
            body: value.body,
            gm_notes: "",
          });
        }
        setCampaigns(await api("campaigns/"));
      } catch {
        /* api clears unauthorized data; retained GM drafts are not silently discarded. */
      } finally {
        pending = false;
      }
    };
    const focus = () => {
      void recheck();
    };
    window.addEventListener("focus", focus);
    document.addEventListener("visibilitychange", focus);
    const timer = setInterval(focus, 30000);
    return () => {
      window.removeEventListener("focus", focus);
      document.removeEventListener("visibilitychange", focus);
      clearInterval(timer);
    };
  }, [campaign?.id, campaign?.role, page?.id, busy, query]);

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
      gm_notes: value.gm_notes || "",
    });
    setEditing(false);
    await refresh(campaign.id);
    changeURL(campaign.id, value.id);
    setNotice(t("Zapisano artykuł.", "Article saved."));
  }
  function addEntry(kind = "lore") {
    if (!mayLeave()) return;
    setNewId(newUUID());
    setPage(null);
    setDraft({ ...emptyDraft(), kind });
    setEditing(true);
    setGraph(false);
    setCopiedTo("");
    requestAnimationFrame(() =>
      document.getElementById("wiki-entry-title")?.focus(),
    );
  }
  function showMap() {
    if (!mayLeave()) return;
    setGraph(true);
    requestAnimationFrame(() =>
      document.querySelector(".wiki-map")?.scrollIntoView({ block: "center" }),
    );
  }
  useEffect(() => {
    if (campaign && canEdit && !editing) {
      document.querySelector(".wiki-start")?.scrollIntoView({ block: "start" });
    }
  }, [campaign?.id, campaign?.pages.length, editing]);
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
            <button aria-pressed={graph} onClick={showMap}>
              {t("Mapa powiązań", "Relationship map")}
            </button>
          </div>
        )}
      </div>
      {inviteToken && (
        <section className="wiki-invite-accept">
          <h2>{t("Zaproszenie do kampanii", "Campaign invitation")}</h2>
          {invitation && (
            <>
              <p>
                {invitation.title} ·{" "}
                {invitation.role === "gm"
                  ? t(
                      "Współprowadzący — dostęp do wszystkich sekretów MG",
                      "Co-GM — access to all GM secrets",
                    )
                  : t("Gracz", "Player")}
              </p>
              <button
                disabled={busy}
                onClick={() =>
                  run(async () => {
                    const result = await api(
                      "campaign-invitations/accept/",
                      "POST",
                      { token: inviteToken },
                    );
                    setInviteToken("");
                    setInvitation(null);
                    history.replaceState(
                      null,
                      "",
                      location.pathname + location.search,
                    );
                    previewRef.current = "";
                    setPreview("");
                    setCampaigns(await api("campaigns/"));
                    await openCampaign(result.campaign_id);
                  })
                }
              >
                {t("Przyjmij zaproszenie", "Accept invitation")}
              </button>
            </>
          )}
          {inviteError && (
            <p role="alert">
              {t(
                "Zaproszenie jest niedostępne lub wygasło.",
                "The invitation is unavailable or expired.",
              )}
            </p>
          )}
          {!user && (
            <p>
              {t(
                "Zaloguj się lub załóż konto, a potem świadomie przyjmij zaproszenie.",
                "Log in or create an account, then accept the invitation.",
              )}
            </p>
          )}
          <button
            onClick={() => {
              setInviteToken("");
              setInvitation(null);
              history.replaceState(
                null,
                "",
                location.pathname + location.search,
              );
            }}
          >
            {t("Odrzuć zaproszenie", "Dismiss invitation")}
          </button>
        </section>
      )}
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
              "Wiki kampanii wymaga konta. Po zaproszeniu zobaczysz udostępnione materiały swojej grupy.",
              "Campaign wiki requires an account. An invitation gives you access to the material shared with your group.",
            )}
          </p>
          <a
            href={`/workshop/?account=login&next=campaigns&lang=${language}${inviteToken ? "#return-wiki-invite=" + encodeURIComponent(inviteToken) : ""}`}
          >
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
                    run(async () => {
                      previewRef.current = "";
                      setPreview("");
                      accessEpoch.current += 1;
                      await openCampaign(e.target.value);
                    });
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
                  previewRef.current = "";
                  setPreview("");
                  accessEpoch.current += 1;
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
                  id="wiki-campaign-name"
                  placeholder={t(
                    "np. Cienie nad Zatoką",
                    "e.g. Shadows over the Bay",
                  )}
                  maxLength={200}
                  value={newCampaign}
                  onChange={(e) => setNewCampaign(e.target.value)}
                />
              </label>
              <button type="submit" disabled={!newCampaign.trim()}>
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
                {canEdit && (
                  <button
                    className="wiki-primary"
                    onClick={() => {
                      addEntry();
                    }}
                  >
                    {t("Nowy artykuł", "New article")}
                  </button>
                )}
                {campaign.role === "owner" && (
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
                )}
                {actualGM && (
                  <CampaignSharing
                    key={campaign.id}
                    campaignId={campaign.id}
                    version={campaign.version}
                    owner={campaign.actual_role === "owner"}
                    readonly={!canEdit}
                    preview={preview}
                    api={api}
                    t={t}
                    run={run}
                    changed={async () => {
                      await refresh(campaign.id);
                    }}
                    onPreview={(id) => {
                      if (!mayLeave()) return;
                      run(async () => {
                        accessEpoch.current += 1;
                        previewRef.current = id;
                        setPreview(id);
                        setPage(null);
                        setDraft(null);
                        setHits(null);
                        setEditing(false);
                        await openCampaign(campaign.id, page?.id);
                      });
                    }}
                  />
                )}
                {!campaign.preview && campaign.role !== "owner" && (
                  <button
                    onClick={() => {
                      if (
                        confirm(
                          t(
                            "Opuścić kampanię? Ponowny dostęp wymaga nowego zaproszenia.",
                            "Leave campaign? Returning requires a new invitation.",
                          ),
                        )
                      )
                        run(async () => {
                          await api(
                            `campaigns/${campaign.id}/leave/`,
                            "POST",
                            {},
                          );
                          setCampaign(null);
                          setPage(null);
                          setDraft(null);
                          setHits(null);
                          setCampaigns(await api("campaigns/"));
                        });
                    }}
                  >
                    {t("Opuść kampanię", "Leave campaign")}
                  </button>
                )}
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
                  {canEdit
                    ? t("Eksport kampanii (MG)", "Export campaign (GM)")
                    : t(
                        "Eksport udostępnionych materiałów",
                        "Export shared material",
                      )}
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
                    "Nadaj nazwę kampanii. Potem dodasz postacie i miejsca oraz połączysz je na mapie. Nie musisz od razu opisywać całego świata.",
                    "Name your campaign. Then add characters and places and connect them on a map. You do not need to describe your whole world at once.",
                  )}
                </p>
                <button
                  className="wiki-primary"
                  onClick={() =>
                    document.getElementById("wiki-campaign-name")?.focus()
                  }
                >
                  {t("Nazwij swoją kampanię", "Name your campaign")}
                </button>
                <p className="wiki-hint">
                  {t(
                    "Kampania to wspólny świat. Warsztat przygód służy do planowania pojedynczych scenariuszy.",
                    "A campaign is your shared world. The adventure workshop plans individual scenarios.",
                  )}
                </p>
              </div>
            ) : (
              <>
                <div className="wiki-campaign-title">
                  <h2>{campaign.title}</h2>
                  {campaign.summary && <p>{campaign.summary}</p>}
                  <p>
                    {canEdit
                      ? t("Materiały robocze MG", "GM draft material")
                      : t("Udostępnione materiały", "Shared material")}{" "}
                    · {campaign.pages.length} {t("artykułów", "articles")}
                  </p>
                </div>
                {canEdit && !editing && (
                  <CampaignStart
                    key={campaign.id}
                    pl={pl}
                    pages={campaign.pages}
                    connected={campaign.relations.length > 0}
                    graph={graph}
                    add={addEntry}
                    showMap={showMap}
                    link={async (source, target, label) => {
                      if (!mayLeave()) return;
                      await run(async () => {
                        await api(
                          `campaigns/${campaign.id}/relations/`,
                          "POST",
                          {
                            id: relationReceipt.current,
                            source,
                            target,
                            label,
                          },
                        );
                        relationReceipt.current = newUUID();
                        await refresh(campaign.id);
                        setGraph(true);
                        setNotice(
                          t(
                            "Zapisano połączenie. Jest widoczne na mapie.",
                            "Connection saved. It is now on your map.",
                          ),
                        );
                      });
                    }}
                  />
                )}
                {graph && campaign.pages.length === 0 && (
                  <p className="wiki-hint">
                    {t(
                      "Mapa zapełni się, gdy dodasz pierwszą postać lub miejsce.",
                      "Your first character or place will appear here on the map.",
                    )}
                  </p>
                )}
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
                      <FitCampaignMap />
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
                          canEdit
                            ? "Pierwszy artykuł czeka."
                            : "Brak udostępnionych artykułów.",
                          canEdit
                            ? "Your first article awaits."
                            : "No shared articles yet.",
                        )}
                      </h3>
                      <p>
                        {t(
                          canEdit
                            ? "Dodaj postać, miejsce albo wiedzę o świecie. Relacje między artykułami pojawią się też na mapie."
                            : "Prowadzący może opublikować dla Ciebie materiały kampanii.",
                          canEdit
                            ? "Add a character, a place or world lore. Article relationships also appear on the map."
                            : "A GM can publish campaign material for you.",
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
                          {canEdit && (
                            <button onClick={() => setEditing(!editing)}>
                              {editing
                                ? t("Podgląd", "Preview")
                                : t("Edytuj", "Edit")}
                            </button>
                          )}
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
                          {canEdit && (
                            <button
                              className="wiki-primary"
                              disabled={!dirty || !draft.title.trim()}
                              onClick={() => run(save)}
                            >
                              {t("Zapisz artykuł", "Save article")}
                            </button>
                          )}
                        </div>
                      </div>
                      {canEdit && editing ? (
                        <div className="wiki-editor">
                          <label>
                            {t("Tytuł artykułu", "Article title")}
                            <input
                              required
                              maxLength={200}
                              id="wiki-entry-title"
                              placeholder={t(
                                "np. Mira, właścicielka karczmy",
                                "e.g. Mira, the innkeeper",
                              )}
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
                              placeholder={t(
                                "Jedno zdanie wystarczy. Kim jest ta postać? Co wyróżnia to miejsce?",
                                "One sentence is enough. Who is this character? What makes this place special?",
                              )}
                              rows={8}
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
                      {page && canEdit && (
                        <PublicationControls
                          key={page.id}
                          campaignId={campaign.id}
                          api={api}
                          t={t}
                          run={run}
                          dirty={!!dirty}
                          resource={page}
                          changed={async () => {
                            await refresh(campaign.id);
                            await openPage(campaign.id, page.id);
                          }}
                        />
                      )}
                      {page && !canEdit && (
                        <button
                          onClick={() =>
                            download(
                              "wiki-article.md",
                              `# ${draft.title}\n\n${draft.body}`,
                              "text/markdown",
                            )
                          }
                        >
                          {t("Pobierz Markdown", "Download Markdown")}
                        </button>
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
                                {canEdit && (
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
                                )}
                                {canEdit && (
                                  <PublicationControls
                                    key={r.id}
                                    campaignId={campaign.id}
                                    api={api}
                                    t={t}
                                    run={run}
                                    resource={r}
                                    relation
                                    changed={async () => {
                                      await refresh(campaign.id);
                                    }}
                                  />
                                )}
                              </div>
                            );
                          })}
                          {canEdit && (
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
                          )}
                        </section>
                      )}
                      {page && canEdit && (
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
