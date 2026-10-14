import { readFile, writeFile, mkdir, rm } from "node:fs/promises";
import {
  render,
  renderBlog,
  posts,
  postPath,
  postBody,
} from "../.prerender/entry-server.js";
const origin = "https://narrativeframework.com";
const shell = await readFile("dist/index.html", "utf8");
const config = JSON.parse(await readFile("dist/site-config.json", "utf8"));
const manifest = JSON.parse(await readFile("dist/.vite/manifest.json", "utf8"));
const escape = (value) =>
  String(value).replace(
    /[&<>"']/g,
    (x) =>
      ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&apos;",
      })[x],
  );
const json = (value) => JSON.stringify(value).replace(/</g, "\\u003c");
const pages = [];
const languages = ["pl", "en"];
const locales = {
  en: [
    "Narrative Framework | an open RPG adventure workshop",
    "Connect scenes, characters and clues. An open-source RPG workshop with a shared campaign wiki, individual adventures and MCP.",
  ],
  pl: [
    "Narrative Framework | otwarty warsztat przygód RPG",
    "Łącz sceny, postacie i wskazówki. Otwarty warsztat RPG ze wspólną wiki kampanii, indywidualnymi przygodami i MCP.",
  ],
};
function cssFor(entry, seen = new Set()) {
  if (seen.has(entry)) return [];
  seen.add(entry);
  const item = manifest[entry];
  if (!item) throw Error(`Missing Vite manifest entry: ${entry}`);
  return [
    ...(item.css || []),
    ...(item.imports || []).flatMap((key) => cssFor(key, seen)),
  ];
}
async function page({
  path,
  lang,
  title,
  description,
  html,
  entry,
  alternates,
  article,
  structured,
}) {
  const canonical = origin + path;
  const meta = `<meta name="description" content="${escape(description)}"><link rel="canonical" href="${canonical}">${languages.map((l) => `<link rel="alternate" hreflang="${l}" href="${origin + alternates[l]}">`).join("")}<link rel="alternate" hreflang="x-default" href="${origin + alternates.en}"><meta property="og:type" content="${article ? "article" : "website"}"><meta property="og:site_name" content="Narrative Framework"><meta property="og:locale" content="${lang === "pl" ? "pl_PL" : "en_GB"}"><meta property="og:title" content="${escape(title)}"><meta property="og:description" content="${escape(description)}"><meta property="og:url" content="${canonical}"><meta property="og:image" content="${origin}/images/workshop.png"><meta name="twitter:card" content="summary_large_image">${article ? `<meta property="article:published_time" content="${article.date}T12:00:00Z">` : ""}<link rel="alternate" type="application/rss+xml" title="Narrative Framework ${lang.toUpperCase()}" href="/${lang}/feed.xml">${[...new Set(cssFor(entry))].map((css) => `<link rel="stylesheet" href="/${css}">`).join("")}<script type="application/ld+json">${json(structured || { "@context": "https://schema.org", "@type": "WebSite", name: "Narrative Framework", url: canonical, inLanguage: lang })}</script>`;
  const output = shell
    .replace('<html lang="en">', `<html lang="${lang}">`)
    .replace(/<title>.*?<\/title>/, `<title>${escape(title)}</title>${meta}`)
    .replace('<div id="root"></div>', `<div id="root">${html}</div>`);
  await mkdir(`dist${path}`, { recursive: true });
  await writeFile(`dist${path}index.html`, output);
  pages.push({ path, alternates, lastmod: article?.updated || article?.date });
}
for (const lang of languages) {
  const [title, description] = locales[lang];
  await page({
    path: `/${lang}/`,
    lang,
    title,
    description,
    html: render(lang, config),
    entry: "src/Landing.tsx",
    alternates: { pl: "/pl/", en: "/en/" },
  });
  await page({
    path: `/${lang}/blog/`,
    lang,
    title:
      lang === "pl"
        ? "Poradniki dla MG | Narrative Framework"
        : "RPG game master guides | Narrative Framework",
    description:
      lang === "pl"
        ? "Praktyczne poradniki o wiki kampanii RPG, downtime między sesjami i własnym warsztacie open source."
        : "Practical guides to sharing an RPG campaign wiki, between-session downtime and an open-source adventure workshop.",
    html: renderBlog(lang),
    entry: "src/Blog.tsx",
    alternates: { pl: "/pl/blog/", en: "/en/blog/" },
    structured: {
      "@context": "https://schema.org",
      "@type": "CollectionPage",
      name: lang === "pl" ? "Poradniki dla MG" : "Game master guides",
      url: `${origin}/${lang}/blog/`,
      inLanguage: lang,
    },
  });
}
await page({
  path: "/",
  lang: "en",
  title: locales.en[0],
  description: locales.en[1],
  html: render("en", config),
  entry: "src/Landing.tsx",
  alternates: { pl: "/pl/", en: "/en/" },
});
const ids = new Set();
for (const post of posts) {
  if (ids.has(post.id)) throw Error("Duplicate post ID");
  ids.add(post.id);
  if (
    !/^\d{4}-\d{2}-\d{2}$/.test(post.date) ||
    !Number.isFinite(Date.parse(post.date))
  )
    throw Error("Invalid article date");
  for (const lang of languages) {
    const data = post[lang],
      path = postPath(post, lang),
      canonical = origin + path;
    if (
      !/^[a-z0-9-]+$/.test(data.slug) ||
      !postBody(post, lang) ||
      !data.title ||
      !data.description
    )
      throw Error("Incomplete article translation");
    if (pages.some((p) => p.path === path))
      throw Error("Duplicate article URL");
    const article = {
      "@context": "https://schema.org",
      "@type": "BlogPosting",
      headline: data.title,
      description: data.description,
      author: {
        "@type": "Organization",
        name: "Narrative Framework",
        url: origin,
      },
      publisher: {
        "@type": "Organization",
        name: "Narrative Framework",
        url: origin,
      },
      image: [origin + "/images/workshop.png"],
      datePublished: post.date + "T12:00:00Z",
      dateModified: (post.updated || post.date) + "T12:00:00Z",
      inLanguage: lang,
      mainEntityOfPage: { "@type": "WebPage", "@id": canonical },
    };
    const breadcrumbs = {
      "@context": "https://schema.org",
      "@type": "BreadcrumbList",
      itemListElement: [
        {
          "@type": "ListItem",
          position: 1,
          name: lang === "pl" ? "Strona główna" : "Home",
          item: `${origin}/${lang}/`,
        },
        {
          "@type": "ListItem",
          position: 2,
          name: lang === "pl" ? "Poradniki" : "Guides",
          item: `${origin}/${lang}/blog/`,
        },
        { "@type": "ListItem", position: 3, name: data.title, item: canonical },
      ],
    };
    await page({
      path,
      lang,
      title: data.title + " | Narrative Framework",
      description: data.description,
      html: renderBlog(lang, data.slug),
      entry: "src/Blog.tsx",
      alternates: { pl: postPath(post, "pl"), en: postPath(post, "en") },
      article: post,
      structured: [article, breadcrumbs],
    });
  }
}
for (const route of ["workshop", "campaigns"]) {
  await mkdir(`dist/${route}`, { recursive: true });
  await writeFile(
    `dist/${route}/index.html`,
    shell.replace(
      /<title>.*?<\/title>/,
      `<title>Narrative Framework | ${route}</title><meta name="robots" content="noindex,nofollow">`,
    ),
  );
}
const sitemap = `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">${pages.map((p) => `<url><loc>${origin + p.path}</loc>${p.lastmod ? `<lastmod>${p.lastmod}</lastmod>` : ""}${languages.map((l) => `<xhtml:link rel="alternate" hreflang="${l}" href="${origin + p.alternates[l]}"/>`).join("")}</url>`).join("")}</urlset>`;
await writeFile("dist/sitemap.xml", sitemap);
await writeFile(
  "dist/robots.txt",
  `User-agent: *\nAllow: /\nDisallow: /api/\nDisallow: /mcp\nDisallow: /play/\nDisallow: /workshop\nDisallow: /campaigns\nSitemap: ${origin}/sitemap.xml\n`,
);
for (const lang of languages) {
  const name =
    lang === "pl"
      ? "Poradniki Narrative Framework"
      : "Narrative Framework guides";
  const rss = `<?xml version="1.0" encoding="UTF-8"?><rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom"><channel><title>${name}</title><link>${origin}/${lang}/blog/</link><description>${name}</description><language>${lang}</language><atom:link href="${origin}/${lang}/feed.xml" rel="self" type="application/rss+xml"/>${posts.map((p) => `<item><title>${escape(p[lang].title)}</title><link>${origin + postPath(p, lang)}</link><guid isPermaLink="true">${origin + postPath(p, lang)}</guid><pubDate>${new Date(p.date + "T12:00:00Z").toUTCString()}</pubDate><description>${escape(p[lang].description)}</description></item>`).join("")}</channel></rss>`;
  await writeFile(`dist/${lang}/feed.xml`, rss);
}
await writeFile(
  "dist/404.html",
  `<!doctype html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex"><title>Page not found | Narrative Framework</title><link rel="stylesheet" href="/analytics.css"></head><body><main style="max-width:640px;margin:15vh auto;padding:24px;font-family:system-ui;color:#1c302b"><h1>Page not found / Nie znaleziono strony</h1><p><a href="/en/blog/">English guides</a> · <a href="/pl/blog/">Poradniki po polsku</a></p><a href="/">Narrative Framework</a></main></body></html>`,
);
await rm("dist/.vite", { recursive: true });
await rm(".prerender", { recursive: true });
console.log(
  `Prerendered ${pages.length} public pages, private app entries, sitemap and PL/EN RSS.`,
);
