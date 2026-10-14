# Guides, SEO and optional analytics

[Polski: full guide](../pl/publishing-and-analytics.md)

## Publish a guide

`/en/blog/` and `/pl/blog/` are generated at build time, without a separate CMS/Node server. Add an entry to `web/frontend/src/content/posts.json` with a unique ID, original `date`, actual `updated` date, and each language's slug/title/description/category. Write matching `<id>.en.md` and `<id>.pl.md` files under `src/content/posts/`. H1 comes from metadata; sections start with `##`. Use local `/images/` assets. Keep published slugs stable or provide redirects.

Build with `npm --prefix web/frontend run build`. Output includes readable static HTML, canonical/hreflang, Open Graph, BlogPosting/BreadcrumbList JSON-LD, sitemap and language-specific RSS. Run `web/tests/content_http.py` and `blog_browser.py` against Compose. Private app entries are noindex and omitted from the sitemap; authorization still protects data. Unknown URLs return real 404 responses. The www hostname redirects permanently to the apex while preserving its URL.

Submit `https://narrativeframework.com/sitemap.xml` in a verified Google Search Console property. Feeds are `/en/feed.xml` and `/pl/feed.xml`. This does not guarantee rankings or indexing time. Publish useful GM examples and distribute individual guides under each community's promotion rules; the implementation does not post messages externally.

**Draft launch copy:** “I've open-sourced Narrative Framework under MIT: an RPG adventure graph, shared campaign wiki with selective publication, and individual downtime with an external MCP agent. Try the account-free demo or self-host it with Docker. Here's a practical guide to keeping campaign spoilers private: https://narrativeframework.com/en/blog/rpg-campaign-wiki-without-spoilers/. Source: https://github.com/AltKorin/NarrativeFramework-OSS. This is an early preview; GM feedback is welcome.”

Controlled campaign labels may use `?utm_source=community&utm_medium=post&utm_campaign=campaign_wiki`. Supported source values: github/community/social; medium: post/profile/link; campaign: public_preview/campaign_wiki/downtime. Other URL parameters are not manually forwarded. Consent refusal means no measurement, so reports do not represent every visitor. Use Search Console for organic queries; our manual context omits referrers.

## Analytics setup and data boundary

Default `public/analytics-config.json` has null IDs and no allowed hosts. Forks/self-hosted sites send nothing to the project's account. Set your own `landingMeasurementId`, `appMeasurementId` and exact `allowedHosts`. Keep `appManualOnlyConfirmed` false until **enhanced measurement is disabled in the application's GA4 stream**. The code cannot change Google account settings. The deployed app stream is prepared but intentionally inactive until that operator step; public landing/articles use the separate public stream.

An enabled configuration still loads Google only after explicit consent (basic consent mode). Equal accept/decline choices and a persistent settings button support withdrawal. The localStorage preference expires after 180 days, separately per host. Withdrawal disables tracking, clears accessible _ga cookies and reloads to remove installed listeners. Browser storage failure does not break the app.

Manual page_view avoids the automatic config pageview. Public CTA events: open_workshop/sign_up_click/github_click/support_click; these indicate clicks, not completed registrations/payments. App context uses generic area names and URLs without query strings/fragments. No username, campaign title/content, invitation token or report is attached. Advertising signals/personalization are disabled. Browser tests intercept Google and inspect queued commands, without synthetic production hits; receiving data in a specific GA property is not verified by those tests.

CSP allows required Google tag/measurement hosts without unsafe-eval/inline scripts. Updated privacy/cookie notices explain actual behavior. Preserve `.env`, production `site-config.json` and `analytics-config.json` during release extraction; the latter is fetched with cache:no-store, independently of React builds. Measurement IDs are public; never put API/Measurement Protocol secrets in the config.

References: [basic consent mode](https://developers.google.com/tag-platform/security/guides/consent?consentmode=basic), [manual pageviews](https://developers.google.com/analytics/devguides/collection/ga4/views), [CSP](https://developers.google.com/tag-platform/security/guides/csp), [Article structured data](https://developers.google.com/search/docs/appearance/structured-data/article).


Docker copies configuration into the image: after changing the file, rebuild `web` (`docker compose up --build --no-deps -d web`).
