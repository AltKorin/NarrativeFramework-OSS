# Narrative Framework 0.3.0-alpha.2

[Polski](README.pl.md) · [Individual adventures](docs/en/individual-play.md) · [MCP play contract](docs/en/mcp-play.md) · [Contributing](../CONTRIBUTING.md)

A self-hosted RPG adventure workshop, alongside the original Unity assets. Django/DRF + PostgreSQL, React/TypeScript + React Flow, and a separate authenticated MCP service. MIT; [brand permissions](../BRANDING.md) are documented separately.

## Start with Docker Compose

Requires Docker Engine and the Compose plugin. From `web/`:

```sh
cp .env.example .env
python3 -c 'import secrets; print(secrets.token_urlsafe(48))'
# Set DJANGO_SECRET_KEY and a separate POSTGRES_PASSWORD in .env.
docker compose up --build -d
docker compose exec api python manage.py createsuperuser
```

Open **http://localhost:8080** for the public website and **http://localhost:8080/workshop** for the editor. The anonymous demo stores edits in this browser. Log in and choose **Zapisz na koncie** to create a private server copy. Save subsequent changes explicitly. Public test accounts can be created from the workshop; administrators can disable this with `ALLOW_REGISTRATION=0`. Registration requires CSRF, a valid username and a strong password of at least 12 characters; it is limited to five attempts per hour per IP. No email or automatic password reset is implemented. Management commands can still provision accounts.

The initial Compose startup migrates PostgreSQL and seeds the demo. Only the web service publishes ports. PostgreSQL and Caddy data use persistent volumes. `docker compose down` preserves volumes; do not use `down -v` unless deleting all data is intended.

## Public server / LXC / Proxmox

The project does not configure your host. Use an LXC that already supports Docker, or a dedicated VM according to your host policy. Set these values in `.env` for direct public HTTPS:

```dotenv
SITE_ADDRESS=narrativeframework.com
HTTP_PORT=80
HTTPS_PORT=443
ALLOWED_HOSTS=narrativeframework.com,localhost,127.0.0.1,api,mcp
CSRF_TRUSTED_ORIGINS=https://narrativeframework.com
SECURE_COOKIES=1
MCP_ALLOWED_HOSTS=narrativeframework.com,narrativeframework.com:443,localhost:*,127.0.0.1:*,mcp:*
MCP_ALLOWED_ORIGINS=https://narrativeframework.com
```

Point DNS at your server and make ports 80/443 reachable. Caddy obtains HTTPS certificates. If an existing reverse proxy terminates TLS, configure its route, forwarded headers, Caddy address and trusted origins consistently. Do not expose the API/MCP/database container ports directly. `.env` contains secrets and must not be committed. The reference deployment uses Cloudflare Tunnel; see [website deployment](docs/en/website.md) for proxy trust and HTTPS settings.

## MCP

Log in, open the account panel and explicitly generate a personal token. Generating another revokes the old token. Configure an MCP client supporting **Streamable HTTP with custom authorization headers**:

```json
{
  "url": "https://narrativeframework.com/mcp",
  "headers": {"Authorization": "Bearer YOUR_PERSONAL_TOKEN"}
}
```

This illustrates author transport settings, not a universal client configuration file. Player sessions use separate credentials and `/play/mcp`; never give an author token to a player. Local URL: `http://localhost:8080/mcp`. Adventure tools: `list_scenarios`, `read_scenario`, `save_scenario`, `validate_scenario`. Campaign wiki tools: `list_campaigns`, `search_wiki`, `read_wiki_page`, `save_wiki_page`. Create/import a private adventure in the web UI first. Reads include GM secrets. Tools can only access the token owner's scenarios and campaigns. No OAuth discovery or dynamic registration in v0.1.

Limits default to 60 requests/minute per token, 2 concurrent requests/token, 8 globally. Redis admission is shared between MCP workers and fails closed on outage. 429 means back off; 503 means try later. Retry a version conflict only after re-reading and reconciling changes. Token rotation revokes future requests, not work already admitted. MCP uses a separate CPU/memory budget, but shares PostgreSQL. This is not a guarantee of web latency during arbitrary hostile traffic.

## Individual adventures (new in 0.2)

Keep writing in the existing editor. Use **Between sessions → Assign this adventure** to assign selected public materials to an existing player's character for downtime, absence or solo play. The player connects an external agent to **`/play/mcp`** using a separate session-scoped token. Turns persist Markdown working memory and a journal; lasting consequences default to human GM review. The main campaign is never changed automatically.

