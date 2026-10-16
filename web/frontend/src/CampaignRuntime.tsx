import { useEffect, useRef, useState } from "react";
import { newUUID } from "./uuid";

import type {
  Actor,
  Effect,
  Event,
  Blocker,
  Runtime,
  Request,
  CampaignView,
} from "./campaignRuntimeTypes";
import CampaignEventEditor from "./CampaignEventEditor";
import RuntimeDialog from "./RuntimeDialog";
import CampaignSession from "./CampaignSession";
import RuntimeChanges from "./RuntimeChanges";
import RuntimeKey from "./RuntimeKey";
const splitFlags = (value: string) =>
  value
    .split(",")
    .map((v) => v.trim())
    .filter(Boolean);
const labels: Record<string, [string, string]> = {
  AddFlag: ["Dodaj fakt", "Add fact"],
  RemoveFlag: ["Usuń fakt", "Remove fact"],
  ModifyResource: ["Zmień zasób", "Change resource"],
  ModifyNarrativeVariable: ["Zmień wskaźnik świata", "Change world value"],
  ModifyActorAttribute: ["Zmień statystykę postaci", "Change character stat"],
  SetRelationship: ["Ustaw relację", "Set relationship"],
  SetNarrativeBoolTrue: ["Włącz stan", "Enable state"],
  SetNarrativeBoolFalse: ["Wyłącz stan", "Disable state"],
};

