import { useEffect, useState } from "react";
import { initialLanguage, type Language } from "./i18n";
import "./landing.css";

type SiteConfig = {
  applicationUrl: string;
  repositoryUrl: string | null;
  supportUrl: string | null;
};
const copy = {
  pl: {
    nav: ["Warsztat", "Między sesjami", "Historia", "Open source"],
    try: "Otwórz warsztat",
    signup: "Załóż konto",
    eyebrow: "OTWARTY WARSZTAT PRZYGÓD RPG",
    title: (
      <>
        Każda decyzja
        <br />
        ma dalszy ciąg.
      </>
    ),
    intro:
      "Łącz sceny, postacie i wskazówki. Przygotuj przygodę, która zostawia graczom miejsce na własną historię.",
    art: "Podróżnik na kamiennym moście przed nadmorskim miastem i latarnią we mgle.",
    introTitle: "Pomysł to początek.\nDaj mu strukturę.",
    introText:
      "Zamiast szukać ważnej wskazówki w kolejnym dokumencie, zobacz jej miejsce w przygodzie. Narrative Framework łączy materiały MG na jednej mapie powiązań.",
    items: [
      [
        "Sceny i powiązania",
        "Rozpisz możliwe drogi przez przygodę. Sprawdź, czy wszystkie sceny są osiągalne.",
      ],
      [
        "Wspólna wiki kampanii",
        "Opisuj postacie, miejsca i świat w artykułach. Łącz je na mapie i wykorzystuj w wielu przygodach.",
      ],
      [
        "Twoje notatki, Twoje tempo",
        "Oddziel opisy od sekretów MG. Eksportuj przygodę jako JSON lub Markdown.",
      ],
    ],
    screenshot:
      "Prawdziwy widok edytora: mapa scen, lista materiałów i notatki MG.",
    demoNote:
      "Demo bez konta zapisuje zmiany w tej przeglądarce. Konto pozwala przechowywać prywatne przygody na serwerze.",
    playTitle: "Historia nie kończy się\nrazem z sesją.",
    playText:
      "MG przygotowuje małą przygodę i udostępnia wybrane materiały. Gracz przeżywa ją z agentem w swoim kliencie MCP. Pamięć i dziennik zostają w warsztacie, a trwałe konsekwencje wracają do oceny MG.",
    modes: ["Downtime", "Nieobecna postać", "Solo"],
    samples: [
      {
        heading: "Wieczór przed wyprawą",
        brief:
          "Masz jeden wieczór w porcie. Dowiedz się, kto wykupił wszystkie mapy północnego wybrzeża.",
        action: "Pytam kartografkę o ostatnie zamówienie.",
        result:
          "Kartografka pamięta kupca z poparzoną dłonią. Proponowana konsekwencja: nowy trop do sprawdzenia na sesji.",
      },
      {
        heading: "Gdy drużyna była daleko",
        brief:
          "Pozostajesz w mieście, by odnaleźć dawnego sojusznika. Wróć do drużyny przed świtem.",
        action: "Sprawdzam, czy zostawił wiadomość w gospodzie.",
        result:
          "Pod ladą czeka zapieczętowany list. Proponowana konsekwencja: kontakt odnowiony, treść do omówienia z MG.",
      },
      {
        heading: "Jedna postać. Własny szlak.",
        brief:
          "Latarnia zgasła po raz pierwszy od stu lat. Sprawdź brzeg, zanim wypłyną rybacy.",
        action: "Idę ścieżką wzdłuż klifu.",
        result:
          "Na mokrych schodach widać świeże ślady. Pamięć i dziennik pozwolą wrócić do tej chwili w kolejnej rozmowie.",
      },
    ],
    brief: "Odprawa MG",
    action: "Decyzja gracza",
    result: "Dalszy ciąg",
    sampleNote:
      "Przykładowy przebieg — ilustracja procesu, nie rozmowa z modelem.",
    mcpNote:
      "Model i rozmowę zapewnia Twój klient MCP. Warsztat dostarcza materiały, pamięć i narzędzia. MG określa granice przygody i zasady zatwierdzania konsekwencji.",
    guide: "Jak działa rozgrywka",
    historyTitle: "Projekt zmienił kierunek.\nPomysł na otwartość został.",
    history: [
      "Zaczęło się od narzędzia do budowania narracji w Unity. Chciałem uporządkować rozgałęzione historie i ich konsekwencje — i podzielić się tym, co powstawało.",
      "Z czasem projekt mocno się zmienił. Główne rozwiązanie trafiło do pracy nad grą, ale nadal chciałem udostępnić wersję open source. Z tego pomysłu wyrósł ten warsztat: osobna, webowa przestrzeń do przygotowywania przygód RPG i zabawy z narracją.",
      "Daję go Wam do eksperymentowania, prowadzenia własnych historii i rozwijania po swojemu. To projekt w rozwoju. Można zajrzeć do kodu, postawić własną instancję albo po prostu sprawdzić, dokąd zaprowadzi Was pierwsza przygoda.",
    ],
    author: "Konrad Komorowski",
    authorRole: "Twórca Narrative Framework · Stolem Narrative Dev",
    openTitle: "Weź historię\nwe własne ręce.",
    openText:
      "Kod na licencji MIT. Własny serwer w Dockerze. Przygody, które możesz eksportować. Wybierz, jak chcesz korzystać z warsztatu.",
    source: "Kod na GitHubie",
    sourceText: "Przeglądaj kod, zgłaszaj błędy i rozwijaj własną wersję.",
    sourcePending: "Publiczne repozytorium jest w przygotowaniu.",
    support: "Postaw mi kawę",
    supportText:
      "Jeśli warsztat Ci się przydaje, możesz wesprzeć dalszą pracę przez Buy Me a Coffee.",
    supportPending:
      "Link do wsparcia pojawi się tutaj po uruchomieniu profilu.",
    docs: "Dokumentacja",
    docsText: "Zasady działania, kontrakt MCP i odtwarzalne testy odporności.",
    readDocs: "Czytaj instrukcję",
    license: "Licencja MIT",
    tests: "Testy odporności",
    protocol: "Kontrakt MCP",
    faqTitle: "Zanim ruszysz w drogę",
    faq: [
      [
        "Czy potrzebuję konta?",
        "Do sprawdzenia edytora — nie. Demo zapisuje szkic w tej przeglądarce. Konto jest potrzebne do prywatnych zapisów na serwerze, MCP i przygód indywidualnych.",
      ],
      [
        "Czy to samodzielny Mistrz Gry AI?",
        "Warsztat nie ma wbudowanego czatu ani własnego modelu. Łączysz go ze swoim klientem MCP. Agent prowadzi małą przygodę w ramach materiałów udostępnionych przez MG; zatwierdzone konsekwencje MG przenosi do głównej kampanii.",
      ],
      [
        "Czy mogę uruchomić własną wersję?",
        "Tak. Projekt korzysta z Dockera i bazy PostgreSQL. Instrukcja instalacji i kod są dostępne w publicznym repozytorium. Dane zapisane na własnej instancji pozostają na Twoim serwerze.",
      ],
      [
        "Co warto wiedzieć o wersji testowej?",
        "To rozwijana aplikacja do eksperymentów. Eksportuj ważne przygody. Konta nie wymagają adresu e-mail; odzyskiwanie zapomnianego hasła wymaga kontaktu z administratorem instancji.",
      ],
    ],
    footer: "Narzędzia do historii, które toczą się dalej.",
    menu: "Nawigacja",
    skip: "Przejdź do treści",
  },
  en: {
    nav: ["Workshop", "Between sessions", "The story", "Open source"],
    try: "Open workshop",
    signup: "Create account",
    eyebrow: "AN OPEN WORKSHOP FOR RPG ADVENTURES",
    title: (
      <>
        Every choice
        <br />
        has a next chapter.
      </>
    ),
    intro:
      "Connect scenes, characters and clues. Build adventures that leave room for your players to make the story their own.",
    art: "A traveler on a stone bridge faces a coastal city and lighthouse in the mist.",
    introTitle: "An idea is a beginning.\nGive it structure.",
    introText:
      "See where a clue belongs instead of searching through another document. Narrative Framework brings your GM materials together on a map of connections.",
    items: [
      [
        "Scenes and connections",
        "Lay out possible paths through an adventure. Check whether every scene can be reached.",
      ],
      [
        "A shared campaign wiki",
        "Write articles about characters, places and world lore. Connect them on a map and reuse them across adventures.",
      ],
      [
        "Your notes, your pace",
        "Separate descriptions from GM secrets. Export an adventure as JSON or Markdown.",
      ],
    ],
    screenshot:
      "The actual editor: scene graph, adventure materials and GM notes.",
    demoNote:
      "The account-free demo saves changes in this browser. An account lets you keep private adventures on the server.",
    playTitle: "The session ends.\nThe story carries on.",
    playText:
      "The GM prepares a small adventure and shares selected material. A player explores it with an agent in their own MCP client. Memory and journal stay in the workshop; lasting consequences return to the GM for review.",
    modes: ["Downtime", "Absent character", "Solo"],
    samples: [
      {
        heading: "The night before departure",
        brief:
          "You have one evening in the harbor. Find out who bought every map of the northern coast.",
        action: "I ask the cartographer about her last order.",
        result:
          "She remembers a merchant with a burned hand. Proposed consequence: a new lead to follow at the table.",
      },
      {
        heading: "While the party was away",
        brief:
          "You stay in town to find an old ally. Rejoin the party before dawn.",
        action: "I check whether he left a message at the inn.",
        result:
          "A sealed letter waits behind the counter. Proposed consequence: contact restored, with details for the GM to review.",
      },
      {
        heading: "One character. Your own path.",
        brief:
          "The lighthouse has gone dark for the first time in a century. Check the shore before the fishing boats depart.",
        action: "I take the path along the cliff.",
        result:
          "Fresh footprints mark the wet steps. Memory and journal let you return to this moment in another conversation.",
      },
    ],
    brief: "GM briefing",
    action: "Player choice",
    result: "What follows",
    sampleNote:
      "An illustrative example of the workflow, not a live model conversation.",
    mcpNote:
      "Your MCP client supplies the model and conversation. The workshop supplies material, memory and tools. The GM sets the adventure boundaries and consequence approval rules.",
    guide: "How individual play works",
    historyTitle: "The project changed direction.\nThe idea of sharing stayed.",
    history: [
      "It started as a tool for building narratives in Unity. I wanted to bring order to branching stories and their consequences — and share what I was making.",
      "Over time, the project changed considerably. The main solution moved into work on a game, but I still wanted to release an open-source version. That idea became this workshop: a separate web space for preparing RPG adventures and experimenting with narrative.",
      "It is here for you to experiment, tell your own stories and develop in your own direction. This is a project in progress. Explore the code, host your own instance, or simply see where your first adventure takes you.",
    ],
    author: "Konrad Komorowski",
    authorRole: "Creator of Narrative Framework · Stolem Narrative Dev",
    openTitle: "Make the story\nyour own.",
    openText:
      "MIT-licensed code. Your own server with Docker. Adventures you can export. Choose how you want to use the workshop.",
    source: "Code on GitHub",
    sourceText: "Explore the code, report bugs and develop your own version.",
    sourcePending: "The public repository is being prepared.",
    support: "Buy me a coffee",
    supportText:
      "If the workshop is useful to you, you can support its development through Buy Me a Coffee.",
    supportPending:
      "The support link will appear here when the profile is available.",
    docs: "Documentation",
    docsText:
      "How it works, the MCP contract and reproducible adversarial tests.",
    readDocs: "Read the guide",
    license: "MIT license",
    tests: "Adversarial tests",
    protocol: "MCP contract",
    faqTitle: "Before you set out",
    faq: [
      [
        "Do I need an account?",
        "Not to try the editor. The demo keeps a draft in this browser. An account is needed for private server saves, MCP access and individual adventures.",
      ],
      [
        "Is this a standalone AI Game Master?",
        "The workshop has no built-in chat or model. Connect your own MCP client. Its agent facilitates a small adventure using the material shared by the GM; the human GM reconciles approved consequences with the main campaign.",
      ],
      [
        "Can I host my own version?",
        "Yes. The project uses Docker and PostgreSQL. Setup instructions and source code are available in the public repository. Data on your own instance stays on your server.",
      ],
      [
        "What should I know about the test instance?",
        "This is an evolving application for experimentation. Export important adventures. Accounts require no email address; forgotten passwords require contacting the instance administrator.",
      ],
    ],
    footer: "Tools for stories that carry on.",
    menu: "Navigation",
    skip: "Skip to content",
  },
};
const ids = ["features", "between-sessions", "story", "open-source"];
function safeUrl(
  value: unknown,
  fallback: string | null = null,
): string | null {
  if (typeof value !== "string" || !value) return fallback;
  if (value.startsWith("/") && !value.startsWith("//")) return value;
  try {
    return new URL(value).protocol === "https:" ? value : fallback;
  } catch {
    return fallback;
  }
}
export default function Landing({
  initialLang,
  siteConfig,
}: { initialLang?: Language; siteConfig?: SiteConfig } = {}) {
  const [language, setLanguage] = useState<Language>(() =>
    initialLang
      ? initialLang
      : location.pathname.startsWith("/pl")
        ? "pl"
        : location.pathname.startsWith("/en")
          ? "en"
          : initialLanguage(),
  );
  const [mode, setMode] = useState(0);
  const [menu, setMenu] = useState(false);
  const [config, setConfig] = useState<SiteConfig>(
    siteConfig ?? {
      applicationUrl: "/workshop",
      repositoryUrl: null,
      supportUrl: null,
    },
  );
  const t = copy[language];
  useEffect(() => {
    fetch("/site-config.json")
      .then((r) => {
        if (!r.ok) throw Error();
        return r.json();
      })
      .then((c) =>
        setConfig({
          applicationUrl: safeUrl(c.applicationUrl, "/workshop")!,
          repositoryUrl: safeUrl(c.repositoryUrl),
          supportUrl: safeUrl(c.supportUrl),
        }),
      )
      .catch(() => {});
  }, []);
  useEffect(() => {
    document.documentElement.lang = language;
    document.title =
      language === "pl"
        ? "Narrative Framework — otwarty warsztat przygód RPG"
        : "Narrative Framework — an open RPG adventure workshop";
    document
      .querySelector('meta[name="description"]')
      ?.setAttribute("content", t.intro);
    try {
      localStorage.setItem("narrative-framework-language", language);
    } catch {}
  }, [language, t.intro]);
  const switchLanguage = (lang: Language) => {
    setLanguage(lang);
    history.replaceState(null, "", `/${lang}/${location.hash}`);
    setMenu(false);
  };
  const app = new URL(
    config.applicationUrl,
    typeof location === "undefined"
      ? "https://narrativeframework.com"
      : location.origin,
  );
  app.searchParams.set("lang", language);
  const appHref = config.applicationUrl.startsWith("/")
    ? app.pathname + app.search + app.hash
    : app.href;
  const signup = new URL(app);
  signup.searchParams.set("account", "register");
  const signupHref = config.applicationUrl.startsWith("/")
    ? signup.pathname + signup.search + signup.hash
    : signup.href;
  const sample = t.samples[mode];
  return (
    <div className="landing">
      <a className="nf-skip" href="#main">
        {t.skip}
      </a>
      <header className="nf-header">
        <a
          className="nf-brand"
          href={`/${language}/`}
          aria-label="Narrative Framework"
        >
          Narrative<span>Framework</span>
        </a>
        <button
          className="nf-menu"
          aria-label={t.menu}
          aria-expanded={menu}
          onClick={() => setMenu(!menu)}
        >
          ☰
        </button>
        <nav className={menu ? "nf-nav is-open" : "nf-nav"} aria-label={t.menu}>
          {t.nav.map((name, i) => (
            <a key={ids[i]} href={`#${ids[i]}`} onClick={() => setMenu(false)}>
              {name}
            </a>
          ))}
        </nav>
        <div
          className="nf-languages"
          aria-label={language === "pl" ? "Język" : "Language"}
        >
          {(["pl", "en"] as const).map((lang) => (
            <button
              key={lang}
              lang={lang}
              aria-pressed={language === lang}
              onClick={() => switchLanguage(lang)}
            >
              {lang.toUpperCase()}
            </button>
          ))}
        </div>
      </header>
      <main id="main">
        <section className="nf-hero nf-container">
          <div className="nf-hero-copy">
            <p className="nf-eyebrow">{t.eyebrow}</p>
            <h1>{t.title}</h1>
            <p className="nf-lead">{t.intro}</p>
            <div className="nf-actions">
              <a className="nf-button" href={appHref}>
                {t.try}
                <span aria-hidden="true">↗</span>
              </a>
              <a className="nf-text-link" href={signupHref}>
                {t.signup}
                <span aria-hidden="true">→</span>
              </a>
            </div>
          </div>
          <figure className="nf-hero-art">
            <img
              src="/images/harbor.webp"
              width="1536"
              height="1024"
              alt={t.art}
              fetchPriority="high"
            />
          </figure>
        </section>
        <section id="features" className="nf-workshop nf-container">
          <h2>{t.introTitle}</h2>
          <p className="nf-section-intro">{t.introText}</p>
          <figure className="nf-product">
            <div className="nf-product-toolbar">
              <span>Narrative Framework</span>
              <span>
                {language === "pl" ? "Warsztat przygód" : "Adventure workshop"}
              </span>
            </div>
            <img
              src="/images/workshop.png"
              width="1440"
              height="1100"
              loading="lazy"
              alt={t.screenshot}
            />
          </figure>
          <div className="nf-features">
            {t.items.map(([title, text]) => (
              <article key={title}>
                <h3>{title}</h3>
                <p>{text}</p>
              </article>
            ))}
          </div>
          <p className="nf-note">{t.demoNote}</p>
        </section>
        <section id="between-sessions" className="nf-play nf-container">
          <div className="nf-play-copy">
            <h2>{t.playTitle}</h2>
            <p>{t.playText}</p>
            <a
              className="nf-text-link"
              href={`/docs/${language}/individual-play.md`}
            >
              {t.guide} <span aria-hidden="true">↗</span>
            </a>
            <p className="nf-note">{t.mcpNote}</p>
          </div>
          <div className="nf-example">
            <div
              className="nf-mode-switch"
              role="group"
              aria-label={
                language === "pl" ? "Przykład przygody" : "Adventure example"
              }
            >
              {t.modes.map((name, i) => (
                <button
                  key={i}
                  aria-pressed={mode === i}
                  onClick={() => setMode(i)}
                >
                  {name}
                </button>
              ))}
            </div>
            <div className="nf-example-content" aria-live="polite">
              <h3>{sample.heading}</h3>
              <dl>
                <dt>{t.brief}</dt>
                <dd>{sample.brief}</dd>
                <dt>{t.action}</dt>
                <dd className="nf-player-line">{sample.action}</dd>
                <dt>{t.result}</dt>
                <dd>{sample.result}</dd>
              </dl>
            </div>
            <p className="nf-example-note">{t.sampleNote}</p>
          </div>
        </section>
        <section id="story" className="nf-story nf-container">
          <div className="nf-story-title">
            <h2>{t.historyTitle}</h2>
            <div className="nf-author">
              <strong>{t.author}</strong>
              <span>{t.authorRole}</span>
            </div>
          </div>
          <div className="nf-story-body">
            {t.history.map((text) => (
              <p key={text}>{text}</p>
            ))}
          </div>
        </section>
        <section id="open-source" className="nf-open nf-container">
          <h2>{t.openTitle}</h2>
          <p className="nf-section-intro">{t.openText}</p>
          <div className="nf-resources">
            <article>
              <span className="nf-resource-mark" aria-hidden="true">
                &lt;/&gt;
              </span>
              <h3>{t.source}</h3>
              <p>{t.sourceText}</p>
              {config.repositoryUrl ? (
                <a className="nf-text-link" href={config.repositoryUrl}>
                  GitHub <span aria-hidden="true">↗</span>
                </a>
              ) : (
                <p className="nf-note">{t.sourcePending}</p>
              )}
              <a className="nf-small-link" href="/docs/LICENSE.txt">
                {t.license}
              </a>
            </article>
            <article>
              <span className="nf-resource-mark" aria-hidden="true">
                MD
              </span>
              <h3>{t.docs}</h3>
              <p>{t.docsText}</p>
              <a
                className="nf-text-link"
                href={`/docs/${language}/individual-play.md`}
              >
                {t.readDocs} <span aria-hidden="true">↗</span>
              </a>
              <div className="nf-doc-links">
                <a href={`/docs/${language}/campaign-wiki.md`}>
                  {language === "pl" ? "Wiki kampanii" : "Campaign wiki"}
                </a>
                <a href={`/docs/${language}/mcp-play.md`}>{t.protocol}</a>
                <a href={`/docs/${language}/adversarial-testing.md`}>
                  {t.tests}
                </a>
              </div>
            </article>
            <article className="nf-support">
              <span className="nf-resource-mark" aria-hidden="true">
                ♡
              </span>
              <h3>{t.support}</h3>
              <p>{t.supportText}</p>
              {config.supportUrl ? (
                <a className="nf-text-link" href={config.supportUrl}>
                  Oil to my machine <span aria-hidden="true">↗</span>
                </a>
              ) : (
                <p className="nf-note">{t.supportPending}</p>
              )}
            </article>
          </div>
        </section>
        <section className="nf-faq nf-container">
          <h2>{t.faqTitle}</h2>
          <div>
            {t.faq.map(([question, answer]) => (
              <details key={question}>
                <summary>
                  {question}
                  <span aria-hidden="true">+</span>
                </summary>
                <p>{answer}</p>
              </details>
            ))}
          </div>
        </section>
      </main>
      <footer className="nf-footer nf-container">
        <div>
          <a className="nf-brand" href={`/${language}/`}>
            Narrative<span>Framework</span>
          </a>
          <p>{t.footer}</p>
        </div>
        <div>
          <span>© {new Date().getFullYear()} Stolem Narrative Dev</span>
          <a href="/docs/LICENSE.txt">{t.license}</a>
          <a href="/privacy.html">
            {language === "pl" ? "Prywatność" : "Privacy"}
          </a>
          <a href="/cookies.html">Cookies</a>
        </div>
      </footer>
    </div>
  );
}
