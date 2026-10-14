import { useEffect } from "react";
import Markdown, { defaultUrlTransform } from "react-markdown";
import remarkGfm from "remark-gfm";
import { posts, postPath, postBody, headingId } from "./posts";
import type { Language } from "./i18n";
import "./landing.css";
import "./blog.css";

export default function Blog({
  initialLang,
  slug,
}: { initialLang?: Language; slug?: string } = {}) {
  const lang: Language =
    initialLang || (location.pathname.startsWith("/pl/") ? "pl" : "en");
  const requested =
    slug ??
    (typeof location === "undefined"
      ? ""
      : location.pathname.split("/").filter(Boolean)[2] || "");
  const post = posts.find((p) => p[lang].slug === requested);
  const pl = lang === "pl";
  const t = (a: string, b: string) => (pl ? a : b);
  const body = post ? postBody(post, lang) : "";
  const headings = [...body.matchAll(/^## (.+)$/gm)].map((x) => x[1]);
  useEffect(() => {
    document.documentElement.lang = lang;
    try {
      localStorage.setItem("narrative-framework-language", lang);
    } catch {}
  }, [lang]);
  const languagePath = (other: Language) =>
    post ? postPath(post, other) : `/${other}/blog/`;
  return (
    <div className="landing nf-editorial">
      <a className="nf-skip" href="#main">
        {t("Przejdź do treści", "Skip to content")}
      </a>
      <header className="nf-header">
        <a className="nf-brand" href={`/${lang}/`}>
          Narrative<span>Framework</span>
        </a>
        <nav className="nf-blog-nav" aria-label={t("Nawigacja", "Navigation")}>
          <a href={`/${lang}/blog/`}>{t("Poradniki", "Guides")}</a>
          <a href={`/workshop/?lang=${lang}`} data-analytics="open_workshop">
            {t("Otwórz warsztat", "Open workshop")}
          </a>
          <a
            href="https://github.com/AltKorin/NarrativeFramework-OSS"
            data-analytics="github_click"
          >
            GitHub
          </a>
        </nav>
        <nav className="nf-languages" aria-label={t("Język", "Language")}>
          {(["pl", "en"] as const).map((l) => (
            <a
              key={l}
              href={languagePath(l)}
              aria-current={l === lang ? "page" : undefined}
            >
              {l.toUpperCase()}
            </a>
          ))}
        </nav>
      </header>
      <main id="main" className="nf-container">
        <nav
          className="nf-breadcrumb"
          aria-label={t("Ścieżka strony", "Breadcrumb")}
        >
          <a href={`/${lang}/`}>{t("Strona główna", "Home")}</a>
          <span aria-hidden="true">/</span>
          {post ? (
            <>
              <a href={`/${lang}/blog/`}>{t("Poradniki", "Guides")}</a>
              <span aria-hidden="true">/</span>
              <span>{post[lang].category}</span>
            </>
          ) : (
            <span>{t("Poradniki", "Guides")}</span>
          )}
        </nav>
        {requested && !post ? (
          <section className="nf-blog-heading">
            <h1>
              {t("Nie ma takiego artykułu.", "That article does not exist.")}
            </h1>
            <a href={`/${lang}/blog/`}>
              {t("Wróć do poradników", "Back to guides")}
            </a>
          </section>
        ) : post ? (
          <>
            <header className="nf-post-heading">
              <p className="nf-post-category">{post[lang].category}</p>
              <h1>{post[lang].title}</h1>
              <p className="nf-post-lead">{post[lang].description}</p>
              <p className="nf-post-byline">
                Narrative Framework ·{" "}
                <time dateTime={post.date}>
                  {new Date(post.date + "T12:00:00Z").toLocaleDateString(
                    pl ? "pl-PL" : "en-GB",
                    {
                      year: "numeric",
                      month: "long",
                      day: "numeric",
                      timeZone: "UTC",
                    },
                  )}
                </time>
              </p>
            </header>
            <div className="nf-post-layout">
              <aside className="nf-post-toc">
                <h2>{t("W tym artykule", "In this guide")}</h2>
                <ol>
                  {headings.map((h) => (
                    <li key={h}>
                      <a href={`#${headingId(h)}`}>{h}</a>
                    </li>
                  ))}
                </ol>
              </aside>
              <article className="nf-post-body">
                <Markdown
                  remarkPlugins={[remarkGfm]}
                  skipHtml
                  urlTransform={defaultUrlTransform}
                  components={{
                    h2: ({ children }) => (
                      <h2 id={headingId(String(children))}>{children}</h2>
                    ),
                    img: ({ src, alt }) =>
                      src?.startsWith("/images/") ? (
                        <img src={src} alt={alt || ""} loading="lazy" />
                      ) : null,
                  }}
                >
                  {body}
                </Markdown>
              </article>
            </div>
            <section className="nf-post-next">
              <h2>
                {t(
                  "Przygotuj następną przygodę",
                  "Prepare your next adventure",
                )}
              </h2>
              <p>
                {t(
                  "Demo działa bez konta. Kampanie i współdzielenie wiki wymagają zalogowania.",
                  "The demo works without an account. Campaigns and wiki sharing require login.",
                )}
              </p>
              <a
                className="nf-button nf-button-primary"
                href={`/workshop/?lang=${lang}`}
                data-analytics="open_workshop"
              >
                {t("Otwórz warsztat", "Open workshop")} ↗
              </a>
            </section>
            <section className="nf-related">
              <h2>{t("Czytaj dalej", "Keep reading")}</h2>
              {posts
                .filter((p) => p.id !== post.id)
                .map((p) => (
                  <a key={p.id} href={postPath(p, lang)}>
                    <span>{p[lang].category}</span>
                    <h3>{p[lang].title}</h3>
                    <p>{p[lang].description}</p>
                  </a>
                ))}
            </section>
          </>
        ) : (
          <>
            <section className="nf-blog-heading">
              <h1>
                {t(
                  "Świat rośnie między sesjami.",
                  "Worlds grow between sessions.",
                )}
              </h1>
              <p>
                {t(
                  "Poradniki o przygotowaniu przygód, wspólnej wiki i historiach, które wracają do drużyny.",
                  "Guides to preparing adventures, sharing a campaign wiki and bringing personal stories back to the table.",
                )}
              </p>
              <a className="nf-text-link" href={`/${lang}/feed.xml`}>
                RSS ↗
              </a>
            </section>
            <section className="nf-blog-feature">
              <div>
                <p className="nf-post-category">{posts[0][lang].category}</p>
                <h2>
                  <a href={postPath(posts[0], lang)}>{posts[0][lang].title}</a>
                </h2>
                <p>{posts[0][lang].description}</p>
                <a className="nf-text-link" href={postPath(posts[0], lang)}>
                  {t("Przeczytaj poradnik", "Read the guide")} ↗
                </a>
              </div>
              <figure>
                <img
                  src="/images/workshop.png"
                  alt={t(
                    "Graf przygody i materiały MG w edytorze Narrative Framework",
                    "Adventure graph and GM material in the Narrative Framework editor",
                  )}
                  width="1440"
                  height="1100"
                />
                <figcaption>
                  {t(
                    "Rzeczywisty widok edytora z fikcyjną przygodą demonstracyjną.",
                    "The actual editor with a fictional demonstration adventure.",
                  )}
                </figcaption>
              </figure>
            </section>
            <section
              className="nf-blog-list"
              aria-label={t("Pozostałe poradniki", "More guides")}
            >
              {posts.slice(1).map((p) => (
                <article key={p.id}>
                  <p>{p[lang].category}</p>
                  <div>
                    <h2>
                      <a href={postPath(p, lang)}>{p[lang].title}</a>
                    </h2>
                    <p>{p[lang].description}</p>
                  </div>
                  <a className="nf-text-link" href={postPath(p, lang)}>
                    {t("Czytaj", "Read")} ↗
                  </a>
                </article>
              ))}
            </section>
          </>
        )}
      </main>
      <footer className="nf-footer nf-container">
        <div>
          <a className="nf-brand" href={`/${lang}/`}>
            Narrative<span>Framework</span>
          </a>
          <p>
            {t(
              "Narzędzia do opowiadania wspólnych historii.",
              "Tools for telling shared stories.",
            )}
          </p>
        </div>
        <div>
          <a href={`/${lang}/blog/`}>
            {t("Wszystkie poradniki", "All guides")}
          </a>
          <a href={`/${lang}/feed.xml`}>RSS</a>
          <a href="/privacy.html">{t("Prywatność", "Privacy")}</a>
          <a href="/cookies.html">Cookies</a>
        </div>
      </footer>
    </div>
  );
}