export default function CampaignRuntime({
  campaignId,
  version,
  pages,
  pl,
  request,
  onChanged,
  onPage,
  view,
  onAdd,
}: {
  campaignId: string;
  version: number;
  pages: Actor[];
  pl: boolean;
  request: Request;
  onChanged: () => Promise<void>;
  onPage: (id: string) => void;
  view: CampaignView;
  onAdd: (kind: string) => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [runtime, setRuntime] = useState<Runtime | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false);
  const [flags, setFlags] = useState("");
  const [stat, setStat] = useState({
    scope: "resources",
    actorId: "",
    key: "",
    value: 0,
  });
  const [event, setEvent] = useState<Event | null>(null);
  const [preview, setPreview] = useState<{
    data: Record<string, unknown>;
    before: Runtime;
    after: Runtime;
    title: string;
  } | null>(null);
  const [receipt, setReceipt] = useState<{
    before: Runtime;
    after: Runtime;
    title: string;
  } | null>(null);
  const requestRef = useRef(request);
  requestRef.current = request;
  const epoch = useRef(0);
  const actors = pages.filter((p) =>
    ["npc", "pc", "organization"].includes(p.kind),
  );
  const name = (id: string) => pages.find((p) => p.id === id)?.title || id;
  const label = (type: string) => labels[type]?.[pl ? 0 : 1] || type;
  const effectText = (effect: Effect) =>
    `${label(effect.type)}: ${effect.actorId ? name(effect.actorId) + " · " : ""}${effect.otherActorId ? name(effect.otherActorId) + " · " : ""}${effect.targetId}${["AddFlag", "RemoveFlag", "SetNarrativeBoolTrue", "SetNarrativeBoolFalse"].includes(effect.type) ? "" : ` (${effect.type === "SetRelationship" || effect.value < 0 ? "" : "+"}${effect.value})`}`;
  async function load() {
    const current = ++epoch.current;
    try {
      const result = await requestRef.current(
        `campaigns/${campaignId}/runtime/`,
      );
      if (current === epoch.current) {
        setRuntime(result);
        setFlags(result.flags.join(", "));
        setError("");
      }
    } catch (e) {
      if (current === epoch.current) setError((e as Error).message);
    }
  }
  useEffect(() => {
    void load();
    return () => {
      ++epoch.current;
    };
  }, [campaignId, version]);
  async function command(data: Record<string, unknown>) {
    if (!runtime || busy) return false;
    setBusy(true);
    setError("");
    try {
      ++epoch.current;
      const result = await request(`campaigns/${campaignId}/runtime/`, "POST", {
        ...data,
        version: runtime.version,
      });
      setRuntime(result);
      setFlags(result.flags.join(", "));
      try {
        await onChanged();
      } catch {
        setError(
          t(
            "Zapisano stan. Odśwież listę materiałów kampanii.",
            "State saved. Reload the campaign material list.",
          ),
        );
      }
      return true;
    } catch (e) {
      setError(
        t(
          "Nie zapisano. Odśwież stan przed ponowieniem: ",
          "Not saved. Reload state before retrying: ",
        ) + (e as Error).message,
      );
      return false;
    } finally {
      setBusy(false);
    }
  }
  function startEvent() {
    setEvent({
      id: newUUID(),
      title: "",
      description: "",
      MinTimeUnit: runtime?.day || 1,
      MaxTimeUnit: Math.min(99999, (runtime?.day || 1) + 7),
      requiredFlags: [],
      forbiddenFlags: [],
      InvolvedActors: [],
      conditions: [],
      choices: [{ text: "", outcomes: [] }],
      blockers: [],
    });
  }
  function reason(b: Blocker) {
    if (b.code === "early") return t(`Od dnia ${b.day}`, `From day ${b.day}`);
    if (b.code === "expired")
      return t(`Termin minął: dzień ${b.day}`, `Expired: day ${b.day}`);
    if (b.code === "completed") return t("Rozegrano", "Resolved");
    if (b.code === "missing_flag")
      return t(`Brakuje faktu: ${b.key}`, `Missing fact: ${b.key}`);
    if (b.code === "forbidden_flag")
      return t(`Blokuje fakt: ${b.key}`, `Blocked by fact: ${b.key}`);
    const operator = (
      { GreaterThan: ">", LessThan: "<", Equal: "=" } as Record<string, string>
    )[b.comparison || ""];
    return `${b.actorId ? name(b.actorId) + " · " : ""}${b.key}: ${b.current ?? t("brak wartości", "unset")} (${t("wymagane", "requires")} ${operator} ${b.value})`;
  }
  const actorSelect = (
    value: string,
    change: (value: string) => void,
    caption: string,
  ) => (
    <label>
      {caption}
      <select
        aria-label={caption}
        required
        value={value}
        onChange={(e) => change(e.target.value)}
      >
        <option value="">{t("Wybierz postać", "Choose a character")}</option>
        {actors.map((a) => (
          <option key={a.id} value={a.id}>
            {a.title}
          </option>
        ))}
      </select>
    </label>
  );
  async function inspect(data: Record<string, unknown>, title: string) {
    if (!runtime || busy) return;
    setBusy(true);
    setError("");
    try {
      const after = await request(
        `campaigns/${campaignId}/runtime/preview/`,
        "POST",
        { ...data, version: runtime.version },
      );
      setPreview({ data, before: runtime, after, title });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="campaign-runtime"
      hidden={view === "materials"}
      aria-label={t("Prowadzenie kampanii", "Run campaign")}
    >
      {error && (
        <p role="alert">
          {error}{" "}
          <button onClick={() => void load()}>
            {t("Odśwież stan", "Reload state")}
          </button>
        </p>
      )}
      {!runtime && !error && (
        <p role="status">{t("Wczytywanie kampanii…", "Loading campaign…")}</p>
      )}
      {runtime && (
        <>
          <div className="runtime-heading session-toolbar">
            <div>
              <span className="session-eyebrow">
                {t("CZAS W ŚWIECIE GRY", "IN-WORLD TIME")}
              </span>
              <h2>
                {t("Dzień", "Day")} {runtime.day}
              </h2>
            </div>
            <button disabled={busy} onClick={() => setOpen(true)}>
              {t("Stan świata i statystyki", "World state and stats")}
            </button>
            <button
              disabled={busy || runtime.day >= 99999}
              onClick={() =>
                void inspect(
                  { action: "advance" },
                  t(
                    `Przejście do dnia ${runtime.day + 1}`,
                    `Advance to day ${runtime.day + 1}`,
                  ),
                )
              }
            >
              {t("Następny dzień", "Next day")}
            </button>
            <button
              className="wiki-primary"
              disabled={busy}
              onClick={startEvent}
            >
              {t("Zaplanuj wydarzenie", "Plan an event")}
            </button>
          </div>
          {receipt && (
            <section className="runtime-receipt" role="status">
              <div className="runtime-heading">
                <h3>
                  {t("Zapisano", "Saved")}: {receipt.title}
                </h3>
                <button onClick={() => setReceipt(null)}>
                  {t("Ukryj wynik", "Dismiss result")}
                </button>
              </div>
              <RuntimeChanges
                before={receipt.before}
                after={receipt.after}
                pages={pages}
                pl={pl}
              />
            </section>
          )}
          <CampaignSession
            runtime={runtime}
            view={view}
            pages={pages}
            pl={pl}
            busy={busy}
            request={request}
            campaignId={campaignId}
            onPage={onPage}
            onAdd={onAdd}
            onPlan={startEvent}
            onEdit={(e) => setEvent(structuredClone(e))}
            onInspect={inspect}
            command={command}
            reason={reason}
          />
          {open && (
            <RuntimeDialog
              title={t("Stan świata", "World state")}
              onClose={() => setOpen(false)}
              busy={busy}
            >
              <fieldset disabled={busy} className="runtime-state">
                {error && <p role="alert">{error}</p>}
                <legend>{t("Aktualny stan", "Current state")}</legend>
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void command({
                      action: "set_flags",
                      flags: splitFlags(flags),
                    });
                  }}
                >
                  <label>
                    {t(
                      "Fakty świata (oddziel przecinkami)",
                      "World facts (comma separated)",
                    )}
                    <input
                      value={flags}
                      onChange={(e) => setFlags(e.target.value)}
                    />
                  </label>
                  <button>{t("Zapisz fakty", "Save facts")}</button>
                </form>
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void command({ action: "set_value", ...stat });
                  }}
                  className="runtime-fields"
                >
                  <label>
                    {t("Rodzaj wartości", "Value type")}
                    <select
                      aria-label={t("Rodzaj wartości", "Value type")}
                      value={stat.scope}
                      onChange={(e) =>
                        setStat({
                          ...stat,
                          scope: e.target.value,
                          key: "",
                          value: 0,
                        })
                      }
                    >
                      <option value="resources">
                        {t("Zasób drużyny", "Party resource")}
                      </option>
                      <option value="variables">
                        {t("Wskaźnik świata", "World value")}
                      </option>
                      <option value="actors">
                        {t("Statystyka postaci", "Character stat")}
                      </option>
                    </select>
                  </label>
                  {stat.scope === "actors" &&
                    actorSelect(
                      stat.actorId,
                      (actorId) =>
                        setStat({ ...stat, actorId, key: "", value: 0 }),
                      t("Postać", "Character"),
                    )}
                  <RuntimeKey
                    key={stat.scope + stat.actorId}
                    label={t(
                      "Nazwa statystyki / zasobu",
                      "Stat / resource name",
                    )}
                    pl={pl}
                    value={stat.key}
                    keys={Object.keys(
                      stat.scope === "actors"
                        ? runtime.actors[stat.actorId] || {}
                        : stat.scope === "variables"
                          ? runtime.variables
                          : runtime.resources,
                    )}
                    onChange={(key) =>
                      setStat({
                        ...stat,
                        key,
                        value:
                          (stat.scope === "actors"
                            ? runtime.actors[stat.actorId]?.[key]
                            : stat.scope === "variables"
                              ? runtime.variables[key]
                              : runtime.resources[key]) ?? 0,
                      })
                    }
                  />
                  <label>
                    {t("Wartość", "Value")}
                    <input
                      type="number"
                      required
                      step="any"
                      min={-1e12}
                      max={1e12}
                      value={stat.value}
                      onChange={(e) =>
                        setStat({ ...stat, value: e.target.valueAsNumber })
                      }
                    />
                  </label>
                  <button>{t("Ustaw wartość", "Set value")}</button>
                </form>
                <dl className="runtime-values">
                  {Object.entries(runtime.resources).map(([k, v]) => (
                    <div key={k}>
                      <dt>{k}</dt>
                      <dd>{v}</dd>
                    </div>
                  ))}
                  {Object.entries(runtime.variables).map(([k, v]) => (
                    <div key={`v-${k}`}>
                      <dt>{k}</dt>
                      <dd>{v}</dd>
                    </div>
                  ))}
                  {Object.entries(runtime.bools).map(([k, v]) => (
                    <div key={`b-${k}`}>
                      <dt>{k}</dt>
                      <dd>{v ? t("tak", "yes") : t("nie", "no")}</dd>
                    </div>
                  ))}
                </dl>
              </fieldset>
            </RuntimeDialog>
          )}

          {event && (
            <RuntimeDialog
              title={t("Przygotuj wydarzenie", "Prepare an event")}
              onClose={() => {
                if (
                  confirm(
                    t(
                      "Odrzucić szkic wydarzenia?",
                      "Discard this event draft?",
                    ),
                  )
                )
                  setEvent(null);
              }}
              busy={busy}
            >
              {error && <p role="alert">{error}</p>}
              <CampaignEventEditor
                event={event}
                setEvent={setEvent}
                runtime={runtime}
                actors={actors}
                pl={pl}
                busy={busy}
                command={command}
                onClose={() => setEvent(null)}
              />
            </RuntimeDialog>
          )}
          {preview && (
            <RuntimeDialog
              title={preview.title}
              onClose={() => setPreview(null)}
              busy={busy}
            >
              <p>
                {t(
                  "Sprawdź skutki przed zapisaniem decyzji.",
                  "Review the consequences before saving your decision.",
                )}
              </p>
              <RuntimeChanges
                before={preview.before}
                after={preview.after}
                pages={pages}
                pl={pl}
              />
              {preview.data.action === "advance" && (
                <p className="wiki-hint">
                  {t(
                    "Upływ czasu zmienia dostępność wydarzeń. Konsekwencje zapisujesz, rozstrzygając wydarzenie.",
                    "Time changes event availability. Resolve an event to apply its consequences.",
                  )}
                </p>
              )}
              {error && <p role="alert">{error}</p>}
              {runtime.version !== preview.before.version && (
                <p role="alert">
                  {t(
                    "Stan kampanii zmienił się. Zamknij podgląd i sprawdź skutki ponownie.",
                    "The campaign changed. Close this preview and review the consequences again.",
                  )}
                </p>
              )}
              <div className="runtime-heading">
                <button
                  className="wiki-primary"
                  disabled={busy || runtime.version !== preview.before.version}
                  onClick={async () => {
                    if (
                      await command({
                        ...preview.data,
                        version: preview.before.version,
                      })
                    ) {
                      setReceipt(preview);
                      setPreview(null);
                    }
                  }}
                >
                  {t("Potwierdź i zapisz", "Confirm and save")}
                </button>
                <button disabled={busy} onClick={() => setPreview(null)}>
                  {t("Wróć", "Back")}
                </button>
              </div>
            </RuntimeDialog>
          )}
          {runtime.history.length > 0 && (
            <details className="runtime-history">
              <summary>
                {t("Co i kiedy się zmieniło", "What changed and when")} (
                {runtime.history.length})
              </summary>
              <ol>
                {[...runtime.history].reverse().map((h, i) => (
                  <li key={i}>
                    <strong>
                      {t("Dzień", "Day")} {h.day}
                    </strong>{" "}
                    ·{" "}
                    {h.action === "resolve"
                      ? `${h.title}: ${h.choice}`
                      : h.action === "add_event" || h.action === "update_event"
                        ? `${t("Zaplanowano", "Planned")}: ${h.title}`
                        : h.action === "advance"
                          ? `${t("Upływ czasu do dnia", "Time advanced to day")} ${h.after}`
                          : h.action === "set_flags"
                            ? `${t("Fakty świata", "World facts")}: ${JSON.stringify(h.before)} → ${JSON.stringify(h.after)}`
                            : `${h.actorId ? name(h.actorId) + " · " : ""}${h.key}: ${h.before ?? "—"} → ${h.after}`}
                    {h.outcomes && (
                      <ul>
                        {h.outcomes.map((o, j) => (
                          <li key={j}>{effectText(o)}</li>
                        ))}
                      </ul>
                    )}
                  </li>
                ))}
              </ol>
            </details>
          )}
        </>
      )}
    </section>
  );
}
