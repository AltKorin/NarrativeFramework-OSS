import type {
  Actor,
  Event,
  Choice,
  Effect,
  Runtime,
} from "./campaignRuntimeTypes";
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

export default function CampaignEventEditor({
  event,
  setEvent,
  runtime,
  busy,
  pl,
  actors,
  command,
  onClose,
}: {
  event: Event;
  setEvent: (event: Event) => void;
  runtime: Runtime;
  busy: boolean;
  pl: boolean;
  actors: Actor[];
  command: (command: Record<string, unknown>) => Promise<boolean>;
  onClose: () => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const label = (type: string) => labels[type]?.[pl ? 0 : 1] || type;
  function updateChoice(index: number, value: Choice) {
    if (event)
      setEvent({
        ...event,
        choices: event.choices.map((c, i) => (i === index ? value : c)),
      });
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
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        if (
          await command({
            action: runtime.events.some((e) => e.id === event.id)
              ? "update_event"
              : "add_event",
            event,
          })
        )
          onClose();
      }}
    >
      <fieldset disabled={busy} className="runtime-editor">
        <legend>
          {runtime.events.some((e) => e.id === event.id)
            ? t("Edytuj wydarzenie", "Edit event")
            : t("Nowe wydarzenie", "New event")}
        </legend>
        <label>
          {t("Co się wydarzy?", "What will happen?")}
          <input
            required
            maxLength={200}
            value={event.title}
            onChange={(e) => setEvent({ ...event, title: e.target.value })}
          />
        </label>
        <label>
          {t("Sytuacja i stawka", "Situation and stakes")}
          <textarea
            maxLength={4000}
            value={event.description}
            onChange={(e) =>
              setEvent({ ...event, description: e.target.value })
            }
          />
        </label>
        <div className="runtime-fields">
          <label>
            {t("Od dnia", "From day")}
            <input
              type="number"
              required
              min={1}
              max={99999}
              value={event.MinTimeUnit}
              onChange={(e) =>
                setEvent({
                  ...event,
                  MinTimeUnit: e.target.valueAsNumber,
                })
              }
            />
          </label>
          <label>
            {t("Do dnia (włącznie)", "Through day (inclusive)")}
            <input
              type="number"
              required
              min={event.MinTimeUnit}
              max={99999}
              value={event.MaxTimeUnit}
              onChange={(e) =>
                setEvent({
                  ...event,
                  MaxTimeUnit: e.target.valueAsNumber,
                })
              }
            />
          </label>
        </div>
        <details className="event-requirements">
          <summary>
            {t("Warunki i uczestnicy", "Conditions and participants")} (
            {event.conditions.length +
              event.requiredFlags.length +
              event.forbiddenFlags.length}{" "}
            · {event.InvolvedActors.length})
          </summary>
          <label>
            {t("Wymagane fakty (przecinki)", "Required facts (commas)")}
            <input
              key={event.id + "required"}
              defaultValue={event.requiredFlags.join(", ")}
              onChange={(e) =>
                setEvent({
                  ...event,
                  requiredFlags: splitFlags(e.target.value),
                })
              }
            />
          </label>
          <label>
            {t("Fakty blokujące (przecinki)", "Blocking facts (commas)")}
            <input
              key={event.id + "forbidden"}
              defaultValue={event.forbiddenFlags.join(", ")}
              onChange={(e) =>
                setEvent({
                  ...event,
                  forbiddenFlags: splitFlags(e.target.value),
                })
              }
            />
          </label>
          <fieldset>
            <legend>{t("Uczestnicy", "Participants")}</legend>
            <div className="session-participants">
              {actors.map((a) => (
                <label className="participant-checkbox" key={a.id}>
                  <input
                    type="checkbox"
                    checked={event.InvolvedActors.includes(a.id)}
                    onChange={(e) =>
                      setEvent({
                        ...event,
                        InvolvedActors: e.target.checked
                          ? [...event.InvolvedActors, a.id]
                          : event.InvolvedActors.filter((id) => id !== a.id),
                      })
                    }
                  />
                  {a.title}
                </label>
              ))}
            </div>
            {!actors.length && (
              <p>
                {t(
                  "Dodaj postacie w widoku Postacie, aby połączyć je z wydarzeniem.",
                  "Add characters in the Characters view to link them to an event.",
                )}
              </p>
            )}
          </fieldset>
          {event.conditions.map((c, i) => (
            <div className="runtime-fields" key={i}>
              <label>
                {t("Warunek", "Condition")}
                <select
                  aria-label={t("Warunek", "Condition")}
                  value={c.scope}
                  onChange={(e) =>
                    setEvent({
                      ...event,
                      conditions: event.conditions.map((v, n) =>
                        n === i ? { ...v, scope: e.target.value } : v,
                      ),
                    })
                  }
                >
                  <option value="resources">{t("Zasób", "Resource")}</option>
                  <option value="variables">
                    {t("Wskaźnik świata", "World value")}
                  </option>
                  <option value="actors">
                    {t("Statystyka postaci", "Character stat")}
                  </option>
                </select>
              </label>
              {c.scope === "actors" &&
                actorSelect(
                  c.actorId || "",
                  (actorId) =>
                    setEvent({
                      ...event,
                      conditions: event.conditions.map((v, n) =>
                        n === i ? { ...v, actorId } : v,
                      ),
                    }),
                  t("Postać warunku", "Condition character"),
                )}
              <RuntimeKey
                label={t("Nazwa wartości", "Value name")}
                pl={pl}
                value={c.key}
                keys={Object.keys(
                  c.scope === "actors"
                    ? runtime.actors[c.actorId || ""] || {}
                    : c.scope === "variables"
                      ? runtime.variables
                      : runtime.resources,
                )}
                onChange={(key) =>
                  setEvent({
                    ...event,
                    conditions: event.conditions.map((v, n) =>
                      n === i ? { ...v, key } : v,
                    ),
                  })
                }
              />
              <label>
                {t("Porównanie", "Comparison")}
                <select
                  aria-label={t("Porównanie", "Comparison")}
                  value={c.comparison}
                  onChange={(e) =>
                    setEvent({
                      ...event,
                      conditions: event.conditions.map((v, n) =>
                        n === i ? { ...v, comparison: e.target.value } : v,
                      ),
                    })
                  }
                >
                  <option value="GreaterThan">&gt;</option>
                  <option value="LessThan">&lt;</option>
                  <option value="Equal">=</option>
                </select>
              </label>
              <label>
                {t("Próg", "Threshold")}
                <input
                  required
                  type="number"
                  step="any"
                  value={c.value}
                  onChange={(e) =>
                    setEvent({
                      ...event,
                      conditions: event.conditions.map((v, n) =>
                        n === i ? { ...v, value: e.target.valueAsNumber } : v,
                      ),
                    })
                  }
                />
              </label>
              <button
                type="button"
                onClick={() =>
                  setEvent({
                    ...event,
                    conditions: event.conditions.filter((_, n) => n !== i),
                  })
                }
              >
                {t("Usuń warunek", "Remove condition")}
              </button>
            </div>
          ))}
          <button
            type="button"
            disabled={event.conditions.length >= 50}
            onClick={() =>
              setEvent({
                ...event,
                conditions: [
                  ...event.conditions,
                  {
                    scope: "resources",
                    key: "",
                    comparison: "GreaterThan",
                    value: 0,
                  },
                ],
              })
            }
          >
            {t("Dodaj warunek statystyki", "Add stat condition")}
          </button>
        </details>
        {event.choices.map((c, i) => (
          <fieldset key={i}>
            <legend>
              {t("Rozstrzygnięcie", "Resolution")} {i + 1}
            </legend>
            <label>
              {t(
                "Co postanowiono / co się stało?",
                "What was decided / what happened?",
              )}
              <input
                required
                maxLength={200}
                value={c.text}
                onChange={(e) =>
                  updateChoice(i, { ...c, text: e.target.value })
                }
              />
            </label>
            {c.outcomes.map((o, j) => {
              const update = (value: Effect) =>
                updateChoice(i, {
                  ...c,
                  outcomes: c.outcomes.map((x, n) => (n === j ? value : x)),
                });
              return (
                <div className="runtime-fields" key={j}>
                  <label>
                    {t("Konsekwencja", "Consequence")}
                    <select
                      aria-label={t("Konsekwencja", "Consequence")}
                      value={o.type}
                      onChange={(e) => update({ ...o, type: e.target.value })}
                    >
                      {Object.keys(labels).map((k) => (
                        <option key={k} value={k}>
                          {label(k)}
                        </option>
                      ))}
                    </select>
                  </label>
                  {["ModifyActorAttribute", "SetRelationship"].includes(
                    o.type,
                  ) &&
                    actorSelect(
                      o.actorId || "",
                      (actorId) => update({ ...o, actorId }),
                      t("Postać konsekwencji", "Consequence character"),
                    )}
                  {o.type === "SetRelationship" &&
                    actorSelect(
                      o.otherActorId || "",
                      (otherActorId) => update({ ...o, otherActorId }),
                      t("Druga postać", "Other character"),
                    )}
                  <RuntimeKey
                    label={t(
                      "Nazwa faktu / statystyki / relacji",
                      "Fact / stat / relationship name",
                    )}
                    pl={pl}
                    value={o.targetId}
                    keys={
                      o.type === "ModifyActorAttribute"
                        ? Object.keys(runtime.actors[o.actorId || ""] || {})
                        : o.type === "ModifyResource"
                          ? Object.keys(runtime.resources)
                          : o.type === "ModifyNarrativeVariable"
                            ? Object.keys(runtime.variables)
                            : o.type === "SetRelationship"
                              ? Object.keys(
                                  runtime.relationships[o.actorId || ""]?.[
                                    o.otherActorId || ""
                                  ] || {},
                                )
                              : o.type.includes("Bool")
                                ? Object.keys(runtime.bools)
                                : [
                                    ...new Set([
                                      ...runtime.flags,
                                      ...runtime.events.flatMap((e) => [
                                        ...e.requiredFlags,
                                        ...e.forbiddenFlags,
                                      ]),
                                    ]),
                                  ]
                    }
                    onChange={(targetId) => update({ ...o, targetId })}
                  />
                  {[
                    "ModifyResource",
                    "ModifyNarrativeVariable",
                    "ModifyActorAttribute",
                    "SetRelationship",
                  ].includes(o.type) && (
                    <label>
                      {o.type === "SetRelationship"
                        ? t("Nowa wartość", "New value")
                        : t("Zmiana (+ / −)", "Change (+ / −)")}
                      <input
                        required
                        type="number"
                        step="any"
                        value={o.value}
                        onChange={(e) =>
                          update({
                            ...o,
                            value: e.target.valueAsNumber,
                          })
                        }
                      />
                    </label>
                  )}
                  <button
                    type="button"
                    onClick={() =>
                      updateChoice(i, {
                        ...c,
                        outcomes: c.outcomes.filter((_, n) => n !== j),
                      })
                    }
                  >
                    {t("Usuń konsekwencję", "Remove consequence")}
                  </button>
                </div>
              );
            })}
            <button
              type="button"
              disabled={c.outcomes.length >= 50}
              onClick={() =>
                updateChoice(i, {
                  ...c,
                  outcomes: [
                    ...c.outcomes,
                    { type: "AddFlag", targetId: "", value: 0 },
                  ],
                })
              }
            >
              {t("Dodaj konsekwencję", "Add consequence")}
            </button>
            {event.choices.length > 1 && (
              <button
                type="button"
                onClick={() =>
                  setEvent({
                    ...event,
                    choices: event.choices.filter((_, n) => n !== i),
                  })
                }
              >
                {t("Usuń rozstrzygnięcie", "Remove resolution")}
              </button>
            )}
          </fieldset>
        ))}
        <div className="runtime-heading">
          <button
            type="button"
            disabled={event.choices.length >= 20}
            onClick={() =>
              setEvent({
                ...event,
                choices: [...event.choices, { text: "", outcomes: [] }],
              })
            }
          >
            {t("Dodaj inne rozstrzygnięcie", "Add alternative resolution")}
          </button>
          <button>{t("Zapisz wydarzenie", "Save event")}</button>
          <button type="button" onClick={() => onClose()}>
            {t("Anuluj", "Cancel")}
          </button>
        </div>
      </fieldset>
    </form>
  );
}