See the [complete guide](docs/en/individual-play.md), [MCP contract and retry examples](docs/en/mcp-play.md), and [sample adventure walkthrough](examples/README.en.md). Memory is stored as Markdown text in PostgreSQL and downloadable as `.md`, so existing database backups cover it. It is not a separately mounted editable file. Migration 0002 adds the session/journal/approval tables; Compose runs it during startup. Existing scenarios and author MCP remain available.

## Languages

Use the **PL / EN** switch in the header. The first visit follows the browser language (Polish for `pl`, otherwise English); a manual choice is remembered. Interface labels, structural hints and Markdown headings follow this setting. The sample adventure is available in both languages, with separate browser-local drafts. Switching languages saves the current demo draft first. Existing Polish demo drafts keep their original storage key. Private authored adventures are never automatically translated or replaced by switching the interface language.

API demo, validation and Markdown exports accept `?lang=pl` or `?lang=en` (also `Accept-Language`). MCP `validate_scenario` accepts `language`, defaulting to English. JSON schema identifiers and graph IDs remain language-independent.

## Data and compatibility

Node kinds: scene, NPC, location and clue. Edges: `leads_to`, `reveals`, `involves`, `located_at`. JSON format is **`narrative-web/1`**, distinct from the Unity importer. Export/import round trips the web format; Unity adaptation remains future work. Markdown is a GM document and includes secrets. Public demo secrets are sample content, not access-controlled player handouts.

Documents are capped at 250 nodes, 1000 links and 256 KB. Saves are explicit whole-document transactions with an expected version; stale writes fail without overwriting. Export unsaved work before reloading after conflict. Demo edits are localStorage; JSON/Markdown export works without an account. Server scenario cap: 50/user, enforced transactionally across concurrent creation requests.

## Checks and local development

```sh
export DEBUG=1
uv venv .venv
uv pip install --python .venv/bin/python -r backend/requirements.lock
.venv/bin/python backend/manage.py migrate
.venv/bin/python backend/manage.py seed_demo
.venv/bin/python backend/manage.py createsuperuser
.venv/bin/python backend/manage.py test stories
.venv/bin/python backend/manage.py runserver
```

In another terminal: `cd web/frontend && npm ci && npm run dev`. Vite proxies `/api` to local Django. This mode uses SQLite unless PostgreSQL environment variables are supplied; PostgreSQL is the deployment database. MCP requires Redis and a separate process: from `backend/`, `../.venv/bin/uvicorn stories.mcp_server:application --port 8001`.

Container tests: `docker compose exec api python manage.py test stories`. Build frontend: `cd frontend && npm run build`. See [architecture decisions](docs/architecture.md) and [verification](docs/verification.md).

## Backup and restore

From `web/`, with the same `.env` and Compose project:

```sh
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > narrative.dump
```

Store the dump and `.env` securely outside this server. Restore into a compatible, empty target database with the application stopped:

```sh
docker compose stop api mcp
docker compose exec -T db sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --exit-on-error' < narrative.dump
docker compose start api mcp
```

Restore into a new instance first; restoring over existing tables needs an explicit replacement plan. A backup is only verified after a successful restore.

## Not included yet

AI generation, background job processing, revision history, realtime collaborative editing, dynamic secret unlocking and Unity-format adapters. Structural checks are writing hints, not guarantees that a plot is solvable. Capacity must be measured on the target host; no user-count guarantee is made.

Adversarial regression tests, reproduced failures and isolation instructions: [English](docs/en/adversarial-testing.md) · [Polski](docs/pl/adversarial-testing.md). JSON input is bounded to 32 nesting levels and 50,000 values. Migration 0003 stores scenario creation fingerprints for safe retries; Compose applies it on startup.

Website configuration and deployment handoff: [English](docs/en/website.md) · [Polski](docs/pl/website.md).

## Campaign wiki

Open `/campaigns/` or choose **Campaign wiki** in the workshop. Campaigns support owner/co-GM/player memberships, single-use invitations, deliberate publication to all or selected players, Markdown articles, search, relationships/backlinks and a filtered graph. Reuse articles across adventures through version-checked snapshots; GM notes remain separate. Migrations 0004 and 0005 are additive; existing material stays private. [Guide and contracts](docs/en/campaign-wiki.md).


- [First campaign / Pierwsza kampania — UX](docs/en/first-campaign-ux.md)
- [Blog, SEO, Analytics](docs/en/publishing-and-analytics.md)
