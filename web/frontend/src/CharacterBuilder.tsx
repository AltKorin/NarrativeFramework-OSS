import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { newUUID } from "./uuid";
import { ZoomImage } from "./Gallery";
import "./character-builder.css";

type Request = (path: string, method?: string, body?: unknown) => Promise<any>;
type Item = { id: string; label: string; [key: string]: any };
type Pack = {
  title: string;
  stats: Item[];
  skills: Item[];
  derived: Item[];
  pools: Item[];
  catalog: Item[];
  tables: Item[];
  roles: Item[];
  methods: Item[];
};
type Summary = { id: string; status: string; method: string; version: number; member: string | null; name: string | null };
type Draft = Summary & {
  review_note: string;
  page: string | null;
  ruleset: Pack;
  state: { role: string | null; template: number | null; stats: Record<string, number> | null; skills: Record<string, number>;
    gear: string[]; items: string[]; tables: Record<string, { table: string; result?: number; text: string }[]>;
    name: string | null; look: string; rerolls: string[] };
  stats: Record<string, number> | null;
  skills: Record<string, number>;
  items: string[];
  problems: string[];
  sheet: null | { derived: Record<string, number>; pools: Record<string, { label: string; max: number; current: number }>;
    price: number; warnings: string[] };
  events: { type: string; [key: string]: any }[];
};

// Ruleset entries may carry a campaign image ID; the provider turns it into a private URL.
const ImageUrl = createContext<{ url: (id?: string) => string | null; pl: boolean }>({ url: () => null, pl: true });
// Thumbnails inside choice buttons stay plain images (no button inside a button); elsewhere they zoom.
function Thumb({ id, zoom, caption }: { id?: string; zoom?: boolean; caption?: string }) {
  const images = useContext(ImageUrl);
  const url = images.url(id);
  if (!url) return null;
  return zoom ? <ZoomImage className="builder-thumb" src={url} caption={caption} pl={images.pl} />
    : <img className="builder-thumb" src={url} alt="" loading="lazy" />;
}

// Server rule messages are English; Polish UI maps them by pattern (pack labels pass through).
const PROBLEMS: [RegExp, string][] = [
  [/^Choose a role$/, "Wybierz rolę"],
  [/^Choose a template$/, "Wybierz szablon"],
  [/^Set statistics$/, "Ustal statystyki"],
  [/^Spend exactly (\d+) statistic points$/, "Rozdaj dokładnie $1 punktów statystyk"],
  [/^Skill points spent (\d+) of (\d+)$/, "Punkty umiejętności: wydano $1 z $2"],
  [/^(.+): too high$/, "$1: za wysoko"],
  [/^(.+): below the role package$/, "$1: poniżej pakietu roli"],
  [/^(.+): at least (\d+)$/, "$1: co najmniej $2"],
  [/^Choose (\d+) from the role package$/, "Wybierz $1 z pakietu roli"],
  [/^Purchases exceed the budget$/, "Zakupy przekraczają budżet"],
  [/^Item outside the allowed categories$/, "Przedmiot spoza dozwolonych kategorii"],
  [/^Roll (.+)$/, "Rzuć: $1"],
  [/^Name the character$/, "Nadaj postaci imię"],
  [/^(.+): requires (.+)$/, "$1: wymaga $2"],
  [/^(.+): max (\d+)$/, "$1: najwyżej $2 szt."],
  [/^(.+): no free slot$/, "$1: brak wolnego slotu"],
  [/^(.+): install as a pair in two hosts$/, "$1: montuj parami w dwóch wszczepach bazowych"],
  [/^(.+): below (-?\d+)$/, "$1: poniżej $2"],
  [/^unknown item$/, "Nieznany przedmiot"],
  [/^Only one of: (.+)$/, "Najwyżej jeden z: $1"],
];
const say = (pl: boolean, text: string) => {
  if (!pl) return text;
  const match = PROBLEMS.find(([pattern]) => pattern.test(text));
  return match ? text.replace(match[0], match[1]) : text;
};

