import { newUUID } from "./uuid";
import { useEffect, useRef, useState } from "react";
import { translate, type Language } from "./i18n";
import { playText, playError, type PlayTextKey } from "./play-i18n";

type Node = { id: string; title: string; kind: string };
type Source = {
  id: string;
  version: number;
  is_demo: boolean;
  document: { start: string; nodes: Node[] };
};
type Effect = {
  id: string;
  category: string;
  description: string;
  status: "pending" | "approved" | "rejected";
  review_note: string;
};
type Play = {
  id: string;
  title: string;
  mode: string;
  character: string;
  language: Language;
  status: string;
  version: number;
  player_username: string;
  can_manage: boolean;
  source_version: number;
  brief: string;
  instructions: string;
  boundaries: string;
  return_condition: string;
  gm_notes?: string;
  memory_markdown: string;
  summary: string;
  consequences: Effect[];
  published_nodes: Node[];
};
type Entry = {
  kind: string;
  version: number;
  player_action?: string;
  narration?: string;
  summary?: string;
  created_at: string;
};
type Request = (path: string, method?: string, body?: unknown) => Promise<any>;
const categories = [
  "resource",
  "relationship",
  "discovery",
  "narrative",
] as const;
const modes = ["absence", "downtime", "solo"] as const;

export function PlaysPanel({
  language,
  user,
  source,
  sourceSaved,
  request,
  onLogin,
}: {
  language: Language;
  user: string | null;
  source: Source;
  sourceSaved: boolean;
  request: Request;
  onLogin: () => void;
}) {
  const t = (key: PlayTextKey) => playText(language, key);
  const [items, setItems] = useState<Play[]>([]),
    [selected, setSelected] = useState<Play | null>(null),
    [formOpen, setFormOpen] = useState(false);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [notice, setNotice] = useState<PlayTextKey | null>(null);
  const [credential, setCredential] = useState<{
    token: string;
    expires_at: string;
  } | null>(null);
  const [entries, setEntries] = useState<Entry[]>([]),
    [total, setTotal] = useState(0),
    [offset, setOffset] = useState(0);
  const [form, setForm] = useState({
    player_username: "",
    character: "",
    brief: "",
    instructions: "",
    boundaries: "",
    return_condition: "",
    gm_notes: "",
    mode: "absence",
    language,
    auto_approve: [] as string[],
    published_node_ids: [] as string[],
  });
  const [notes, setNotes] = useState<Record<string, string>>({});
  const creationId = useRef("");
  const retries = useRef(
    new Map<
      string,
      { operation_id: string; expected_version: number; decisions: unknown[] }
    >(),
  );
  const requestEpoch = useRef(0);
  async function refresh() {
    setItems(await request("plays/"));
  }
  async function load(id: string, page = 0) {
    const epoch = ++requestEpoch.current;
    const [detail, journal] = await Promise.all([
      request(`plays/${id}/`),
      request(`plays/${id}/journal/?offset=${page}&limit=5`),
    ]);
    if (epoch !== requestEpoch.current) return;
    setSelected(detail);
    setEntries(journal.entries);
    setTotal(journal.total);
    setOffset(page);
  }
  useEffect(() => {
    ++requestEpoch.current;
    setSelected(null);
    setCredential(null);
    setItems([]);
    setError("");
    setNotice(null);
    setFormOpen(false);
    if (user)
      request("plays/")
        .then(setItems)
        .catch((e) => setError((e as Error).message));
  }, [user]);
  useEffect(() => {
    setFormOpen(false);
  }, [source.id, source.version]);
  async function run(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError("");
    setNotice(null);
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function openForm() {
    creationId.current = newUUID();
    setForm({
      player_username: user || "",
      character: "",
      brief: "",
      instructions: "",
      boundaries: "",
      return_condition: "",
      gm_notes: "",
      mode: "absence",
      language,
      auto_approve: [],
      published_node_ids: [source.document.start],
    });
    setFormOpen(true);
  }
  function sample() {
    setForm((f) => ({
      ...f,
      character: t("sampleCharacter"),
      brief: t("sampleBrief"),
      instructions: t("sampleInstructions"),
      boundaries: t("sampleBoundaries"),
      return_condition: t("sampleReturn"),
      gm_notes: t("sampleNotes"),
    }));
  }
  function field(
    key:
      | "character"
      | "brief"
      | "instructions"
      | "boundaries"
      | "return_condition"
      | "gm_notes",
    label: PlayTextKey,
    maximum: number,
    required = false,
  ) {
    return (
      <label>
        {t(label)}
        <textarea
          aria-label={t(label)}
          required={required}
          maxLength={maximum}
          rows={key === "character" ? 2 : 3}
          value={form[key]}
          onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))}
        />
      </label>
    );
  }
  async function create() {
    if (source.is_demo || !sourceSaved) throw new Error(t("saveFirst"));
    const created = await request("plays/", "POST", {
      ...form,
      id: creationId.current,
      scenario_id: source.id,
      source_version: source.version,
    });
    await refresh();
    await load(created.id);
    setCredential(null);
    setFormOpen(false);
    setNotice("created");
  }
  async function review(effect: Effect, decision: "approved" | "rejected") {
    const note = notes[effect.id] || "";
    const key = JSON.stringify([selected!.id, effect.id, decision, note]);
    let payload = retries.current.get(key);
    if (!payload) {
      payload = {
        operation_id: newUUID(),
        expected_version: selected!.version,
        decisions: [{ id: effect.id, decision, note }],
      };
      retries.current.set(key, payload);
    }
    await request(`plays/${selected!.id}/review/`, "POST", payload);
    retries.current.delete(key);
    await load(selected!.id, offset);
    await refresh();
    setNotice("reviewed");
  }
  async function download() {
    const response = await fetch(
      `/api/plays/${selected!.id}/report/?lang=${language}`,
    );
    if (!response.ok) throw new Error(t("requestError"));
    const url = URL.createObjectURL(await response.blob());
    const a = document.createElement("a");
    a.href = url;
    a.download = "narrative-framework-report.md";
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  return (
    <section className="plays" aria-label={t("eyebrow")}>
      <div className="plays-heading">
        <div>
          <div className="eyebrow">{t("eyebrow")}</div>
          <h2>{t("title")}</h2>
          <p>{t("intro")}</p>
        </div>
        {user && (
          <button
            disabled={busy || source.is_demo || !sourceSaved}
            onClick={openForm}
          >
            {t("prepare")}
          </button>
        )}
      </div>
      {!user ? (
        <div className="plays-empty">
          <p>{t("login")}</p>
          <button onClick={onLogin}>{t("loginButton")}</button>
        </div>
      ) : (
        <>
          {(source.is_demo || !sourceSaved) && (
            <p className="plays-help">{t("saveFirst")}</p>
          )}
          {error && (
            <div role="alert" className="plays-error">
              {playError(language, new Error(translate(language, error)))}
              {formOpen && <p>{t("retryCreate")}</p>}
            </div>
          )}
          {notice && (
            <p role="status" className="plays-notice">
              {t(notice)}
            </p>
          )}
          {formOpen && (
            <form
              className="play-form"
              onSubmit={(e) => {
                e.preventDefault();
                run(create);
              }}
            >
              <fieldset disabled={busy}>
                <div className="play-form-heading">
                  <h3>{t("prepare")}</h3>
                  <button type="button" onClick={sample}>
                    {t("sample")}
                  </button>
                </div>
                <div className="play-form-grid">
                  <label>
                    {t("player")}
                    <input
                      required
                      maxLength={150}
                      value={form.player_username}
                      onChange={(e) =>
                        setForm((f) => ({
                          ...f,
                          player_username: e.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    {t("mode")}
                    <select
                      aria-label={t("mode")}
                      value={form.mode}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, mode: e.target.value }))
                      }
                    >
                      {modes.map((mode) => (
                        <option key={mode} value={mode}>
                          {t(mode)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    {t("language")}
                    <select
                      aria-label={t("language")}
                      value={form.language}
                      onChange={(e) =>
                        setForm((f) => ({
                          ...f,
                          language: e.target.value as Language,
                        }))
                      }
                    >
                      <option value="pl">Polski</option>
                      <option value="en">English</option>
                    </select>
                  </label>
                  {field("character", "character", 2000, true)}
                  {field("brief", "brief", 4000, true)}
                  {field("instructions", "instructions", 6000)}
                  {field("boundaries", "boundaries", 4000, true)}
                  {field("return_condition", "returnCondition", 2000, true)}
                </div>
                <p className="play-public-note">{t("sharedWarning")}</p>
                <fieldset className="play-materials">
                  <legend>{t("publish")}</legend>
                  <p>{t("publishHelp")}</p>
                  {source.document.nodes.map((n) => (
                    <label className="play-check" key={n.id}>
                      <input
                        type="checkbox"
                        checked={form.published_node_ids.includes(n.id)}
                        disabled={n.id === source.document.start}
                        onChange={(e) =>
                          setForm((f) => ({
                            ...f,
                            published_node_ids: e.target.checked
                              ? [...f.published_node_ids, n.id]
                              : f.published_node_ids.filter(
                                  (id) => id !== n.id,
                                ),
                          }))
                        }
                      />
                      {n.title}
                    </label>
                  ))}
                </fieldset>
                <fieldset className="play-materials">
                  <legend>{t("autoApprove")}</legend>
                  <p>{t("approvalHelp")}</p>
                  {categories.map((category) => (
                    <label className="play-check" key={category}>
                      <input
                        type="checkbox"
                        checked={form.auto_approve.includes(category)}
                        onChange={(e) =>
                          setForm((f) => ({
                            ...f,
                            auto_approve: e.target.checked
                              ? [...f.auto_approve, category]
                              : f.auto_approve.filter((c) => c !== category),
                          }))
                        }
                      />
                      {t(category)}
                    </label>
                  ))}
                </fieldset>
                <div className="play-private-note">
                  {field("gm_notes", "gmNotes", 6000)}
                  <small>{t("gmNotesLabel")}</small>
                </div>
                <div className="play-buttons">
                  <button className="primary" type="submit">
                    {t("create")}
                  </button>
                  <button type="button" onClick={() => setFormOpen(false)}>
                    {t("cancel")}
                  </button>
                </div>
              </fieldset>
            </form>
          )}
          <div className="play-selector">
            <label>
              {t("choose")}
              <select
                disabled={busy}
                aria-label={t("choose")}
                value={selected?.id || ""}
                onChange={(e) => {
                  if (e.target.value) {
                    setCredential(null);
                    setNotes({});
                    run(() => load(e.target.value));
                  }
                }}
              >
                <option value="">—</option>
                {items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.title} · {item.player_username} ·{" "}
                    {item.can_manage ? t("managed") : t("assigned")} ·{" "}
                    {t(item.status as PlayTextKey)}
                  </option>
                ))}
              </select>
            </label>
            <button
              disabled={busy}
              onClick={() =>
                run(async () => {
                  retries.current.clear();
                  await refresh();
                  if (selected) await load(selected.id, offset);
                })
              }
            >
              {t("refresh")}
            </button>
          </div>
          {!items.length && <p className="plays-empty">{t("empty")}</p>}
          {selected && (
            <article className="play-detail" aria-label={t("detail")}>
              <div className="play-detail-heading">
                <div>
                  <h3>{selected.title}</h3>
                  <p>{selected.character}</p>
                  <small>
                    {t(selected.mode as PlayTextKey)} ·{" "}
                    {t(selected.status as PlayTextKey)} · {t("version")}{" "}
                    {selected.version} · {t("sourceVersion")}{" "}
                    {selected.source_version}
                  </small>
                </div>
                <button disabled={busy} onClick={() => run(download)}>
                  {t("report")}
                </button>
              </div>
              <p className="play-public-note">{t("recordWarning")}</p>
              <div className="play-detail-grid">
                {(
                  [
                    "brief",
                    "instructions",
                    "boundaries",
                    "return_condition",
                  ] as const
                ).map((key) => (
                  <section key={key}>
                    <h4>
                      {t(key === "return_condition" ? "returnCondition" : key)}
                    </h4>
                    <p className="play-prose">{selected[key]}</p>
                  </section>
                ))}
              </div>
              {selected.can_manage && selected.gm_notes && (
                <details className="play-private-note">
                  <summary>{t("gmNotes")}</summary>
                  <p className="play-prose">{selected.gm_notes}</p>
                </details>
              )}
              <details className="play-access">
                <summary>{t("accessTitle")}</summary>
                <p>{t("accessHelp")}</p>
                <label>
                  {t("endpoint")}
                  <input
                    readOnly
                    value={window.location.origin + "/play/mcp"}
                    onFocus={(e) => e.target.select()}
                  />
                </label>
                <div className="play-buttons">
                  <button
                    disabled={busy}
                    onClick={() =>
                      run(async () => {
                        setCredential(null);
                        setCredential(
                          await request(`plays/${selected.id}/access/`, "POST"),
                        );
                      })
                    }
                  >
                    {t("generate")}
                  </button>
                  <button
                    disabled={busy}
                    onClick={() =>
                      run(async () => {
                        await request(`plays/${selected.id}/access/`, "DELETE");
                        setCredential(null);
                        setNotice("tokenRevoked");
                      })
                    }
                  >
                    {t("revoke")}
                  </button>
                </div>
                {credential && (
                  <label>
                    {t("token")}
                    <input
                      readOnly
                      aria-label={t("token")}
                      value={credential.token}
                      onFocus={(e) => e.target.select()}
                    />
                    <small>
                      {t("expires")}:{" "}
                      {new Date(credential.expires_at).toLocaleString(language)}
                    </small>
                  </label>
                )}
                <label>
                  {t("prompt")}
                  <textarea
                    rows={4}
                    readOnly
                    aria-label={t("prompt")}
                    value={t("starter")}
                    onFocus={(e) => e.target.select()}
                  />
                </label>
              </details>
              <div className="play-detail-grid">
                <section>
                  <h4>{t("memory")}</h4>
                  <pre>{selected.memory_markdown || t("noMemory")}</pre>
                </section>
                <section>
                  <h4>{t("recap")}</h4>
                  <p className="play-prose">
                    {selected.summary || t("noRecap")}
                  </p>
                </section>
              </div>
              <section className="play-effects">
                <h4>{t("effects")}</h4>
                {!selected.consequences.length && <p>{t("noEffects")}</p>}
                {selected.consequences.map((effect) => (
                  <div className="play-effect" key={effect.id}>
                    <span className={"effect-status " + effect.status}>
                      {t(effect.status)}
                    </span>
                    <strong>{t(effect.category as PlayTextKey)}</strong>
                    <p className="play-prose">{effect.description}</p>
                    {effect.review_note && (
                      <p className="play-prose">{effect.review_note}</p>
                    )}
                    {selected.can_manage && effect.status === "pending" && (
                      <div className="play-decision">
                        <label>
                          {t("reviewNote")}
                          <input
                            maxLength={2000}
                            disabled={busy}
                            value={notes[effect.id] || ""}
                            onChange={(e) =>
                              setNotes((n) => ({
                                ...n,
                                [effect.id]: e.target.value,
                              }))
                            }
                          />
                        </label>
                        <button
                          disabled={busy}
                          onClick={() => run(() => review(effect, "approved"))}
                        >
                          {t("approve")}
                        </button>
                        <button
                          disabled={busy}
                          onClick={() => run(() => review(effect, "rejected"))}
                        >
                          {t("reject")}
                        </button>
                      </div>
                    )}
                  </div>
                ))}
              </section>
              <section className="play-journal">
                <h4>{t("journal")}</h4>
                {!total && <p>{t("noJournal")}</p>}
                {entries.map((entry) => (
                  <div key={entry.version}>
                    <small>
                      {t("version")} {entry.version} ·{" "}
                      {new Date(entry.created_at).toLocaleString(language)}
                    </small>
                    {entry.player_action && (
                      <p className="play-prose">
                        <strong>{t("action")}: </strong>
                        {entry.player_action}
                      </p>
                    )}
                    <p className="play-prose">
                      {entry.narration || entry.summary}
                    </p>
                  </div>
                ))}
                {total > 5 && (
                  <div className="play-buttons">
                    <button
                      disabled={busy || offset === 0}
                      onClick={() =>
                        run(() => load(selected.id, Math.max(0, offset - 5)))
                      }
                    >
                      {t("previous")}
                    </button>
                    <button
                      disabled={busy || offset + 5 >= total}
                      onClick={() => run(() => load(selected.id, offset + 5))}
                    >
                      {t("next")}
                    </button>
                  </div>
                )}
              </section>
            </article>
          )}
        </>
      )}
    </section>
  );
}
