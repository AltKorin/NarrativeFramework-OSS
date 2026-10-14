# Verification — 2026-10-14

## Verified locally

- Docker Compose built and ran PostgreSQL, Redis, migration/seed job, Django API, MCP and Caddy/frontend. Database and API health checks passed.
- Production frontend build passed TypeScript checks and Vite bundling.
- Django tests passed on SQLite and PostgreSQL: owner isolation, immutable public demo, stale-write rejection, retry-safe creation, structural validation, malformed fields, export, login CSRF and token rotation/scope.
- Real Streamable HTTP via official MCP client: initialize, tool discovery, owned reads/saves, stale writes flagged as tool errors and cross-owner access rejected.
- MCP HTTP admission: missing/invalid credential 401, oversized request 413, rate limit 429; atomic Lua admission admitted only two out of twelve concurrent reservations for a two-slot limit.
- With Redis stopped, MCP returned 503. Redis was restarted afterwards.
- Existing MCP workers worked after a PostgreSQL restart.
- Chromium: browser-local demo survives reload; login, private save/version increment, structural checks, Markdown download; desktop and mobile render without horizontal overflow or uncaught page errors.

These are functional checks on this development machine, not capacity benchmarks. No public hostname/TLS deployment, target Proxmox/LXC deployment, backup restoration or sustained mixed-user/MCP load test has been performed.

## Repeat

From the repository root, with `web/.env` configured:

```sh
docker compose -f web/compose.yaml up --build -d
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/integration.py
```

The integration script creates uniquely named temporary users and removes them on completion. Run against a development/test instance. It exercises real MCP and Redis admission rather than mocks.

Browser check (optional development dependency, not part of runtime image):

```sh
uv pip install --python web/.venv/bin/python playwright
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python -m playwright install chromium
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/browser.py
```

The browser script expects http://localhost:8080, creates/removes a temporary account and writes desktop/mobile screenshots under `/tmp/`.

## Next performance acceptance

On the target host, measure web save/read latency, HTTP errors, database locks/connections, CPU/RAM and MCP queue/rejection rates under a realistic human editing workload plus an independently saturated agent workload. Set a latency/error budget before interpreting the results. Do not translate open tabs directly into requests per second or publish a supported-user count without measurements.

## English / Polish interface

After adding language support, 17 tests passed on PostgreSQL. Chromium checks passed for browser-language detection, manual PL/EN switching, persisted preference, independent demo drafts (including switching before debounce), localized hints and Markdown, mobile layout, and switching a private adventure's interface without changing its authored content. The MCP integration suite also passed.

Repeat locale-specific browser checks with:

```sh
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/languages.py
```

## Individual adventures — 0.2

Verified on the local Compose stack:

- **34 Django tests passed on PostgreSQL**, including concurrent scenario saves and concurrent retries of one play turn (one journal event, one consequence set). SQLite passes with PostgreSQL-specific concurrency tests skipped.
- Both MCP catalogs worked with the real official SDK: existing author tools, player-only discovery, endpoint/token separation, publication allowlist, working memory/journal/search, stale-write errors, replay without duplicate effects, completion, fresh-client resume and revocation.
- The play integration script also passed through Caddy using the same public Host form as the local browser (`localhost:8080`).
- Human REST checks rejected player/outsider review, permitted GM review after completion, replayed an identical decision, and filtered private GM notes from the player report.
- Chromium completed the two-account flow: GM saves/assigns an adventure, player logs in and generates a scoped token, a real MCP client commits/finishes play, GM approves the effect, both download correctly filtered reports. PL/EN and mobile layout passed with no page errors. This is a deterministic client workflow, not a claim that a particular model obeyed the brief.
- Both sample JSON files passed the scenario validator. `seed_absence_example` created the two language variants for a temporary owner and preserved data on repeated execution.
- TypeScript/Vite production build passed; database migration state has no missing model changes. Local Markdown links were checked.

