import { readFile, writeFile, mkdir, readdir, rm } from "node:fs/promises";
import { render } from "../.prerender/entry-server.js";
const shell = await readFile("dist/index.html", "utf8");
const config = JSON.parse(await readFile("dist/site-config.json", "utf8"));
const css = (await readdir("dist/assets")).find(
  (name) => name.startsWith("Landing-") && name.endsWith(".css"),
);
const locales = {
  en: [
    "Narrative Framework — an open RPG adventure workshop",
    "Connect scenes, characters and clues. An open-source RPG workshop with individual adventures and MCP.",
  ],
  pl: [
    "Narrative Framework — otwarty warsztat przygód RPG",
    "Łącz sceny, postacie i wskazówki. Otwarty warsztat RPG z przygodami indywidualnymi i MCP.",
  ],
};
for (const [lang, [title, description]] of Object.entries(locales)) {
  const canonical = `https://narrativeframework.com/${lang}/`;
  const meta = `<meta name="description" content="${description}"><link rel="canonical" href="${canonical}"><link rel="alternate" hreflang="en" href="https://narrativeframework.com/en/"><link rel="alternate" hreflang="pl" href="https://narrativeframework.com/pl/"><link rel="alternate" hreflang="x-default" href="https://narrativeframework.com/"><meta property="og:type" content="website"><meta property="og:title" content="${title}"><meta property="og:description" content="${description}"><meta property="og:url" content="${canonical}"><meta property="og:image" content="https://narrativeframework.com/images/harbor.jpg"><link rel="stylesheet" href="/assets/${css}">`;
  const html = shell
    .replace('<html lang="en">', `<html lang="${lang}">`)
    .replace(/<title>.*?<\/title>/, `<title>${title}</title>${meta}`)
    .replace(
      '<div id="root"></div>',
      `<div id="root">${render(lang, config)}</div>`,
    );
  await mkdir(`dist/${lang}`, { recursive: true });
  await writeFile(`dist/${lang}/index.html`, html);
  if (lang === "en")
    await writeFile(
      "dist/index.html",
      html.replace(
        `href="${canonical}"`,
        'href="https://narrativeframework.com/"',
      ),
    );
}
await mkdir("dist/workshop", { recursive: true });
await writeFile(
  "dist/workshop/index.html",
  shell.replace(
    "<title>Narrative Framework</title>",
    '<title>Narrative Framework — Workshop</title><meta name="robots" content="noindex">',
  ),
);
await mkdir("dist/campaigns", { recursive: true });
await writeFile(
  "dist/campaigns/index.html",
  shell.replace(
    "<title>Narrative Framework</title>",
    '<title>Narrative Framework — Campaign wiki</title><meta name="robots" content="noindex">',
  ),
);
await rm(".prerender", { recursive: true });
console.log("Prerendered EN/PL landing pages and a separate workshop entry.");
