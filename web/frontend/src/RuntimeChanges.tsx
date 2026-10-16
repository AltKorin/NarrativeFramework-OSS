import type { Actor, Runtime } from "./campaignRuntimeTypes";

export default function RuntimeChanges({
  before,
  after,
  pages,
  pl,
}: {
  before: Runtime;
  after: Runtime;
  pages: Actor[];
  pl: boolean;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const name = (id: string) => pages.find((p) => p.id === id)?.title || id;
  const flatten = (r: Runtime) => {
    const entries: Record<string, { label: string; value: unknown }> = {};
    const add = (path: string[], label: string, value: unknown) => {
      entries[JSON.stringify(path)] = { label, value };
    };
    add(["day"], t("Dzień", "Day"), r.day);
    for (const scope of ["resources", "variables", "bools"] as const)
      for (const [k, v] of Object.entries(r[scope]))
        add(
          [scope, k],
          `${scope === "resources" ? t("Drużyna", "Party") : t("Świat", "World")} · ${k}`,
          v,
        );
    for (const key of r.flags)
      add(["flags", key], `${t("Fakt", "Fact")} · ${key}`, true);
    for (const [id, stats] of Object.entries(r.actors))
      for (const [k, v] of Object.entries(stats))
        add(["actors", id, k], `${name(id)} · ${k}`, v);
    for (const [source, targets] of Object.entries(r.relationships))
      for (const [target, stats] of Object.entries(targets))
        for (const [k, v] of Object.entries(stats))
          add(
            ["relationships", source, target, k],
            `${name(source)} → ${name(target)} · ${k}`,
            v,
          );
    return entries;
  };
  const a = flatten(before),
    b = flatten(after);
  const changes = [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(
    (k) => a[k]?.value !== b[k]?.value,
  );
  const display = (v: unknown) =>
    v === undefined
      ? t("brak", "unset")
      : typeof v === "boolean"
        ? v
          ? t("tak", "yes")
          : t("nie", "no")
        : String(v);
  const availability = after.events.filter((e) => {
    const prev = before.events.find((p) => p.id === e.id);
    return prev && JSON.stringify(prev.blockers) !== JSON.stringify(e.blockers);
  });
  const status = (e: Runtime["events"][number]) =>
    !e.blockers.length
      ? t("Dostępne teraz", "Available now")
      : e.blockers.some((b) => b.code === "completed")
        ? t("Rozegrane", "Resolved")
        : e.blockers.some((b) => b.code === "expired")
          ? t("Po terminie", "Expired")
          : e.blockers.some((b) => b.code === "early")
            ? t("Później", "Later")
            : t("Zablokowane", "Blocked");
  return (
    <div className="runtime-changes">
      {changes.length > 0 ? (
        <dl>
          {changes.map((k) => (
            <div key={k}>
              <dt>{b[k]?.label || a[k].label}</dt>
              <dd>
                <span>{display(a[k]?.value)}</span> →{" "}
                <strong>{display(b[k]?.value)}</strong>
              </dd>
            </div>
          ))}
        </dl>
      ) : (
        <p>
          {t(
            "Bez zmian statystyk i faktów świata.",
            "No changes to stats or world facts.",
          )}
        </p>
      )}
      {availability.length > 0 && (
        <>
          <h3>
            {t("Co zmieni się w wydarzeniach", "Event availability changes")}
          </h3>
          <ul>
            {availability.map((e) => (
              <li key={e.id}>
                <strong>{e.title}</strong> · {status(e)}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