Repeat on a **development/test instance**, from repository root:

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/integration.py
docker compose -f web/compose.yaml exec -T api python < web/tests/play_integration.py
docker compose -f web/compose.yaml exec -T -e PLAY_TEST_BASE=http://web -e PLAY_TEST_HOST=localhost:8080 api python < web/tests/play_integration.py
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/play_browser.py
```

The browser/protocol scripts create and remove uniquely named temporary accounts. The browser script expects the local public URL and uses an independent thread for the async MCP client while Playwright's synchronous driver is active. Screenshots under `/tmp/` use fictional data and hide tokens. Run checks only after Compose startup finishes; rebuilding containers during a browser workflow interrupts that workflow.

Still unverified: live-model narrative quality/obedience, a real player's campaign integration, public DNS/TLS deployment, target Proxmox/LXC deployment, backup restore and sustained mixed web/MCP load. No supported-user count is claimed.

## Adversarial regression pass — 2026-10-14

All 48 Django tests passed on PostgreSQL, including concurrent token rotation and context-read interleavings. Six browser fault scenarios passed. Real MCP checks covered hostile player calls and slow incomplete request bodies (408 with admission cleanup). Isolated Redis and PostgreSQL shutdowns returned controlled 503 responses, preserved rejected-write semantics and recovered existing workers after restart. Baseline results, fixes, limitations and commands are recorded in the [English report](en/adversarial-testing.md) and [Polish report](pl/adversarial-testing.md). These results do not establish deployment capacity.

The fixed main local Compose stack also passed the existing editor, EN/PL and individual-play browser workflows after migration 0003, including real player MCP calls and GM approval. The isolated fault-test containers were stopped afterwards; their volumes were retained.

## Public website and test-account registration

The finished local Docker stack passed all 55 Django tests on PostgreSQL, including registration CSRF, privilege injection, password validation, duplicate usernames, disabled signup and per-IP admission. The new browser flow passed prerendered PL/EN content, donation link, real images, example switching, mobile navigation, account creation, private save, session persistence and logout. The existing editor, language and individual-play browser suites also passed after the editor moved to `/workshop/`. TypeScript/Vite and the static prerender build passed. Public DNS/TLS and the target Proxmox host are still awaiting deployment access.

## Dedicated LXC verification

The target Debian 13 LXC passed all 55 PostgreSQL tests, both real MCP integrations, and the website/signup and individual-play browser workflows through its LAN HTTP endpoint. This exposed a browser compatibility issue hidden by localhost: `crypto.randomUUID()` is restricted to secure contexts. A shared UUIDv4 helper now falls back to `crypto.getRandomValues()` for LAN HTTP, and scenario creation, assignment and consequence review passed afterwards. The Caddy proxy test also passed trusted-local-proxy client-IP preservation, strict XFF parsing and spoof rejection for an untrusted container peer. Public Cloudflare DNS/account authorization is still pending; these are LAN deployment results.

## Public HTTPS deployment — 2026-10-14

The dedicated LXC deployment was published through a named Cloudflare Tunnel running as a systemd service. All three hostnames (apex, www, app) returned the correct static PL/EN HTML. A routing regression initially served English fallback HTML for /pl/ while client-side translation hid the issue; explicit locale index resolution and stronger raw-HTML assertions fixed it. HTTP redirects to HTTPS, anonymous MCP calls return 401, and public CSRF cookies carry Secure. The public browser workflow passed registration/private save/session persistence/logout; the real public player MCP workflow passed assignment, play, GM approval and filtered reports. Newly propagated DNS was pinned only within the test processes to a verified Cloudflare address, with normal hostname TLS validation; no system DNS changes were made. Original DNS records were backed up before apex/www cutover.

## Shared campaign wiki — 2026-10-14

All **73 Django tests passed on PostgreSQL**, including ownership, malformed inputs, immutable creation receipts, concurrent edits/UUID collisions, persistent campaign settings, deletion and version-checked article copies. The real author MCP wiki integration passed discovery, search, private read, replacement, conflict and foreign-owner rejection. Existing author and player MCP suites passed; the player catalog remains separate and cannot call wiki tools.

Chromium passed the full wiki workflow: safe Markdown, categories, body search, bidirectional relations and map, PL/EN draft preservation, stale-write draft retention, private JSON export, source-linked adventure copies including GM notes, foreign-account isolation and mobile layout. Existing editor, locale, landing/signup and individual-play browser checks also passed. This caught and fixed accessible textarea labels and mobile header overflow introduced by the wiki link. TypeScript/Vite production build and focused Ruff checks passed.

The wiki is owner-private in this release. It has no Kanka importer, campaign membership, live copy synchronization or wiki access for players. These are functional checks, not a load/capacity benchmark. See [English](en/campaign-wiki.md) and [Polish](pl/campaign-wiki.md) for contracts, limits and reproduction commands.

The deployed LXC passed all 73 tests on PostgreSQL, reported no missing migrations, and also passed the real wiki MCP check and the complete public HTTPS wiki browser workflow through Cloudflare, using disposable accounts. A private PostgreSQL dump and previous-source archive were taken before additive migration 0004. Public tests used process-local DNS pinning with hostname TLS verification while local resolver propagation remained stale.

## Shared wiki groups — alpha.2

Local PostgreSQL coverage now includes ownership/co-GM/player matrices, selected audiences, unpublished draft isolation across title/body/search/version, hidden edges between visible endpoints, filtered exports/preview, role demotion and membership revocation, single-use/expired/revoked invitations, recipient validation, co-GM copies into owned scenarios and race tests for invite consumption, publication/edit conflicts and revoked waiting writers. A raw legacy-style insert test checks database defaults added by migration 0006 for rollback compatibility.

The complete group browser workflow passed invitation/login return, all three roles, actual UI publication to selected players, safe projections, filtered exports/graph/search, GM preview, draft isolation, demotion/revocation, cleared search results after withdrawal, PL/EN and mobile. Existing editor/locale/site/signup/wiki/play browser suites and all three real MCP integrations also passed. Wiki MCP explicitly tested player rejection, co-GM secret access and demotion in an already-connected client. GM export changed to `narrative-wiki/2`; reader exports use `narrative-wiki-player/1`. Separate campaign reader MCP remains unimplemented.

## Public content, analytics and first-campaign UX — 2026-10-14

- Production TypeScript/Vite/static HTML build passed. Eleven public PL/EN pages include three guides per language, canonical/hreflang, article/breadcrumb JSON-LD, sitemap and RSS. HTTP checks verify CSS without JavaScript, private-app noindex and real 404 responses.
- Browser consent tests intercepted Google's SDK (no real analytics hits): no tag before consent, decline persistence, correct separate IDs, disabled-app guard, sanitized manual context, advertising denial, cookie removal/withdrawal and mobile controls passed. Application measurement remains explicitly disabled pending operator confirmation that Enhanced Measurement is off. Delivery to GA reports is not verified.
- New first-campaign browser journey passed in PL/EN: fixed return after login, campaign naming focus, typed entries, title guard, progress after reload, custom directed relationship, saved graph, mobile fit/layout, hide/reopen guidance and node-to-article navigation.
- Existing editor, languages, public site/signup, wiki, group sharing/publication and individual-play browser regressions passed locally.
- First-time human usability study is planned in `pl/first-campaign-ux.md` / `en/first-campaign-ux.md`; no study results or conversion improvement claimed.

## Shareable illustrations and infographics — 2026-10-14

- Both first-campaign PL/EN posters and a new rugged dark-fantasy GM-publication series are available on the landing page. Actual downloads were verified as PNG 1080 × 1350. Language switches select the matching image/download; 390/768/1024/1440 layouts have no horizontal overflow.
- Campaign-wiki guides include the first-campaign infographic; downtime guides use the expedition illustration. All added article images decode successfully with descriptive alt text; explicit dimensions reserve layout space. New RPG-poster previews use WebP, with full PNG downloads.
- Layered Photocraft sources and PSD exports are included in `web/marketing/social`. First-campaign native/PSD reopen checks retained 34 type layers and 24 shapes; editing a reopened type layer succeeded. New PL/EN `.pcraft` reopen checks retained 16 editable text layers and 5 named groups. The illustrative scene remains a separate raster layer; typography is native.
- Final local TypeScript/static build, public-content HTTP checks and blog/site/analytics browser suites passed. Source asset provenance, exact generation prompts and editing/re-export instructions are included. No external social posts were published.
