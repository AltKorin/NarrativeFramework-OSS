export type Kind = "scene" | "npc" | "location" | "clue";
export type StoryNode = {
  id: string;
  kind: Kind;
  title: string;
  text: string;
  secret: string;
  x: number;
  y: number;
  wiki_origin?: { campaign_id: string; page_id: string; version: number };
};
export type Link = { id: string; source: string; target: string; kind: string };
export type Doc = {
  schema: string;
  start: string;
  nodes: StoryNode[];
  edges: Link[];
};
// Mirror server-side JSON/storage limits before importing or restoring untrusted data.
export function validJSON(value: unknown): boolean {
  const todo: [unknown, number][] = [[value, 0]];
  let count = 0;
  const safe = (value: string) => !/[\u0000\uD800-\uDFFF]/u.test(value);
  while (todo.length) {
    const [item, depth] = todo.pop()!;
    if (++count > 50000 || depth > 32) return false;
    if (typeof item === "string") {
      if (!safe(item)) return false;
    } else if (typeof item === "number") {
      if (!Number.isFinite(item)) return false;
    } else if (item === null || typeof item === "boolean") continue;
    else if (Array.isArray(item)) {
      for (const child of item) todo.push([child, depth + 1]);
    } else if (typeof item === "object") {
      for (const [key, child] of Object.entries(item)) {
        if (!safe(key)) return false;
        todo.push([child, depth + 1]);
      }
    } else return false;
  }
  return true;
}
export function validDocument(value: unknown): value is Doc {
  if (!validJSON(value)) return false;
  const d = value as Doc;
  if (
    !d ||
    d.schema !== "narrative-web/1" ||
    !Array.isArray(d.nodes) ||
    !d.nodes.length ||
    d.nodes.length > 250 ||
    !Array.isArray(d.edges) ||
    d.edges.length > 1000 ||
    new TextEncoder().encode(JSON.stringify(d)).length > 256000
  )
    return false;
  const ids = new Set(d.nodes.map((n) => n?.id));
  const links = new Set(d.edges.map((e) => e?.id));
  return (
    ids.size === d.nodes.length &&
    links.size === d.edges.length &&
    d.nodes.some((n) => n && n.id === d.start && n.kind === "scene") &&
    d.nodes.every(
      (n) =>
        n &&
        typeof n.id === "string" &&
        n.id.length > 0 &&
        n.id.length <= 80 &&
        typeof n.kind === "string" &&
        ["scene", "npc", "location", "clue"].includes(n.kind) &&
        typeof n.title === "string" &&
        n.title.trim().length > 0 &&
        n.title.length <= 200 &&
        ["text", "secret"].every(
          (k) =>
            typeof n[k as "text" | "secret"] === "string" &&
            n[k as "text" | "secret"].length <= 12000,
        ) &&
        ["x", "y"].every(
          (k) =>
            n[k as "x" | "y"] === undefined ||
            (typeof n[k as "x" | "y"] === "number" &&
              Number.isFinite(n[k as "x" | "y"]) &&
              Math.abs(n[k as "x" | "y"]) <= 100000),
        ),
    ) &&
    d.edges.every(
      (e) =>
        e &&
        typeof e.id === "string" &&
        e.id.length > 0 &&
        e.id.length <= 80 &&
        ids.has(e.source) &&
        ids.has(e.target) &&
        ["leads_to", "reveals", "involves", "located_at"].includes(e.kind),
    )
  );
}
