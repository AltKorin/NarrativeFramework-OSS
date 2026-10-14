import { newUUID } from "./uuid";
import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  ReactFlow,
  Background,
  Controls,
  MarkerType,
  type Connection,
  Position,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import "./style.css";
import WorkshopHome from "./WorkshopHome";
import { PlaysPanel } from "./PlaysPanel";
import {
  validDocument,
  validJSON,
  type Kind,
  type Doc,
  type StoryNode,
  type Link,
} from "./document";
import { translate, initialLanguage, formatIssue, type Language } from "./i18n";
type Scenario = {
  id: string;
  title: string;
  version: number;
  document: Doc;
  is_demo: boolean;
};
const labels: Record<Kind, string> = {
  scene: "Scena",
  npc: "Postać",
  location: "Miejsce",
  clue: "Wskazówka",
};
const colors: Record<Kind, string> = {
  scene: "#3d7264",
  npc: "#95603e",
  location: "#62778f",
  clue: "#a18335",
};
const demoKey = (language: Language) =>
  language === "pl"
    ? "narrative-workshop-demo-v1"
    : "narrative-framework-demo-en-v1";
function restoreDemo(demo: Scenario, language: Language): Scenario {
  try {
    const raw = localStorage.getItem(demoKey(language));
    if (raw) {
      const parsed = JSON.parse(raw);
      if (
        validJSON(parsed) &&
        typeof parsed.title === "string" &&
        parsed.title.trim() &&
        parsed.title.length <= 200 &&
        validDocument(parsed.document)
      )
        return { ...parsed, id: demo.id, is_demo: true };
    }
  } catch {}
  return demo;
}
let csrf = "";
async function api(path: string, method = "GET", body?: unknown) {
  const r = await fetch("/api/" + path, {
    method,
    headers: { "Content-Type": "application/json", "X-CSRFToken": csrf },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  let data;
  try {
    data = await r.json();
  } catch {
    throw new Error("Unexpected server response");
  }
  if (!r.ok) throw new Error(data.error || data.detail || "Request failed");
  return data;
}
export default function Workshop() {
  const [view, setView] = useState<"home" | "editor">(() => {
    const params = new URLSearchParams(location.search);
    return params.get("view") === "editor" || params.has("scenario")
      ? "editor"
      : "home";
  });
  const [hasOpenedEditor, setHasOpenedEditor] = useState(
    () =>
      new URLSearchParams(location.search).get("view") === "editor" ||
      new URLSearchParams(location.search).has("scenario"),
  );
  const [homeAction, setHomeAction] = useState<"campaign" | "adventure" | null>(
    () => {
      const next = new URLSearchParams(location.search).get("new");
      return next === "campaign" || next === "adventure" ? next : null;
    },
  );
  const [language, setLanguage] = useState<Language>(initialLanguage);
  const t = (text: string) => translate(language, text);
  const labels: Record<Kind, string> = {
    scene: t("Scena"),
    npc: t("Postać"),
    location: t("Miejsce"),
    clue: t("Wskazówka"),
  };
  const phrase = (text: string, count: number) =>
    t(text).replace("{count}", String(count));
  useEffect(() => {
    document.documentElement.lang = language;
    document.title =
      "Narrative Framework — " +
      (language === "pl" ? "warsztat przygód RPG" : "RPG adventure workshop");
    try {
      localStorage.setItem("narrative-framework-language", language);
    } catch {}
  }, [language]);

  const [story, setStory] = useState<Scenario | null>(null),
    [selected, setSelected] = useState("harbor"),
    [user, setUser] = useState<string | null>(null);
  const [notice, setNotice] = useState(""),
    [issues, setIssues] = useState<
      { node: string; message: string; code?: string; count?: number }[] | null
    >(null),
    [busy, setBusy] = useState(false);
  const [loginOpen, setLoginOpen] = useState(
      () =>
        new URLSearchParams(location.search).has("account") ||
        ["campaign", "adventure"].includes(
          new URLSearchParams(location.search).get("new") || "",
        ),
    ),
    [username, setUsername] = useState(""),
    [password, setPassword] = useState("");
  const [registerMode, setRegisterMode] = useState(
    () => new URLSearchParams(location.search).get("account") === "register",
  );
  const [registrationEnabled, setRegistrationEnabled] = useState(false);
  const [passwordConfirm, setPasswordConfirm] = useState("");
  const [saved, setSaved] = useState(true),
    [items, setItems] = useState<
      { id: string; title: string; version: number }[]
    >([]),
    [mcpToken, setMcpToken] = useState("");
  const [linkKind, setLinkKind] = useState("leads_to");
  const pendingCreate = useRef(new Map<string, string>());
  useEffect(() => {
    Promise.all([api("session/"), api(`demo/?lang=${language}`)])
      .then(async ([session, demo]) => {
        csrf = session.csrf;
        setUser(session.user);
        setRegistrationEnabled(session.registration_enabled === true);
        if (!session.registration_enabled) setRegisterMode(false);
        const nextAction = new URLSearchParams(location.search).get("new");
        if (nextAction === "campaign" || nextAction === "adventure") {
          setLoginOpen(!session.user);
          if (!session.user && session.registration_enabled)
            setRegisterMode(true);
        }
        let initialStory = restoreDemo(demo, language);
        const requested = new URLSearchParams(location.search).get("scenario");
        if (session.user && requested)
          initialStory = await api(
            `scenarios/${encodeURIComponent(requested)}/`,
          );
        setStory(initialStory);
        setSelected(initialStory.document.start);
      })
      .catch((e) => setNotice(e.message));
  }, []);
  useEffect(() => {
    let current = true;
    if (user)
      api("scenarios/")
        .then((value) => {
          if (current) setItems(value);
        })
        .catch((e) => {
          if (current) setNotice(e.message);
        });
    else setItems([]);
    return () => {
      current = false;
    };
  }, [user]);
  useEffect(() => {
    const restore = () => {
      const params = new URLSearchParams(location.search);
      setView(
        params.get("view") === "editor" || params.has("scenario")
          ? "editor"
          : "home",
      );
    };
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);
  useEffect(() => {
    if (saved || story?.is_demo) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [saved, story?.is_demo]);
  function navigateView(next: "home" | "editor") {
    if (busy) return;
    const url = new URL(location.href);
    if (next === "home") {
      url.searchParams.delete("scenario");
      url.searchParams.delete("view");
    } else {
      url.searchParams.set("view", "editor");
      url.searchParams.delete("new");
    }
    url.searchParams.set("lang", language);
    history.pushState(null, "", url.pathname + url.search + url.hash);
    setView(next);
    if (next === "editor") {
      setHasOpenedEditor(true);
      setHomeAction(null);
    }
    setLoginOpen(false);
    window.scrollTo({ top: 0 });
  }
  const change = useCallback((fn: (s: Scenario) => Scenario) => {
    setStory((s) => (s ? fn(s) : s));
    setSaved(false);
    setIssues(null);
  }, []);
  useEffect(() => {
    if (!story?.is_demo) return;
    const timer = setTimeout(() => {
      try {
        localStorage.setItem(demoKey(language), JSON.stringify(story));
        setSaved(true);
      } catch {
        setNotice(
          t("Pamięć przeglądarki niedostępna — wyeksportuj swoją pracę."),
        );
      }
    }, 250);
    return () => clearTimeout(timer);
  }, [story, language]);
  const run = async (fn: () => Promise<void>) => {
    if (busy) return;
    setBusy(true);
    setNotice("");
    try {
      await fn();
    } catch (e) {
      setNotice(
        e instanceof SyntaxError
          ? "Nieprawidłowy plik JSON."
          : (e as Error).message,
      );
    } finally {
      setBusy(false);
    }
  };
  if (!story)
    return (
      <main className="loading">
        <h1>Narrative Framework</h1>
        <p>{notice ? t(notice) : t("Otwieranie warsztatu…")}</p>
      </main>
    );
  const doc = story.document,
    n = doc.nodes.find((n) => n.id === selected);
  const update = (patch: Partial<StoryNode>) =>
    change((s) => ({
      ...s,
      document: {
        ...s.document,
        nodes: s.document.nodes.map((n) =>
          n.id === selected ? { ...n, ...patch } : n,
        ),
      },
    }));
  function check() {
    const seen = new Set<string>(),
      queue = [doc.start];
    while (queue.length) {
      const id = queue.shift()!;
      if (seen.has(id)) continue;
      seen.add(id);
      queue.push(
        ...doc.edges
          .filter(
            (e) =>
              e.source === id &&
              e.kind === "leads_to" &&
              doc.nodes.find((n) => n.id === e.source)?.kind === "scene" &&
              doc.nodes.find((n) => n.id === e.target)?.kind === "scene",
          )
          .map((e) => e.target),
      );
    }
    const found: {
      node: string;
      message: string;
      code?: string;
      count?: number;
    }[] = [];
    for (const node of doc.nodes) {
      if (node.kind === "scene" && !seen.has(node.id))
        found.push({
          node: node.id,
          code: "unreachable_scene",
          message: "Scena nieosiągalna ze sceny początkowej przez leads_to.",
        });
      if (node.kind === "clue") {
        const count = new Set(
          doc.edges
            .filter((e) => e.target === node.id && e.kind === "reveals")
            .map((e) => e.source),
        ).size;
        if (count < 3)
          found.push({
            node: node.id,
            code: "clue_sources",
            count,
            message: "",
          });
      }
    }
    setIssues(found);
  }
  const download = (format: "json" | "md") => {
    const text =
      format === "json"
        ? JSON.stringify(story, null, 2)
        : "# " +
          story.title +
          "\n\n" +
          t("Materiały dla MG — zawierają sekrety.") +
          "\n\n" +
          doc.nodes
            .map(
              (n) =>
                "## " +
                n.title +
                " (" +
                labels[n.kind] +
                ")\n\n" +
                n.text +
                "\n\n" +
                (n.secret
                  ? "### " + t("Sekret MG") + "\n" + n.secret + "\n\n"
                  : "") +
                doc.edges
                  .filter((e) => e.source === n.id)
                  .map(
                    (e) =>
                      "- " +
                      t(
                        (
                          {
                            leads_to: "prowadzi do",
                            reveals: "ujawnia",
                            involves: "angażuje",
                            located_at: "mieści się w",
                          } as Record<string, string>
                        )[e.kind] || e.kind,
                      ) +
                      ": " +
                      doc.nodes.find((n) => n.id === e.target)?.title,
                  )
                  .join("\n"),
            )
            .join("\n\n");
    const url = URL.createObjectURL(
      new Blob([text], {
        type:
          format === "json"
            ? "application/json"
            : "text/markdown;charset=utf-8",
      }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = (language === "pl" ? "przygoda" : "adventure") + "." + format;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  const connect = (c: Connection) => {
    if (!c.source || !c.target || busy) return;
    if (doc.edges.length >= 1000) {
      setNotice(t("Limit 1000 powiązań."));
      return;
    }
    change((s) => ({
      ...s,
      document: {
        ...s.document,
        edges: [
          ...s.document.edges,
          {
            id: newUUID(),
            source: c.source!,
            target: c.target!,
            kind: linkKind,
          },
        ],
      },
    }));
  };
  async function switchLanguage(next: Language) {
    if (next === language || busy) return;
    await run(async () => {
      if (story!.is_demo) {
        try {
          localStorage.setItem(demoKey(language), JSON.stringify(story));
        } catch {
          throw new Error(
            t("Pamięć przeglądarki niedostępna — wyeksportuj swoją pracę."),
          );
        }
        const demo = await api(`demo/?lang=${next}`);
        const restored = restoreDemo(demo, next);
        setStory(restored);
        setSelected(restored.document.start);
        setSaved(true);
      }
      setIssues(null);
      setLanguage(next);
      const url = new URL(location.href);
      url.searchParams.set("lang", next);
      history.replaceState(null, "", url.pathname + url.search + url.hash);
    });
  }
  async function persist() {
    const body = { title: story!.title, document: story!.document };
    const key = `narrative-framework-create:${user}:${language}`;
    let creationId = pendingCreate.current.get(key);
    if (story!.is_demo && !creationId) {
      try {
        const cached = localStorage.getItem(key);
        if (
          cached &&
          /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
            cached,
          )
        )
          creationId = cached;
      } catch {}
      creationId = creationId || newUUID();
      pendingCreate.current.set(key, creationId);
      try {
        localStorage.setItem(key, creationId);
      } catch {}
    }
    let result;
    try {
      result = story!.is_demo
        ? await api("scenarios/", "POST", { ...body, id: creationId })
        : await api(`scenarios/${story!.id}/`, "PUT", {
            ...body,
            version: story!.version,
          });
    } catch (error) {
      try {
        setItems(await api("scenarios/"));
      } catch {}
      throw error;
    }
    if (story!.is_demo) {
      pendingCreate.current.delete(key);
      try {
        localStorage.removeItem(key);
      } catch {}
    }
    setStory(result);
    setSaved(true);
    setItems(await api("scenarios/"));
    const url = new URL(location.href);
    url.searchParams.set("scenario", result.id);
    url.searchParams.delete("view");
    url.searchParams.delete("new");
    history.replaceState(null, "", url.pathname + url.search + url.hash);
    setNotice(t("Zapisano na serwerze."));
  }
  return (
    <div className="app">
      <header>
        <a className="brand" href="/">
          N<span className="brand-rule" />F{" "}
          <span>
            Narrative
            <br />
            Framework
          </span>
        </a>
        <div className="header-caption">
          {t("NARZĘDZIA DO OPOWIADANIA HISTORII")}
        </div>
        <div className="header-actions">
          <button
            aria-pressed={view === "home"}
            onClick={() => navigateView("home")}
          >
            {language === "pl" ? "Pulpit" : "Dashboard"}
          </button>
          <a
            className="wiki-entry"
            href={`/workshop/?lang=${language}&new=campaign`}
          >
            {language === "pl" ? "Nowa kampania" : "New campaign"}
          </a>
          <a className="wiki-entry" href={`/campaigns/?lang=${language}`}>
            {language === "pl" ? "Wiki kampanii" : "Campaign wiki"}
          </a>
          <div
            className="language-switch"
            role="group"
            aria-label={t("Język interfejsu")}
          >
            <button
              disabled={busy}
              aria-pressed={language === "pl"}
              onClick={() => switchLanguage("pl")}
            >
              PL
            </button>
            <button
              disabled={busy}
              aria-pressed={language === "en"}
              onClick={() => switchLanguage("en")}
            >
              EN
            </button>
          </div>
          <button onClick={() => setLoginOpen(!loginOpen)}>
            {user || t("Zaloguj się")} <span aria-hidden="true">↗</span>
          </button>
        </div>
      </header>
      {view === "editor" && (
        <div className="workspace-heading">
          <div>
            <div className="eyebrow">
              {t("WARSZTAT / PRZYGODA NA JEDNĄ SESJĘ")}
            </div>
            <input
              className="story-title"
              disabled={busy}
              aria-label={t("Tytuł przygody")}
              maxLength={200}
              value={story.title}
              onChange={(e) => change((s) => ({ ...s, title: e.target.value }))}
            />
            <p>
              {t("Śledź powiązania. Odkrywaj luki. Zostaw graczom przestrzeń.")}
            </p>
          </div>
          <div className="save-panel">
            <span className="status-dot" />
            {story.is_demo
              ? t("Kopia w tej przeglądarce")
              : saved
                ? t("Zapisano · wersja ") + story.version
                : t("Niezapisane zmiany")}
            <button
              className="primary"
              disabled={busy}
              onClick={() => (user ? run(persist) : setLoginOpen(true))}
            >
              {story.is_demo ? t("Zapisz na koncie") : t("Zapisz zmiany")}
            </button>
          </div>
        </div>
      )}
      {loginOpen && (
        <section className="account-panel">
          <div>
            <strong>
              {user
                ? t("Twoje konto")
                : registerMode
                  ? t("Załóż konto")
                  : t("Zaloguj się do warsztatu")}
            </strong>
            <p>
              {user
                ? t("Zapisane przygody i dostęp MCP.")
                : registerMode
                  ? t(
                      "Wersja testowa. Bez adresu e-mail i automatycznego odzyskiwania hasła. Eksportuj ważne przygody.",
                    )
                  : registrationEnabled
                    ? t(
                        "Demo działa bez konta. Załóż konto, aby zapisywać prywatne przygody.",
                      )
                    : t(
                        "Demo działa bez konta. Konto tworzy administrator serwera.",
                      )}
            </p>
            {!user && homeAction && (
              <p>
                {language === "pl"
                  ? "Po zalogowaniu wrócisz do rozpoczętego tworzenia kampanii lub przygody."
                  : "After signing in, continue creating your campaign or adventure."}
              </p>
            )}
          </div>
          {user ? (
            <>
              <select
                aria-label={t("Zapisane przygody")}
                value={story.is_demo ? "" : story.id}
                onChange={(e) =>
                  e.target.value &&
                  run(async () => {
                    if (
                      !saved &&
                      !confirm(
                        t(
                          "Otworzyć inną przygodę i odrzucić niezapisane zmiany?",
                        ),
                      )
                    )
                      return;
                    const s = await api(`scenarios/${e.target.value}/`);
                    setStory(s);
                    setSelected(s.document.start);
                    setSaved(true);
                    setIssues(null);
                  })
                }
              >
                <option value="">{t("Wybierz przygodę")}</option>
                {items.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.title}
                  </option>
                ))}
              </select>
              <button
                onClick={() =>
                  run(async () => {
                    const r = await api("token/", "POST");
                    setMcpToken(r.token);
                  })
                }
              >
                {t("Wygeneruj / zmień token MCP")}
              </button>
              <button
                onClick={() =>
                  run(async () => {
                    await api("session/", "DELETE");
                    setUser(null);
                    setMcpToken("");
                    if (!story.is_demo)
                      setStory(
                        restoreDemo(
                          await api(`demo/?lang=${language}`),
                          language,
                        ),
                      );
                    setSaved(true);
                    navigateView("home");
                  })
                }
              >
                {t("Wyloguj")}
              </button>
              {mcpToken && (
                <label className="token">
                  {t("Token Bearer — skopiuj i zachowaj prywatnie")}
                  <input
                    readOnly
                    value={mcpToken}
                    onFocus={(e) => e.target.select()}
                  />
                </label>
              )}
            </>
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                run(async () => {
                  const r = await api(
                    registerMode ? "register/" : "session/",
                    "POST",
                    {
                      username,
                      password,
                      ...(registerMode
                        ? { password_confirm: passwordConfirm }
                        : {}),
                    },
                  );
                  csrf = r.csrf;
                  setUser(r.user);
                  const wikiInvite = new URLSearchParams(
                    location.hash.slice(1),
                  ).get("return-wiki-invite");
                  if (wikiInvite && /^[A-Za-z0-9_-]{43}$/.test(wikiInvite)) {
                    location.assign(
                      `/campaigns/?lang=${language}#invite=${wikiInvite}`,
                    );
                    return;
                  }
                  if (
                    new URLSearchParams(location.search).get("next") ===
                    "campaigns"
                  ) {
                    location.assign(`/campaigns/?lang=${language}`);
                    return;
                  }
                  setPassword("");
                  setPasswordConfirm("");
                  setRegisterMode(false);
                  if (view === "home") setLoginOpen(false);
                  const url = new URL(location.href);
                  url.searchParams.delete("account");
                  history.replaceState(
                    null,
                    "",
                    url.pathname + url.search + url.hash,
                  );
                });
              }}
            >
              <input
                aria-label={t("Login")}
                autoComplete="username"
                required
                maxLength={registerMode ? 32 : 150}
                minLength={registerMode ? 3 : undefined}
                pattern={registerMode ? "[A-Za-z0-9_.\\-]{3,32}" : undefined}
                placeholder={t("Login")}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
              />
              <input
                aria-label={t("Hasło")}
                autoComplete={
                  registerMode ? "new-password" : "current-password"
                }
                required
                minLength={registerMode ? 12 : undefined}
                maxLength={4096}
                type="password"
                placeholder={t("Hasło")}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              {registerMode && (
                <>
                  <input
                    aria-label={t("Powtórz hasło")}
                    placeholder={t("Powtórz hasło")}
                    type="password"
                    autoComplete="new-password"
                    required
                    minLength={12}
                    maxLength={4096}
                    value={passwordConfirm}
                    onChange={(e) => setPasswordConfirm(e.target.value)}
                  />
                  <p className="registration-hint">
                    {t(
                      "Hasło: minimum 12 znaków. Unikaj popularnych haseł i swojego loginu.",
                    )}
                  </p>
                </>
              )}
              <button className="primary" disabled={busy}>
                {t(registerMode ? "Załóż konto" : "Zaloguj")}
              </button>
              {registrationEnabled && (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    setRegisterMode(!registerMode);
                    setPassword("");
                    setPasswordConfirm("");
                  }}
                >
                  {t(registerMode ? "Mam już konto" : "Załóż konto")}
                </button>
              )}
            </form>
          )}
        </section>
      )}
      {notice && (
        <div role="status" className="notice">
          {t(notice)}
          <button
            onClick={() => setNotice("")}
            aria-label={t("Zamknij komunikat")}
          >
            ×
          </button>
        </div>
      )}
      {view === "home" ? (
        <WorkshopHome
          key={user || "anonymous"}
          language={language}
          user={user}
          request={api}
          initialAction={homeAction}
          openPlays={() => {
            navigateView("editor");
            requestAnimationFrame(() =>
              document
                .querySelector(".plays")
                ?.scrollIntoView({ block: "start" }),
            );
          }}
          resume={
            hasOpenedEditor
              ? { title: story.title, unsaved: !saved && !story.is_demo }
              : null
          }
          continueEditing={() => {
            const url = new URL(location.href);
            if (!story.is_demo) url.searchParams.set("scenario", story.id);
            history.replaceState(
              null,
              "",
              url.pathname + url.search + url.hash,
            );
            navigateView("editor");
          }}
          openDemo={() => {
            if (
              !saved &&
              !story.is_demo &&
              !confirm(
                t("Otworzyć inną przygodę i odrzucić niezapisane zmiany?"),
              )
            )
              return;
            void run(async () => {
              const demo = restoreDemo(
                await api(`demo/?lang=${language}`),
                language,
              );
              setStory(demo);
              setSelected(demo.document.start);
              setSaved(true);
              setIssues(null);
              const url = new URL(location.href);
              url.searchParams.delete("scenario");
              history.replaceState(
                null,
                "",
                url.pathname + url.search + url.hash,
              );
              navigateView("editor");
            });
          }}
          authenticate={(action) => {
            setHomeAction(action);
            const url = new URL(location.href);
            url.searchParams.set("new", action);
            history.replaceState(
              null,
              "",
              url.pathname + url.search + url.hash,
            );
            setLoginOpen(true);
            setRegisterMode(registrationEnabled);
            requestAnimationFrame(() =>
              document
                .querySelector(".account-panel input")
                ?.scrollIntoView({ block: "center" }),
            );
          }}
        />
      ) : (
        <>
          <main className="workbench" inert={busy}>
            <aside className="outline">
              <div className="section-label">
                {t("ZAWARTOŚĆ")} <span>{doc.nodes.length}</span>
              </div>
              {(["scene", "npc", "location", "clue"] as Kind[]).map((kind) => (
                <section key={kind}>
                  <h2>
                    <span style={{ background: colors[kind] }} />
                    {labels[kind]}
                  </h2>
                  {doc.nodes
                    .filter((n) => n.kind === kind)
                    .map((n) => (
                      <button
                        key={n.id}
                        className={selected === n.id ? "selected" : ""}
                        onClick={() => setSelected(n.id)}
                      >
                        {n.title}
                        {doc.start === n.id && <small>START</small>}
                      </button>
                    ))}
                  <button
                    className="add"
                    disabled={doc.nodes.length >= 250}
                    onClick={() => {
                      const id = newUUID();
                      change((s) => ({
                        ...s,
                        document: {
                          ...s.document,
                          nodes: [
                            ...s.document.nodes,
                            {
                              id,
                              kind,
                              title: t(
                                (
                                  {
                                    scene: "Nowa scena",
                                    npc: "Nowa postać",
                                    location: "Nowe miejsce",
                                    clue: "Nowa wskazówka",
                                  } as Record<Kind, string>
                                )[kind],
                              ),
                              text: "",
                              secret: "",
                              x: 150 + Math.random() * 450,
                              y: 100 + Math.random() * 300,
                            },
                          ],
                        },
                      }));
                      setSelected(id);
                    }}
                  >
                    {t("+ Dodaj")}
                  </button>
                </section>
              ))}
              <div className="outline-footer">
                {t("DOBRA PRZYGODA")}
                <br />
                {t("MA WIĘCEJ NIŻ JEDNĄ DROGĘ.")}
              </div>
            </aside>
            <section className="canvas-area" aria-label={t("Graf przygody")}>
              <div className="canvas-toolbar">
                <span>{t("Mapa powiązań")}</span>
                <label>
                  {t("Nowe połączenie")}{" "}
                  <select
                    value={linkKind}
                    onChange={(e) => setLinkKind(e.target.value)}
                  >
                    <option value="leads_to">{t("prowadzi do")}</option>
                    <option value="reveals">{t("ujawnia")}</option>
                    <option value="involves">{t("angażuje")}</option>
                    <option value="located_at">{t("mieści się w")}</option>
                  </select>
                </label>
              </div>
              <div className="graph">
                <ReactFlow
                  fitView
                  ariaLabelConfig={
                    language === "pl"
                      ? {
                          "controls.zoomIn.ariaLabel": "Powiększ",
                          "controls.zoomOut.ariaLabel": "Pomniejsz",
                          "controls.fitView.ariaLabel": "Dopasuj widok",
                          "controls.ariaLabel": "Sterowanie mapą",
                          "node.a11yDescription.default":
                            "Naciśnij Enter lub spację, aby wybrać element. Escape anuluje wybór.",
                          "edge.a11yDescription.default":
                            "Naciśnij Enter lub spację, aby wybrać połączenie. Delete usuwa je, Escape anuluje wybór.",
                        }
                      : {}
                  }
                  nodes={doc.nodes.map((n) => ({
                    id: n.id,
                    position: { x: n.x || 0, y: n.y || 0 },
                    data: {
                      label: (
                        <div className="graph-label">
                          <small style={{ color: colors[n.kind] }}>
                            {labels[n.kind]}
                            {doc.start === n.id ? " / START" : ""}
                          </small>
                          <strong>{n.title}</strong>
                        </div>
                      ),
                    },
                    selected: n.id === selected,
                    style: { borderTop: "3px solid " + colors[n.kind] },
                    sourcePosition: Position.Right,
                    targetPosition: Position.Left,
                    type: "default",
                  }))}
                  edges={doc.edges.map((e) => ({
                    ...e,
                    label: (
                      {
                        leads_to: t("prowadzi do"),
                        reveals: t("ujawnia"),
                        involves: t("angażuje"),
                        located_at: t("miejsce"),
                      } as Record<string, string>
                    )[e.kind],
                    markerEnd: { type: MarkerType.ArrowClosed },
                    style: {
                      stroke: e.kind === "reveals" ? "#a18335" : "#9ba9a2",
                    },
                    labelStyle: { fill: "#627069", fontSize: 10 },
                  }))}
                  onNodesChange={(changes) => {
                    const positions = changes.filter(
                      (c) => c.type === "position" && c.position,
                    );
                    if (positions.length)
                      change((s) => ({
                        ...s,
                        document: {
                          ...s.document,
                          nodes: s.document.nodes.map((n) => {
                            const c = positions.find(
                              (c) => c.type === "position" && c.id === n.id,
                            );
                            return c && c.type === "position" && c.position
                              ? { ...n, x: c.position.x, y: c.position.y }
                              : n;
                          }),
                        },
                      }));
                  }}
                  onNodeClick={(_, n) => setSelected(n.id)}
                  onNodeDragStop={(_, n) =>
                    change((s) => ({
                      ...s,
                      document: {
                        ...s.document,
                        nodes: s.document.nodes.map((v) =>
                          v.id === n.id
                            ? { ...v, x: n.position.x, y: n.position.y }
                            : v,
                        ),
                      },
                    }))
                  }
                  onConnect={connect}
                  onEdgesDelete={(edges) =>
                    change((s) => ({
                      ...s,
                      document: {
                        ...s.document,
                        edges: s.document.edges.filter(
                          (e) => !edges.some((d) => d.id === e.id),
                        ),
                      },
                    }))
                  }
                  nodesDraggable
                  nodesConnectable
                  deleteKeyCode={["Backspace", "Delete"]}
                >
                  <Background color="#c9d1c9" gap={22} />
                  <Controls
                    showInteractive={false}
                    aria-label={
                      language === "pl" ? "Sterowanie mapą" : "Map controls"
                    }
                  />
                </ReactFlow>
              </div>
              <div className="canvas-footer">
                <span>
                  {t("Przeciągnij między punktami, aby połączyć elementy.")}
                </span>
                <span>{phrase("{count} powiązań", doc.edges.length)}</span>
              </div>
            </section>
            <aside className="inspector">
              {n ? (
                <>
                  <div className="section-label">
                    {t("SZCZEGÓŁY")} <span>{labels[n.kind]}</span>
                  </div>
                  {n.wiki_origin &&
                    typeof n.wiki_origin.campaign_id === "string" &&
                    typeof n.wiki_origin.page_id === "string" && (
                      <p className="wiki-origin">
                        <a
                          href={`/campaigns/?campaign=${encodeURIComponent(n.wiki_origin.campaign_id)}&page=${encodeURIComponent(n.wiki_origin.page_id)}&lang=${language}`}
                        >
                          {language === "pl"
                            ? "Źródło w wiki kampanii"
                            : "Source in campaign wiki"}
                        </a>{" "}
                        ·{" "}
                        {language === "pl" ? "kopia wersji" : "copy of version"}{" "}
                        {typeof n.wiki_origin.version === "number"
                          ? n.wiki_origin.version
                          : "?"}
                      </p>
                    )}
                  <label>
                    {t("Tytuł")}
                    <input
                      maxLength={200}
                      value={n.title}
                      onChange={(e) => update({ title: e.target.value })}
                    />
                  </label>
                  <label>
                    {t("Opis / tekst dla graczy")}
                    <textarea
                      rows={6}
                      maxLength={12000}
                      value={n.text}
                      onChange={(e) => update({ text: e.target.value })}
                    />
                  </label>
                  <label className="secret-label">
                    {t("Sekret dla prowadzącego")}
                    <textarea
                      rows={5}
                      maxLength={12000}
                      aria-label={t("Sekret dla prowadzącego")}
                      value={n.secret}
                      onChange={(e) => update({ secret: e.target.value })}
                    />
                  </label>
                  {n.kind === "scene" && doc.start !== n.id && (
                    <button
                      onClick={() =>
                        change((s) => ({
                          ...s,
                          document: { ...s.document, start: n.id },
                        }))
                      }
                    >
                      {t("Ustaw jako początek")}
                    </button>
                  )}
                  <h3>{t("Powiązania")}</h3>
                  {doc.edges
                    .filter((e) => e.source === n.id || e.target === n.id)
                    .map((e) => (
                      <div className="link-row" key={e.id}>
                        <span>
                          {e.source === n.id ? "→" : "←"}{" "}
                          {
                            doc.nodes.find(
                              (v) =>
                                v.id ===
                                (e.source === n.id ? e.target : e.source),
                            )?.title
                          }
                          <small>
                            {t(
                              (
                                {
                                  leads_to: "prowadzi do",
                                  reveals: "ujawnia",
                                  involves: "angażuje",
                                  located_at: "mieści się w",
                                } as Record<string, string>
                              )[e.kind] || e.kind,
                            )}
                          </small>
                        </span>
                        <button
                          aria-label={t("Usuń powiązanie")}
                          onClick={() =>
                            change((s) => ({
                              ...s,
                              document: {
                                ...s.document,
                                edges: s.document.edges.filter(
                                  (v) => v.id !== e.id,
                                ),
                              },
                            }))
                          }
                        >
                          ×
                        </button>
                      </div>
                    ))}
                  {n.id !== doc.start && (
                    <button
                      className="danger"
                      onClick={() => {
                        if (!confirm(t("Usunąć element i jego powiązania?")))
                          return;
                        change((s) => ({
                          ...s,
                          document: {
                            ...s.document,
                            nodes: s.document.nodes.filter(
                              (v) => v.id !== n.id,
                            ),
                            edges: s.document.edges.filter(
                              (e) => e.source !== n.id && e.target !== n.id,
                            ),
                          },
                        }));
                        setSelected(doc.start);
                      }}
                    >
                      {t("Usuń element")}
                    </button>
                  )}
                </>
              ) : (
                <p>{t("Wybierz element na mapie.")}</p>
              )}
            </aside>
          </main>
          <section className="review" inert={busy}>
            <div>
              <div className="eyebrow">{t("DRUGIE SPOJRZENIE")}</div>
              <h2>{t("Sprawdź, zanim poprowadzisz.")}</h2>
              <p>
                {t(
                  "Kontrola struktury wskazuje luki. O jakości historii decydujesz Ty.",
                )}
              </p>
            </div>
            <div className="review-actions">
              <button
                onClick={() =>
                  story.is_demo
                    ? check()
                    : run(async () => {
                        if (!saved)
                          throw new Error(
                            t("Zapisz zmiany przed walidacją serwera."),
                          );
                        const r = await api(
                          `scenarios/${story.id}/validate/?lang=${language}`,
                          "POST",
                        );
                        setIssues(r.issues);
                      })
                }
              >
                {t("Sprawdź strukturę ↗")}
              </button>
              <button onClick={() => download("md")}>
                {t("Eksport Markdown ↓")}
              </button>
              <button onClick={() => download("json")}>JSON ↓</button>
              <label className="import-button">
                {t("Import JSON")}
                <input
                  type="file"
                  accept="application/json,.json"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (!file) return;
                    run(async () => {
                      if (file.size > 300000)
                        throw new Error(
                          t("Plik jest zbyt duży (maks. 300 KB)."),
                        );
                      const imported = JSON.parse(await file.text());
                      const d = imported.document;
                      if (!validJSON(imported) || !validDocument(d))
                        throw new Error(
                          t("Nieprawidłowy dokument narrative-web/1."),
                        );
                      if (
                        typeof imported.title !== "string" ||
                        !imported.title.trim() ||
                        imported.title.length > 200
                      )
                        throw new Error(t("Nieprawidłowy tytuł przygody."));
                      if (
                        !confirm(
                          t("Zastąpić bieżącą zawartość importowaną przygodą?"),
                        )
                      )
                        return;
                      change((s) => ({
                        ...s,
                        title: imported.title || t("Importowana przygoda"),
                        document: d,
                      }));
                      setSelected(d.start);
                    });
                    e.target.value = "";
                  }}
                />
              </label>
            </div>
          </section>
          {issues && (
            <section className="issues" aria-live="polite">
              <strong>
                {issues.length
                  ? phrase("{count} sugestie do przejrzenia", issues.length)
                  : t("Brak uwag strukturalnych")}
              </strong>
              {issues.map((i, index) => (
                <button key={index} onClick={() => setSelected(i.node)}>
                  {doc.nodes.find((n) => n.id === i.node)?.title}:{" "}
                  {formatIssue(language, i)}
                </button>
              ))}
            </section>
          )}
          <PlaysPanel
            key={user || "anonymous"}
            language={language}
            user={user}
            source={story}
            sourceSaved={saved}
            request={api}
            onLogin={() => {
              setLoginOpen(true);
              window.scrollTo({ top: 0, behavior: "smooth" });
            }}
          />
        </>
      )}
      <footer>
        <span>NARRATIVE FRAMEWORK · OPEN SOURCE</span>
        <span>{t("Warsztat 0.2 / RPG")}</span>
      </footer>
    </div>
  );
}
