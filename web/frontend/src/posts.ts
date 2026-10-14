import manifest from "./content/posts.json";
import type { Language } from "./i18n";
const files = import.meta.glob("./content/posts/*.md", {
  query: "?raw",
  import: "default",
  eager: true,
}) as Record<string, string>;
export const posts = manifest;
export type Post = (typeof manifest)[number];
export const postPath = (post: Post, lang: Language) =>
  `/${lang}/blog/${post[lang].slug}/`;
export const postBody = (post: Post, lang: Language) =>
  files[`./content/posts/${post.id}.${lang}.md`];
export const headingId = (text: string) =>
  text
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/ł/g, "l")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
