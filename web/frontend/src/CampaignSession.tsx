import { useEffect, useState } from "react";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import RuntimeDialog from "./RuntimeDialog";
import RuntimeKey from "./RuntimeKey";
import type {
  Actor,
  Blocker,
  CampaignView,
  Event,
  Request,
  Runtime,
} from "./campaignRuntimeTypes";

type Props = {
  runtime: Runtime;
  view: CampaignView;
  pages: Actor[];
  pl: boolean;
  busy: boolean;
  request: Request;
  campaignId: string;
  onPage: (id: string) => void;
  onAdd: (kind: string) => void;
  onPlan: () => void;
  onEdit: (e: Event) => void;
  onInspect: (data: Record<string, unknown>, title: string) => Promise<void>;
  command: (data: Record<string, unknown>) => Promise<boolean>;
  reason: (b: Blocker) => string;
};

function CharacterSheet({
  actor,
  close,
  ...p
}: Props & { actor: Actor; close: () => void }) {
  const { runtime, pl, busy, request, campaignId, command } = p;
  const t = (a: string, b: string) => (pl ? a : b);
  const [page, setPage] = useState<{ body: string; gm_notes?: string } | null>(
    null,
  );
  const [error, setError] = useState("");
  const [stat, setStat] = useState({ key: "", value: 0 });
  const [saved, setSaved] = useState("");
  const stats = runtime.actors[actor.id] || {};
  useEffect(() => {
    let active = true;
    void request(`campaigns/${campaignId}/pages/${actor.id}/`)
      .then((value) => {
        if (active) setPage(value);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [campaignId, actor.id]);
  const name = (id: string) => p.pages.find((a) => a.id === id)?.title || id;
  const related = runtime.events.filter(
    (e) =>
      e.InvolvedActors.includes(actor.id) ||
      e.conditions.some((c) => c.actorId === actor.id) ||
      e.choices.some((c) =>
        c.outcomes.some(
          (o) => o.actorId === actor.id || o.otherActorId === actor.id,
        ),
      ),
  );
  return (
    <RuntimeDialog title={actor.title} onClose={close} busy={busy}>
      <p className="session-eyebrow">
        {actor.kind === "pc"
          ? t("POSTAĆ GRACZA", "PLAYER CHARACTER")
          : actor.kind === "npc"
            ? "NPC"
            : t("ORGANIZACJA", "ORGANIZATION")}
      </p>
      <h3>{t("Aktualne statystyki", "Current stats")}</h3>
      {Object.keys(stats).length ? (
        <dl className="runtime-values">
          {Object.entries(stats).map(([k, v]) => (
            <div key={k}>
              <dt>{k}</dt>
              <dd>{v}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p>
          {t(
            "Dodaj pierwszą statystykę tej postaci.",
            "Add this character's first stat.",
          )}
        </p>
      )}
      <form
        className="runtime-fields"
        onSubmit={async (e) => {
          e.preventDefault();
          setSaved("");
          const ok = await command({
            action: "set_value",
            scope: "actors",
            actorId: actor.id,
            ...stat,
          });
          setSaved(
            ok
              ? t("Zapisano statystykę.", "Stat saved.")
              : t(
                  "Nie zapisano. Zamknij kartę i odśwież stan kampanii.",
                  "Not saved. Close the sheet and reload the campaign state.",
                ),
          );
        }}
      >
        <RuntimeKey
          label={t("Statystyka", "Stat")}
          value={stat.key}
          keys={Object.keys(stats)}
          pl={pl}
          onChange={(key) => setStat({ key, value: stats[key] ?? 0 })}
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
        <button disabled={busy}>{t("Zapisz statystykę", "Save stat")}</button>
      </form>
      {saved && <p role="status">{saved}</p>}
      <h3>{t("Opis postaci", "Character description")}</h3>
      {error ? (
        <p role="alert">{error}</p>
      ) : page ? (
        <div className="wiki-markdown">
          <Markdown remarkPlugins={[remarkGfm]}>
            {page.body || t("Brak opisu.", "No description yet.")}
          </Markdown>
          {page.gm_notes && (
            <details>
              <summary>{t("Notatki MG", "GM notes")}</summary>
              <Markdown remarkPlugins={[remarkGfm]}>{page.gm_notes}</Markdown>
            </details>
          )}
        </div>
      ) : (
        <p role="status">{t("Wczytywanie opisu…", "Loading description…")}</p>
      )}
      <button
        onClick={() => {
          close();
          p.onPage(actor.id);
        }}
      >
        {t("Edytuj opis i publikację", "Edit description and publication")}
      </button>
      <h3>{t("Relacje liczbowe", "Relationship values")}</h3>
      {Object.entries(runtime.relationships).flatMap(([source, targets]) =>
        Object.entries(targets)
          .filter(([target]) => source === actor.id || target === actor.id)
          .flatMap(([target, values]) =>
            Object.entries(values).map(([key, value]) => (
              <p key={`${source}-${target}-${key}`}>
                {name(source)} → {name(target)} · {key}:{" "}
                <strong>{value}</strong>
              </p>
            )),
          ),
      )}
      {!Object.entries(runtime.relationships).some(
        ([source, targets]) => source === actor.id || actor.id in targets,
      ) && (
        <p className="wiki-hint">
          {t(
            "Relacje zapisane przez rozstrzygnięcia wydarzeń pojawią się tutaj.",
            "Relationship values set by event resolutions appear here.",
          )}
        </p>
      )}
      <h3>{t("Wydarzenia z udziałem postaci", "Related events")}</h3>
      {related.length ? (
        <ul>
          {related.map((e) => (
            <li key={e.id}>
              {e.title} · {t("dni", "days")} {e.MinTimeUnit}–{e.MaxTimeUnit} ·{" "}
              {e.blockers.length
                ? e.blockers.map(p.reason).join(" · ")
                : t("Dostępne teraz", "Available now")}
            </li>
          ))}
        </ul>
      ) : (
        <p>{t("Brak powiązanych wydarzeń.", "No related events.")}</p>
      )}
    </RuntimeDialog>
  );
}

export default function CampaignSession(p: Props) {
  const {
    runtime,
    view,
    pages,
    pl,
    busy,
    onAdd,
    onPlan,
    onEdit,
    onInspect,
    reason,
  } = p;
  const t = (a: string, b: string) => (pl ? a : b);
  const [actor, setActor] = useState<Actor | null>(null);
  const [filter, setFilter] = useState("all");
  const [query, setQuery] = useState("");
  useEffect(() => setFilter("all"), [view]);
  const actors = pages.filter((p) =>
    ["npc", "pc", "organization"].includes(p.kind),
  );
  const archived = (e: Event) =>
    e.blockers.some((b) => ["completed", "expired"].includes(b.code));
  const active = runtime.events.filter((e) => !archived(e));
  const current = active
    .filter((e) => !e.blockers.length)
    .sort((a, b) => a.MaxTimeUnit - b.MaxTimeUnit);
  const future = active
    .filter((e) => e.MinTimeUnit > runtime.day)
    .sort((a, b) => a.MinTimeUnit - b.MinTimeUnit);
  const blocked = active.filter(
    (e) => e.MinTimeUnit <= runtime.day && e.blockers.length,
  );
  const archive = runtime.events
    .filter(archived)
    .sort((a, b) => b.MaxTimeUnit - a.MaxTimeUnit);
  const actorButton = (id: string) => {
    const a = actors.find((a) => a.id === id);
    return a ? (
      <button
        className="session-actor-link"
        key={id}
        onClick={() => setActor(a)}
      >
        {a.title}
      </button>
    ) : null;
  };
  const eventCard = (e: Event, compact = false) => (
    <article
      className={`runtime-event ${!e.blockers.length ? "is-ready" : ""}`}
      key={e.id}
    >
      <div className="runtime-heading">
        <h3>{e.title}</h3>
        <span>
          {t("Dni", "Days")} {e.MinTimeUnit}–{e.MaxTimeUnit}
        </span>
      </div>
      <p className={!e.blockers.length ? "runtime-ready" : "wiki-hint"}>
        {e.blockers.length
          ? e.blockers.map(reason).join(" · ")
          : e.MaxTimeUnit === runtime.day
            ? t("Ostatni dzień na decyzję", "Last day to decide")
            : t("Dostępne teraz", "Available now")}
      </p>
      {e.InvolvedActors.length > 0 && (
        <div className="session-participants">
          {e.InvolvedActors.map(actorButton)}
        </div>
      )}
      <details className="session-event-details" open={!compact || undefined}>
        <summary>
          {t("Sytuacja i rozstrzygnięcia", "Situation and resolutions")}
        </summary>
        {e.description && <p>{e.description}</p>}
        {e.choices.map((c, i) => (
          <div className="runtime-choice" key={i}>
            <button
              disabled={busy || e.blockers.length > 0}
              onClick={() =>
                void onInspect(
                  { action: "resolve", eventId: e.id, choice: i },
                  `${e.title}: ${c.text}`,
                )
              }
            >
              {t("Sprawdź skutki", "Review consequences")}: {c.text}
            </button>
          </div>
        ))}
        {!e.blockers.some((b) => b.code === "completed") && (
          <button disabled={busy} onClick={() => onEdit(e)}>
            {t("Edytuj wydarzenie", "Edit event")}
          </button>
        )}
      </details>
    </article>
  );
  const actorList = (
    <div className="session-characters">
      {actors
        .filter(
          (a) =>
            (filter === "all" || a.kind === filter) &&
            a.title.toLocaleLowerCase().includes(query.toLocaleLowerCase()),
        )
        .map((a) => (
          <button
            key={a.id}
            className="session-character"
            onClick={() => setActor(a)}
          >
            <span className="session-eyebrow">
              {a.kind === "pc"
                ? t("POSTAĆ GRACZA", "PLAYER CHARACTER")
                : a.kind === "npc"
                  ? "NPC"
                  : t("ORGANIZACJA", "ORGANIZATION")}
            </span>
            <strong>{a.title}</strong>
            <span>
              {Object.entries(runtime.actors[a.id] || {})
                .slice(0, 4)
                .map(([k, v]) => `${k}: ${v}`)
                .join(" · ") ||
                t(
                  "Otwórz kartę i dodaj statystyki",
                  "Open sheet and add stats",
                )}
            </span>
          </button>
        ))}
    </div>
  );
  return (
    <>
      {view === "run" && (
        <div className="session-columns">
          <div>
            <div className="runtime-heading">
              <h2>{t("Do rozegrania teraz", "Ready to play")}</h2>
              <span>{current.length}</span>
            </div>
            {current.length ? (
              current.map((e) => eventCard(e))
            ) : (
              <div className="session-empty">
                <h3>
                  {runtime.events.length
                    ? t(
                        "Na dziś nie ma dostępnych wydarzeń.",
                        "No events are available today.",
                      )
                    : t(
                        "Co ma się wydarzyć na najbliższej sesji?",
                        "What should happen next session?",
                      )}
                </h3>
                <p>
                  {runtime.events.length
                    ? t(
                        "Sprawdź terminy i blokady obok. Możesz też przygotować nowe wydarzenie.",
                        "Check the upcoming dates and blockers, or prepare a new event.",
                      )
                    : t(
                        "Zaplanuj sytuację, termin i możliwe decyzje. Silnik policzy ich skutki oraz wskaże, co wydarzy się dalej.",
                        "Plan a situation, its timing and possible decisions. The engine calculates their consequences and shows what becomes available next.",
                      )}
                </p>
                <button onClick={onPlan}>
                  {t("Zaplanuj wydarzenie", "Plan an event")}
                </button>
              </div>
            )}
            {archive.length > 0 && (
              <details className="session-archive">
                <summary>
                  {t("Rozegrane i po terminie", "Resolved and expired")} (
                  {archive.length})
                </summary>
                {archive.map((e) => eventCard(e, true))}
              </details>
            )}
          </div>
          <aside className="session-context">
            <h2>{t("Nadchodzi", "Coming up")}</h2>
            {future.length ? (
              future.slice(0, 4).map((e) => eventCard(e, true))
            ) : (
              <p className="wiki-hint">
                {t(
                  "Brak zaplanowanych późniejszych wydarzeń.",
                  "No later events planned.",
                )}
              </p>
            )}
            {future.length > 4 && (
              <p>
                {t(
                  "Pełny plan znajdziesz w osi czasu.",
                  "See the timeline for the full plan.",
                )}
              </p>
            )}
            <h2>{t("Czeka na warunki", "Waiting on conditions")}</h2>
            {blocked.length ? (
              blocked.map((e) => eventCard(e, true))
            ) : (
              <p className="wiki-hint">
                {t(
                  "Brak zablokowanych wydarzeń na dziś.",
                  "No blocked events today.",
                )}
              </p>
            )}
            <h2>{t("Drużyna", "Party")}</h2>
            <dl className="runtime-values">
              {Object.entries(runtime.resources).map(([k, v]) => (
                <div key={k}>
                  <dt>{k}</dt>
                  <dd>{v}</dd>
                </div>
              ))}
            </dl>
            <div className="session-participants">
              {actors
                .filter((a) => a.kind === "pc")
                .map((a) => actorButton(a.id))}
            </div>
          </aside>
        </div>
      )}
      {view === "timeline" && (
        <>
          <div className="runtime-heading">
            <h2>{t("Co i kiedy", "What happens when")}</h2>
            <label>
              {t("Pokaż", "Show")}
              <select aria-label={t("Pokaż", "Show")}
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              >
                <option value="all">
                  {t("Aktualne i nadchodzące", "Current and upcoming")}
                </option>
                <option value="ready">
                  {t("Dostępne teraz", "Available now")}
                </option>
                <option value="archive">
                  {t("Rozegrane i po terminie", "Resolved and expired")}
                </option>
              </select>
            </label>
          </div>
          <div className="session-timeline">
            {(filter === "archive"
              ? archive
              : filter === "ready"
                ? current
                : active
            )
              .sort((a, b) => a.MinTimeUnit - b.MinTimeUnit)
              .map((e) => eventCard(e, true))}
          </div>
          {!runtime.events.length && (
            <p>
              {t(
                "Zaplanuj pierwsze wydarzenie, aby ustalić przebieg kampanii.",
                "Plan the first event to build your campaign timeline.",
              )}
            </p>
          )}
        </>
      )}
      {view === "actors" && (
        <>
          <div className="runtime-heading">
            <div>
              <h2>{t("Postacie i ich stan", "Characters and their state")}</h2>
              <p>
                {t(
                  "Opis, statystyki, relacje i wydarzenia w jednej karcie.",
                  "Description, stats, relationships and events in one sheet.",
                )}
              </p>
            </div>
            <button onClick={() => onAdd("npc")}>
              {t("Dodaj NPC", "Add NPC")}
            </button>
            <button onClick={() => onAdd("pc")}>
              {t("Dodaj postać gracza", "Add player character")}
            </button>
          </div>
          <div className="runtime-fields">
            <label>
              {t("Szukaj postaci", "Find character")}
              <input
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </label>
            <label>
              {t("Rodzaj postaci", "Character type")}
              <select aria-label={t("Rodzaj postaci", "Character type")}
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              >
                <option value="all">{t("Wszystkie", "All")}</option>
                <option value="pc">
                  {t("Postacie graczy", "Player characters")}
                </option>
                <option value="npc">NPC</option>
                <option value="organization">
                  {t("Organizacje", "Organizations")}
                </option>
              </select>
            </label>
          </div>
          {actorList}
          {!actors.length && (
            <p>
              {t(
                "Dodaj NPC lub postać gracza, a następnie uzupełnij jej statystyki w karcie.",
                "Add an NPC or player character, then set their stats in the sheet.",
              )}
            </p>
          )}
        </>
      )}
      {actor && (
        <CharacterSheet
          key={actor.id}
          {...p}
          actor={actor}
          close={() => setActor(null)}
        />
      )}
    </>
  );
}