const STATUS: Record<string, [string, string]> = {
  draft: ["W przygotowaniu", "In progress"],
  submitted: ["Czeka na MG", "Waiting for GM"],
  returned: ["Do poprawy", "Needs changes"],
  approved: ["Zatwierdzona", "Approved"],
};

export default function CharacterBuilder({ campaignId, gm, pl, request, onPage }: {
  campaignId: string;
  gm: boolean;
  pl: boolean;
  request: Request;
  onPage: (id: string) => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const base = `campaigns/${campaignId}/`;
  const [ruleset, setRuleset] = useState<{ revision: number; document: Pack } | null | undefined>(undefined);
  const [drafts, setDrafts] = useState<Summary[]>([]);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function run(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function load() {
    const rules = await request(base + "ruleset/").catch(() => null);
    setRuleset(rules);
    setDrafts(await request(base + "characters/"));
  }
  useEffect(() => {
    void run(load);
  }, [campaignId]);

  async function act(command: Record<string, unknown>) {
    if (!draft) return;
    const value = await request(`${base}characters/${draft.id}/`, "POST", { version: draft.version, ...command });
    setDraft(value);
    setDrafts(await request(base + "characters/"));
  }

  const imageUrl = (id?: string) => (id ? `/api/campaigns/${campaignId}/images/${id}/file/` : null);
  if (ruleset === undefined) return <section className="character-builder"><p>{t("Wczytywanie…", "Loading…")}</p></section>;
  return (
    <ImageUrl.Provider value={{ url: imageUrl, pl }}>
    <section className="character-builder" aria-labelledby="builder-title">
      {error && <p role="alert" className="builder-error">{error}</p>}
      {draft ? (
        <DraftEditor draft={draft} gm={gm} pl={pl} busy={busy} onBack={() => run(async () => { setDraft(null); await load(); })}
          act={(c) => run(() => act(c))} onPage={onPage}
          onDelete={() => run(async () => {
            await request(`${base}characters/${draft.id}/`, "DELETE", { version: draft.version });
            setDraft(null);
            await load();
          })} />
      ) : (
        <>
          <h2 id="builder-title">{t("Tworzenie postaci", "Character creation")}</h2>
          {gm && <RulesetPanel ruleset={ruleset} pl={pl} busy={busy}
            upload={(document) => run(async () => {
              await request(base + "ruleset/", "PUT", { version: ruleset?.revision || 0, document });
              await load();
            })}
            remove={() => run(async () => {
              const result = await request(base + "ruleset/", "DELETE", { version: ruleset!.revision });
              setError(result.drafts_in_progress ? t(`Zasady usunięte. ${result.drafts_in_progress} postaci w toku można dokończyć na starych zasadach.`, `Rules removed. ${result.drafts_in_progress} characters in progress can still be finished.`) : "");
              await load();
            })} />}
          {!ruleset && !gm && <p>{t("Prowadzący nie dodał jeszcze zasad tworzenia postaci do tej kampanii.", "The GM has not added character rules to this campaign yet.")}</p>}
          {gm && drafts.some((d) => d.status === "submitted") && (
            <h3>{t("Do zatwierdzenia", "Waiting for approval")}: {drafts.filter((d) => d.status === "submitted").length}</h3>
          )}
          <DraftList drafts={drafts} gm={gm} pl={pl} busy={busy}
            open={(id) => run(async () => setDraft(await request(`${base}characters/${id}/`)))} />
          {ruleset && (
            <NewDraft methods={ruleset.document.methods} pl={pl} busy={busy}
              create={(method) => run(async () => {
                setDraft(await request(base + "characters/", "POST", { id: newUUID(), method }));
              })} />
          )}
        </>
      )}
    </section>
    </ImageUrl.Provider>
  );
}

function RulesetPanel({ ruleset, pl, busy, upload, remove }: {
  ruleset: { revision: number; document: Pack } | null; pl: boolean; busy: boolean;
  upload: (document: unknown) => void; remove: () => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [pending, setPending] = useState<Pack | null>(null);
  const [error, setError] = useState("");
  async function read(file?: File) {
    setPending(null);
    setError("");
    if (!file) return;
    try {
      if (file.size > 450000) throw Error(t("Plik jest za duży (limit 450 KB).", "File too large (450 KB limit)."));
      const value = JSON.parse(await file.text());
      if (value?.schema !== "narrative-ruleset/1") throw Error(t("To nie jest pakiet zasad narrative-ruleset/1.", "Not a narrative-ruleset/1 pack."));
      setPending(value);
    } catch (e) {
      setError(e instanceof SyntaxError ? t("Niepoprawny plik JSON.", "Invalid JSON file.") : (e as Error).message);
    }
  }
  return (
    <div className="builder-ruleset">
      <p>
        {ruleset
          ? <>{t("Zasady kampanii", "Campaign rules")}: <strong>{ruleset.document.title}</strong> · {t("wersja", "revision")} {ruleset.revision}</>
          : t("Dodaj pakiet zasad (plik JSON), aby gracze mogli tworzyć postacie.", "Add a rules pack (JSON file) so players can create characters.")}
      </p>
      <label>{ruleset ? t("Podmień zasady (.json)", "Replace rules (.json)") : t("Pakiet zasad (.json)", "Rules pack (.json)")}
        <input type="file" accept=".json,application/json" disabled={busy} onChange={(e) => void read(e.target.files?.[0])} />
      </label>
      {error && <p role="alert">{error}</p>}
      {pending && (
        <div className="builder-preview">
          <p><strong>{pending.title}</strong> · {pending.stats?.length} {t("statystyk", "stats")} · {pending.skills?.length} {t("umiejętności", "skills")} · {pending.roles?.length} {t("ról", "roles")} · {pending.catalog?.length} {t("pozycji katalogu", "catalog items")} · {pending.methods?.length} {t("metod", "methods")}</p>
          {ruleset && <p>{t("Postacie w toku zostają na dotychczasowych zasadach; nowe powstaną na nowych.", "Characters in progress keep their rules; new ones use the new pack.")}</p>}
          <button className="primary" disabled={busy} onClick={() => { upload(pending); setPending(null); }}>{ruleset ? t("Podmień zasady", "Replace rules") : t("Dodaj zasady", "Add rules")}</button>
        </div>
      )}
      {ruleset && (
        <button disabled={busy} onClick={() => confirm(t("Usunąć zasady? Nowych postaci nie da się tworzyć, rozpoczęte można dokończyć.", "Remove the rules? New characters cannot be started; started ones can be finished.")) && remove()}>
          {t("Usuń zasady", "Remove rules")}
        </button>
      )}
    </div>
  );
}

function DraftList({ drafts, gm, pl, busy, open }: { drafts: Summary[]; gm: boolean; pl: boolean; busy: boolean; open: (id: string) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  if (!drafts.length) return <p>{gm ? t("Nikt jeszcze nie tworzy postaci.", "Nobody is creating a character yet.") : t("Nie masz jeszcze postaci.", "You have no characters yet.")}</p>;
  const order = ["submitted", "returned", "draft", "approved"];
  return (
    <ul className="builder-drafts">
      {[...drafts].sort((a, b) => order.indexOf(a.status) - order.indexOf(b.status)).map((d) => (
        <li key={d.id}>
          <button disabled={busy} onClick={() => open(d.id)}>
            <strong>{d.name || t("Bez imienia", "Unnamed")}</strong>
            <span className={`builder-status status-${d.status}`}>{STATUS[d.status]?.[pl ? 0 : 1] || d.status}</span>
            {gm && <small>{d.member ? t("gracz", "player") : t("postać MG", "GM character")}</small>}
          </button>
        </li>
      ))}
    </ul>
  );
}

function NewDraft({ methods, pl, busy, create }: { methods: Item[]; pl: boolean; busy: boolean; create: (method: string) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  return (
    <div className="builder-new">
      <h3>{t("Nowa postać", "New character")}</h3>
      <div className="builder-methods">
        {methods.map((m) => (
          <button key={m.id} disabled={busy} onClick={() => create(m.id)}>
            <Thumb id={m.image} />
            <strong>{m.label}</strong>
            {m.description && <span>{m.description}</span>}
          </button>
        ))}
      </div>
    </div>
  );
}

function DraftEditor({ draft, gm, pl, busy, act, onBack, onDelete, onPage }: {
  draft: Draft; gm: boolean; pl: boolean; busy: boolean;
  act: (command: Record<string, unknown>) => void; onBack: () => void; onDelete: () => void; onPage: (id: string) => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const method = pack.methods.find((m) => m.id === draft.method)!;
  const steps: Item[] = method.steps;
  const editable = ["draft", "returned"].includes(draft.status) && (!gm || draft.member === null);
  const [index, setIndex] = useState(0);
  const step = steps[Math.min(index, steps.length - 1)];
  const [note, setNote] = useState("");
  const [reroll, setReroll] = useState("");
  return (
    <div className="builder-editor">
      <div className="builder-editor-head">
        <button onClick={onBack} disabled={busy}>← {t("Lista postaci", "Characters")}</button>
        <h2 id="builder-title">{draft.state.name || t("Nowa postać", "New character")} · {method.label}</h2>
        <span className={`builder-status status-${draft.status}`}>{STATUS[draft.status]?.[pl ? 0 : 1]}</span>
      </div>
      {draft.status === "returned" && <p className="builder-note">{t("Uwaga prowadzącego", "GM note")}: {draft.review_note}</p>}
      {draft.status === "submitted" && !gm && <p className="builder-note">{t("Postać czeka na decyzję prowadzącego.", "Waiting for the GM's decision.")}</p>}
      {draft.status === "approved" && <p className="builder-note">{t("Postać zatwierdzona i dodana do kampanii.", "Approved and added to the campaign.")}{" "}
        {gm && draft.page && <button onClick={() => onPage(draft.page!)}>{t("Otwórz kartę", "Open sheet")}</button>}</p>}
      <div className="builder-grid">
        <ol className="builder-steps">
          {steps.map((s, i) => (
            <li key={i}><button aria-current={i === index ? "step" : undefined} onClick={() => setIndex(i)}>{stepLabel(s, pack, pl)}</button></li>
          ))}
        </ol>
        <div className="builder-step">
          <fieldset disabled={!editable || busy}>
            <legend>{stepLabel(step, pack, pl)}</legend>
            <Step key={`${index}-${draft.version}`} step={step} draft={draft} pl={pl} act={act} />
          </fieldset>
          <div className="builder-nav">
            {index > 0 && <button onClick={() => setIndex(index - 1)}>← {t("Wstecz", "Back")}</button>}
            {index < steps.length - 1 && <button className="primary" onClick={() => setIndex(index + 1)}>{t("Dalej", "Next")} →</button>}
          </div>
        </div>
        <SheetPanel draft={draft} pl={pl} />
      </div>
      <div className="builder-actions">
        {editable && (
          <button className="primary" disabled={busy || draft.problems.length > 0} onClick={() => act({ action: "submit" })}>
            {draft.member === null ? t("Zapisz postać w kampanii", "Save character to campaign") : t("Wyślij do prowadzącego", "Send to GM")}
          </button>
        )}
        {editable && draft.problems.length > 0 && <span>{t("Najpierw uzupełnij braki z listy obok.", "Fix the items listed next to the sheet first.")}</span>}
        {draft.status !== "approved" && (editable || gm) && (
          <button disabled={busy} onClick={() => confirm(t("Usunąć tę postać? Tej operacji nie można cofnąć.", "Delete this character? This cannot be undone.")) && onDelete()}>
            {t("Usuń postać", "Delete character")}
          </button>
        )}
      </div>
      {gm && draft.status === "submitted" && draft.member !== null && (
        <div className="builder-review">
          <h3>{t("Decyzja prowadzącego", "GM decision")}</h3>
          <button className="primary" disabled={busy} onClick={() => act({ action: "approve" })}>{t("Zatwierdź i dodaj kartę do kampanii", "Approve and add sheet to campaign")}</button>
          <label>{t("Uwaga dla gracza", "Note for the player")}
            <textarea value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} />
          </label>
          <button disabled={busy || !note.trim()} onClick={() => act({ action: "return", note })}>{t("Odeślij do poprawy", "Return for changes")}</button>
        </div>
      )}
      {gm && draft.status !== "approved" && (
        <div className="builder-review">
          <label>{t("Pozwól rzucić ponownie", "Allow a reroll")}
            <select value={reroll} onChange={(e) => setReroll(e.target.value)}>
              <option value="">—</option>
              <option value="stats">{t("Statystyki", "Statistics")}</option>
              {Object.keys(draft.state.tables).map((k) => <option key={k} value={k}>{pack.tables.find((x) => x.id === k)?.label || k}</option>)}
            </select>
          </label>
          <button disabled={busy || !reroll} onClick={() => act({ action: "grant_reroll", target: reroll })}>{t("Zezwól", "Allow")}</button>
          <details><summary>{t("Historia rzutów i wyborów", "Dice and choice log")}</summary>
            <ol>{draft.events.map((e, i) => <li key={i}><code>{e.type}</code> {JSON.stringify({ ...e, type: undefined })}</li>)}</ol>
          </details>
        </div>
      )}
    </div>
  );
}

function stepLabel(step: Item, pack: Pack, pl: boolean) {
  const t = (a: string, b: string) => (pl ? a : b);
  return ({
    role: t("Rola", "Role"), template: t("Szablon", "Template"), stats_points: t("Statystyki", "Statistics"),
    stats_role_roll: t("Statystyki (rzut)", "Statistics (roll)"), skills_points: t("Umiejętności", "Skills"),
    skills_package: t("Pakiet umiejętności", "Skill package"), tables: t("Historia", "Background"),
    gear_package: t("Sprzęt roli", "Role gear"), shop: t("Zakupy", "Shopping"), name: t("Imię i wygląd", "Name and look"),
  } as Record<string, string>)[step.type] || step.type;
}

function Step({ step, draft, pl, act }: { step: Item; draft: Draft; pl: boolean; act: (command: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const role = pack.roles.find((r) => r.id === draft.state.role);
  switch (step.type) {
    case "role":
      return (
        <div className="builder-choices">
          {pack.roles.map((r) => (
            <button key={r.id} aria-pressed={r.id === draft.state.role} onClick={() => act({ action: "choose_role", role: r.id })}>
              <Thumb id={r.image} />
              <strong>{r.label}</strong>
              {r.ability && <span>{r.ability.label} {r.ability.rank}</span>}
              {r.description && <span>{r.description}</span>}
            </button>
          ))}
        </div>
      );
    case "template":
      return role ? (
        <div className="builder-choices">
          {role.templates.map((tpl: Item, i: number) => (
            <button key={i} aria-pressed={draft.state.template === i} onClick={() => act({ action: "choose_template", index: i })}>
              <strong>{tpl.label}</strong>
            </button>
          ))}
        </div>
      ) : <p>{t("Najpierw wybierz rolę.", "Choose a role first.")}</p>;
    case "stats_points":
      return <StatPoints step={step} draft={draft} pl={pl} act={act} />;
    case "stats_role_roll":
      return (
        <div>
          {draft.stats && <StatRow pack={pack} stats={draft.stats} />}
          <button className="primary" disabled={!role || (draft.state.stats !== null && !draft.state.rerolls.includes("stats") && draft.events.some((e) => e.type === "stats_rolled"))}
            onClick={() => act({ action: "roll_stats" })}>{t("Rzuć na statystyki roli", "Roll role statistics")}</button>
        </div>
      );
    case "skills_package":
      return role ? (
        <ul>{Object.entries(role.skill_package as Record<string, number>).map(([k, v]) => <li key={k}>{pack.skills.find((s) => s.id === k)?.label} {v}</li>)}</ul>
      ) : <p>{t("Najpierw wybierz rolę.", "Choose a role first.")}</p>;
    case "skills_points":
      return <SkillPoints step={step} draft={draft} pl={pl} act={act} />;
    case "gear_package":
      return <GearPackage draft={draft} pl={pl} act={act} />;
    case "shop":
      return <Shop step={step} draft={draft} pl={pl} act={act} />;
    case "tables":
      return <Tables step={step} draft={draft} pl={pl} act={act} />;
    case "name":
      return <NameStep draft={draft} pl={pl} act={act} />;
    default:
      return null;
  }
}

function StatRow({ pack, stats }: { pack: Pack; stats: Record<string, number> }) {
  return (
    <table className="builder-stats"><tbody>
      <tr>{pack.stats.map((s) => <th key={s.id} title={s.label}>{s.short || s.id}</th>)}</tr>
      <tr>{pack.stats.map((s) => <td key={s.id}>{stats[s.id]}</td>)}</tr>
    </tbody></table>
  );
}

function StatPoints({ step, draft, pl, act }: { step: Item; draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const [values, setValues] = useState<Record<string, number>>(
    draft.state.stats || Object.fromEntries(pack.stats.map((s) => [s.id, s.min])));
  const spent = Object.values(values).reduce((a, b) => a + b, 0);
  return (
    <div>
      <p className={spent === step.pool ? "builder-ok" : "builder-counter"}>{t("Wydane punkty", "Points spent")}: {spent} / {step.pool}</p>
      <div className="builder-number-grid">
        {pack.stats.map((s) => (
          <label key={s.id} title={s.description}>{s.label}
            <input type="number" min={step.min ?? s.min} max={step.max ?? s.max} value={values[s.id]}
              onChange={(e) => setValues({ ...values, [s.id]: Number(e.target.value) })} />
          </label>
        ))}
      </div>
      <button className="primary" onClick={() => act({ action: "set_stats", values })}>{t("Zapisz statystyki", "Save statistics")}</button>
    </div>
  );
}

function SkillPoints({ step, draft, pl, act }: { step: Item; draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const role = pack.roles.find((r) => r.id === draft.state.role);
  const method = pack.methods.find((m) => m.id === draft.method)!;
  const template = role && draft.state.template !== null ? role.templates[draft.state.template] : null;
  const base: Record<string, number> = { ...(template?.skills || {}),
    ...(method.steps.some((s: Item) => s.type === "skills_package") && role ? role.skill_package : {}) };
  const [values, setValues] = useState<Record<string, number>>({ ...base, ...draft.state.skills });
  const spent = pack.skills.reduce((sum, s) => sum + Math.max(0, (values[s.id] || 0) - (base[s.id] || 0)) * s.cost, 0);
  const categories = [...new Set(pack.skills.map((s) => s.category || ""))];
  return (
    <div>
      <p className={spent === step.pool ? "builder-ok" : "builder-counter"}>{t("Wydane punkty", "Points spent")}: {spent} / {step.pool}</p>
      {categories.map((c) => (
        <div key={c}>
          {c && <h4>{c}</h4>}
          <div className="builder-number-grid">
            {pack.skills.filter((s) => (s.category || "") === c).map((s) => (
              <label key={s.id}>{s.label} <small>({s.stat}{s.cost > 1 ? ` · ×${s.cost}` : ""}{s.basic_min ? ` · min ${s.basic_min}` : ""})</small>
                <input type="number" min={base[s.id] || 0} max={Math.min(s.max, step.max ?? s.max)} value={values[s.id] || 0}
                  onChange={(e) => setValues({ ...values, [s.id]: Number(e.target.value) })} />
              </label>
            ))}
          </div>
        </div>
      ))}
      <button className="primary" onClick={() => act({ action: "set_skills", values: Object.fromEntries(Object.entries(values).filter(([, v]) => v > 0)) })}>
        {t("Zapisz umiejętności", "Save skills")}
      </button>
    </div>
  );
}

function costLabel(item: Item, pack: Pack) {
  return Object.entries(item.costs || {}).map(([pool, c]: [string, any]) =>
    `${pack.pools.find((p) => p.id === pool)?.label || pool} −${c.fixed} (${c.dice})`).join(", ");
}

function GearPackage({ draft, pl, act }: { draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const role = pack.roles.find((r) => r.id === draft.state.role);
  const [chosen, setChosen] = useState<string[]>(draft.state.gear);
  if (!role) return <p>{t("Najpierw wybierz rolę.", "Choose a role first.")}</p>;
  return (
    <div>
      {role.gear_package.map((group: { choose: number; items: string[] }, i: number) => (
        <div key={i} className="builder-group">
          <p>{t("Wybierz", "Choose")} {group.choose} ({chosen.filter((x) => group.items.includes(x)).length}/{group.choose})</p>
          {group.items.map((id) => {
            const item = pack.catalog.find((x) => x.id === id)!;
            return (
              <label key={id} className="builder-check">
                <Thumb id={item.image} />
                <input type="checkbox" checked={chosen.includes(id)}
                  onChange={(e) => setChosen(e.target.checked ? [...chosen, id] : chosen.filter((x) => x !== id))} />
                {item.label} <small>{costLabel(item, pack)}</small>
              </label>
            );
          })}
        </div>
      ))}
      <button className="primary" onClick={() => act({ action: "set_gear", items: chosen })}>{t("Zapisz wybór", "Save choice")}</button>
    </div>
  );
}

function Shop({ step, draft, pl, act }: { step: Item; draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const [cart, setCart] = useState<string[]>(draft.state.items);
  const [filter, setFilter] = useState("");
  const budget = (step.budgets || []).reduce((a: number, b: Item) => a + b.amount, 0);
  const currency = step.budgets?.[0]?.currency || "";
  const price = cart.reduce((sum, id) => sum + (pack.catalog.find((x) => x.id === id)?.price || 0), 0);
  const allowed: string[] = (step.budgets || []).flatMap((b: Item) => b.categories || []);
  const catalog = pack.catalog.filter((x) => (!allowed.length || allowed.includes(x.category))
    && (!filter || `${x.label} ${x.category}`.toLowerCase().includes(filter.toLowerCase())));
  const categories = [...new Set(catalog.map((x) => x.category || ""))];
  const count = (id: string) => cart.filter((x) => x === id).length;
  const remove = (id: string) => {
    const index = cart.lastIndexOf(id);
    const next = cart.filter((_, i) => i !== index);
    const orphans = pack.catalog.filter((x) => next.includes(x.id) && (x.uses_slot_of || []).length
      && !(x.uses_slot_of as string[]).some((h) => next.includes(h)));
    if (orphans.length && !confirm(t(`Usunięcie zabierze też: ${orphans.map((x) => x.label).join(", ")}. Kontynuować?`,
      `Removing it also removes: ${orphans.map((x) => x.label).join(", ")}. Continue?`))) return;
    setCart(next.filter((x) => !orphans.some((o) => o.id === x)));
  };
  return (
    <div>
      <p className={price > budget ? "builder-counter" : "builder-ok"}>{t("Wydane", "Spent")}: {price} / {budget} {currency}</p>
      {draft.sheet && Object.values(draft.sheet.pools).map((p) => <p key={p.label}>{p.label}: {p.current} / {p.max} <small>{t("(po zapisaniu koszyka)", "(after saving the cart)")}</small></p>)}
      <input type="search" placeholder={t("Szukaj w katalogu", "Search the catalog")} value={filter} onChange={(e) => setFilter(e.target.value)} />
      {categories.map((c) => (
        <div key={c}>
          <h4>{c}</h4>
          <ul className="builder-catalog">
            {catalog.filter((x) => (x.category || "") === c).map((x) => (
              <li key={x.id}>
                <Thumb id={x.image} zoom caption={x.label} />
                <div><strong>{x.label}</strong> · {x.price} {currency}{x.slots ? ` · ${x.slots} ${t("sloty", "slots")}` : ""}{x.uses_slot_of?.length ? ` · ${t("zajmuje", "uses")} ${x.slot_cost || 1} ${t("slot", "slot")}` : ""}
                  {x.costs && Object.keys(x.costs).length > 0 && <small> · {costLabel(x, pack)}</small>}
                  {x.description && <small className="builder-desc">{x.description}</small>}
                </div>
                <div className="builder-qty">
                  <button aria-label={t("Odejmij", "Remove")} disabled={!count(x.id)} onClick={() => remove(x.id)}>−</button>
                  <span>{count(x.id)}</span>
                  <button aria-label={t("Dodaj", "Add")} onClick={() => setCart([...cart, x.id])}>+</button>
                </div>
              </li>
            ))}
          </ul>
        </div>
      ))}
      <button className="primary" onClick={() => act({ action: "set_items", items: cart })}>{t("Zapisz koszyk", "Save cart")}</button>
    </div>
  );
}

function Tables({ step, draft, pl, act }: { step: Item; draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const role = pack.roles.find((r) => r.id === draft.state.role);
  const ids: string[] = [...(step.tables || []), ...(step.include_role_lifepath && role ? role.lifepath : [])];
  return (
    <div>
      {ids.map((id) => {
        const table = pack.tables.find((x) => x.id === id)!;
        const results = draft.state.tables[id];
        const locked = results && !draft.state.rerolls.includes(id);
        return (
          <div key={id} className="builder-group">
            <h4>{table.label}</h4>
            {results?.map((r, i) => <p key={i}>{r.result !== undefined && <code>{r.result}</code>} {r.text}</p>)}
            {!locked && (
              <>
                <button className="primary" onClick={() => act({ action: "roll_table", table: id })}>{t("Rzuć", "Roll")} {table.dice}</button>
                {table.choose && (
                  <select defaultValue="" onChange={(e) => e.target.value && act({ action: "choose_table", table: id, entry: Number(e.target.value) })}>
                    <option value="">{t("…albo wybierz", "…or choose")}</option>
                    {table.entries.map((entry: Item, i: number) => <option key={i} value={i}>{entry.text}</option>)}
                  </select>
                )}
              </>
            )}
          </div>
        );
      })}
    </div>
  );
}

function NameStep({ draft, pl, act }: { draft: Draft; pl: boolean; act: (c: Record<string, unknown>) => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [name, setName] = useState(draft.state.name || "");
  const [look, setLook] = useState(draft.state.look);
  return (
    <div className="builder-name">
      <label>{t("Imię lub ksywka", "Name or handle")}<input value={name} maxLength={120} onChange={(e) => setName(e.target.value)} /></label>
      <label>{t("Wygląd i krótki opis", "Look and short description")}<textarea value={look} maxLength={2000} onChange={(e) => setLook(e.target.value)} /></label>
      <button className="primary" disabled={!name.trim()} onClick={() => act({ action: "set_name", name, look })}>{t("Zapisz", "Save")}</button>
    </div>
  );
}

function SheetPanel({ draft, pl }: { draft: Draft; pl: boolean }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const pack = draft.ruleset;
  const labels = useMemo(() => Object.fromEntries(pack.derived.map((d) => [d.id, d.label])), [pack]);
  return (
    <aside className="builder-sheet" aria-label={t("Karta postaci", "Character sheet")}>
      <h3>{t("Karta", "Sheet")}</h3>
      {draft.stats && (
        <dl className="builder-stat-grid">
          {pack.stats.map((s) => <div key={s.id} title={s.label}><dt>{s.short || s.id}</dt><dd>{draft.stats![s.id]}</dd></div>)}
        </dl>
      )}
      {draft.sheet && (
        <ul>
          {Object.entries(draft.sheet.derived).map(([k, v]) => <li key={k}>{labels[k]}: <strong>{v}</strong></li>)}
          {Object.values(draft.sheet.pools).map((p) => <li key={p.label}>{p.label}: <strong>{p.current}</strong> / {p.max}</li>)}
        </ul>
      )}
      {draft.items.length > 0 && <p>{t("Sprzęt", "Gear")}: {draft.items.map((id) => pack.catalog.find((x) => x.id === id)?.label).join(", ")}</p>}
      {draft.sheet?.warnings.map((w) => <p key={w} className="builder-warning">⚠ {say(pl, w)}</p>)}
      {draft.problems.length > 0 ? (
        <>
          <h4>{t("Do uzupełnienia", "Still to do")}</h4>
          <ul className="builder-problems">{draft.problems.map((p) => <li key={p}>{say(pl, p)}</li>)}</ul>
        </>
      ) : <p className="builder-ok">{t("Postać kompletna.", "Character complete.")}</p>}
    </aside>
  );
}
