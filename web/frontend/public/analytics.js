/* Optional first-party bootstrap. No Google request before explicit analytics consent.
   Forks/self-hosted installs default to disabled. Never read application text or URL parameters. */
(() => {
  "use strict";
  const KEY = "nf-analytics-consent-v1";
  const TTL = 180 * 24 * 60 * 60 * 1000;
  const denied = {
    analytics_storage: "denied",
    ad_storage: "denied",
    ad_user_data: "denied",
    ad_personalization: "denied",
  };
  const allowedEvents = new Set([
    "open_workshop",
    "sign_up_click",
    "github_click",
    "support_click",
  ]);
  let config,
    id,
    scope,
    loaded = false,
    consent = null,
    panel,
    settings;
  const pl = () => document.documentElement.lang === "pl";
  const text = (a, b) => (pl() ? a : b);
  const gtag = function () {
    window.dataLayer.push(arguments);
  };
  function context() {
    if (scope === "app") {
      const path = /^\/campaigns(?:\/|$)/.test(location.pathname)
        ? "/campaigns/"
        : "/workshop/";
      return {
        page_location: location.origin + path,
        page_title:
          path === "/campaigns/"
            ? "Narrative Framework | Campaign wiki"
            : "Narrative Framework | Workshop",
        page_referrer: "",
        content_group: "app",
      };
    }
    const raw = document.querySelector('link[rel="canonical"]')?.href;
    if (!raw) return null;
    const canonical = new URL(raw);
    if (
      canonical.origin !== "https://narrativeframework.com" ||
      canonical.search ||
      canonical.hash
    )
      return null;
    const campaign = {};
    const query = new URLSearchParams(location.search);
    const allowed = {
      utm_source: ["github", "community", "social"],
      utm_medium: ["post", "profile", "link"],
      utm_campaign: ["public_preview", "campaign_wiki", "downtime"],
    };
    for (const [key, values] of Object.entries(allowed)) {
      const value = query.get(key);
      if (values.includes(value))
        campaign[
          {
            utm_source: "campaign_source",
            utm_medium: "campaign_medium",
            utm_campaign: "campaign_name",
          }[key]
        ] = value;
    }
    return {
      ...campaign,
      page_location: canonical.href,
      page_title: document.title,
      page_referrer: "",
      content_group: "public",
    };
  }
  function track(name) {
    if (!consent || !loaded || window["ga-disable-" + id]) return;
    const safe = context();
    if (safe) gtag("event", name, { ...safe, send_to: id });
  }
  function readConsent() {
    try {
      const value = JSON.parse(localStorage.getItem(KEY));
      return value &&
        typeof value.analytics === "boolean" &&
        value.expiresAt > Date.now()
        ? value.analytics
        : null;
    } catch {
      return null;
    }
  }
  function clearCookies() {
    for (const item of document.cookie.split(";")) {
      const name = item.split("=")[0].trim();
      if (!/^_ga(?:_|$)/.test(name)) continue;
      // Google cookies are configured host-only; also remove historical parent-domain variants.
      const parts = location.hostname.split(".");
      document.cookie = name + "=; Max-Age=0; path=/; SameSite=Lax";
      for (let i = 0; i < parts.length - 1; i++)
        document.cookie =
          name +
          "=; Max-Age=0; path=/; domain=." +
          parts.slice(i).join(".") +
          "; SameSite=Lax";
    }
  }
  function start() {
    if (loaded || !consent || !context()) return;
    loaded = true;
    window["ga-disable-" + id] = false;
    window.dataLayer = window.dataLayer || [];
    window.gtag = gtag;
    gtag("consent", "default", denied);
    gtag("consent", "update", { ...denied, analytics_storage: "granted" });
    gtag("js", new Date());
    gtag("set", context());
    gtag("config", id, {
      ...context(),
      send_page_view: false,
      ignore_referrer: true,
      allow_google_signals: false,
      allow_ad_personalization_signals: false,
      cookie_domain: "none",
      cookie_expires: TTL / 1000,
    });
    const script = document.createElement("script");
    script.async = true;
    script.id = "nf-google-tag";
    script.referrerPolicy = "no-referrer";
    script.src =
      "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(id);
    document.head.append(script);
    track("page_view");
  }
  function save(value) {
    const hadLoaded = loaded;
    consent = value;
    try {
      localStorage.setItem(
        KEY,
        JSON.stringify({ analytics: value, expiresAt: Date.now() + TTL }),
      );
    } catch {
      /* Current-page preference still works when storage is blocked. */
    }
    panel.hidden = true;
    if (value) start();
    else {
      window["ga-disable-" + id] = true;
      clearCookies();
      document.getElementById("nf-google-tag")?.remove();
      if (hadLoaded) location.reload(); // Remove installed Google listeners after withdrawal.
    }
  }
  function render() {
    panel.replaceChildren();
    const heading = document.createElement("h2");
    heading.id = "nf-consent-title";
    heading.textContent = text(
      "Analityka za Twoją zgodą",
      "Analytics with your permission",
    );
    const p = document.createElement("p");
    p.textContent = text(
      "Czy pozwalasz nam użyć Google Analytics, aby sprawdzić, jak korzystasz ze strony? Odmowa nie ogranicza działania warsztatu.",
      "May we use Google Analytics to understand site usage? Declining does not limit the workshop.",
    );
    const actions = document.createElement("div");
    actions.className = "nf-consent-actions";
    for (const [label, value] of [
      [text("Bez analityki", "No analytics"), false],
      [text("Zgadzam się na analitykę", "Allow analytics"), true],
    ]) {
      const b = document.createElement("button");
      b.type = "button";
      b.textContent = label;
      b.addEventListener("click", () => save(value));
      actions.append(b);
    }
    const links = document.createElement("p");
    links.className = "nf-consent-links";
    for (const [label, url] of [
      [text("Prywatność", "Privacy"), "/privacy.html"],
      ["Cookies", "/cookies.html"],
    ]) {
      const a = document.createElement("a");
      a.textContent = label;
      a.href = url;
      links.append(a);
    }
    panel.append(heading, p, actions, links);
    settings.textContent = text("Ustawienia analityki", "Analytics settings");
  }
  async function init() {
    try {
      const response = await fetch("/analytics-config.json", {
        cache: "no-store",
      });
      if (!response.ok) return;
      config = await response.json();
      if (
        !Array.isArray(config.allowedHosts) ||
        !config.allowedHosts.includes(location.hostname)
      )
        return;
      scope = /^\/(workshop|campaigns)(?:\/|$)/.test(location.pathname)
        ? "app"
        : "public";
      if (scope === "app" && !config.appManualOnlyConfirmed) return;
      id =
        scope === "app" ? config.appMeasurementId : config.landingMeasurementId;
      if (typeof id !== "string" || !/^G-[A-Z0-9]{6,20}$/.test(id)) return;
      if (!context()) return;
      consent = readConsent();
      panel = document.createElement("section");
      panel.id = "nf-consent";
      panel.setAttribute("aria-labelledby", "nf-consent-title");
      panel.setAttribute("aria-label", "Analytics preferences");
      panel.hidden = consent !== null;
      settings = document.createElement("button");
      settings.id = "nf-consent-settings";
      settings.type = "button";
      settings.addEventListener("click", () => {
        render();
        panel.hidden = false;
        panel.querySelector("button")?.focus();
      });
      render();
      document.body.append(panel, settings);
      new MutationObserver(render).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["lang"],
      });
      document.addEventListener("click", (event) => {
        const name =
          event.target instanceof Element
            ? event.target
                .closest("[data-analytics]")
                ?.getAttribute("data-analytics")
            : null;
        if (allowedEvents.has(name)) track(name);
      });
      window.addEventListener("nf:page-view", () => track("page_view"));
      window.addEventListener("storage", (event) => {
        if (event.key === KEY && readConsent() !== consent) location.reload();
      });
      start();
    } catch {
      /* Analytics must never break the site. */
    }
  }
  if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", init, { once: true });
  else init();
})();
