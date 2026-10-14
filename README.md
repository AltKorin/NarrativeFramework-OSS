# Narrative Framework

[Polski](README.pl.md) · [Try the workshop](https://app.narrativeframework.com/workshop/) · [Website](https://narrativeframework.com) · [Support development](https://www.buymeacoffee.com/StolemCentrumGier)

An open-source RPG adventure workshop: connect scenes, characters, places and clues, keep campaign lore in a wiki, and prepare individual downtime, solo or absent-character adventures for an external MCP agent.

**First public preview: `v0.3.0-alpha.1`.** This is working software under active development. Campaign wiki sharing is planned, not available: currently each wiki is private to its owner. Functional test results are not a server-capacity guarantee.

![The adventure editor with its graph and GM materials](web/frontend/public/images/workshop.png)

## What works

- A visual adventure editor, structural checks and separate GM secrets.
- Markdown campaign articles, categories, search, relationships/backlinks and a map over the same records.
- Explicit, version-checked wiki copies into multiple adventures.
- GM-assigned downtime/solo adventures, scoped player MCP access, persistent memory/journal and human review of lasting consequences.
- Separate author and player MCP catalogs; the server does not include its own AI model or chat client.
- English/Polish interface, account-free browser-local demo, private accounts and JSON/Markdown exports.
- Docker Compose with Django/DRF, PostgreSQL, Redis, React/TypeScript, React Flow and Caddy.

## Run locally

Requires Docker Engine and Docker Compose. From the repository root:

```sh
cp web/.env.example web/.env
```

Generate two separate secrets and put them in `web/.env` as `DJANGO_SECRET_KEY` and `POSTGRES_PASSWORD`:

```sh
python3 -c 'import secrets; print(secrets.token_urlsafe(48))'
```

Then start the stack:

```sh
docker compose -f web/compose.yaml up --build -d
```

Open **http://localhost:8080**. The workshop is at `/workshop/`, the campaign wiki at `/campaigns/`. The example configuration enables signup. PostgreSQL persists in a Docker volume; `docker compose down -v` deletes that data. No AI provider key is needed to run the application or tests. An external agent/client is required for AI-led play.

Before public deployment, follow the [setup and HTTPS instructions](web/README.md), set your own hostnames, secure cookies and trusted proxy addresses, and arrange backups. Do not commit `.env`, tokens or real campaign data. The hosted test instance is separate from a self-hosted installation.

## Guides

| Topic | English | Polski |
| --- | --- | --- |
| Setup and operation | [Guide](web/README.md) | [Instrukcja](web/README.pl.md) |
| Campaign wiki | [Guide](web/docs/en/campaign-wiki.md) | [Instrukcja](web/docs/pl/campaign-wiki.md) |
| Individual adventures | [Guide](web/docs/en/individual-play.md) | [Instrukcja](web/docs/pl/individual-play.md) |
| Player MCP contract | [Contract](web/docs/en/mcp-play.md) | [Kontrakt](web/docs/pl/mcp-play.md) |
| Reproduced failures | [Report](web/docs/en/adversarial-testing.md) | [Raport](web/docs/pl/adversarial-testing.md) |
| Campaign sharing — planned | [Design](web/docs/en/campaign-sharing-plan.md) | [Plan](web/docs/pl/campaign-sharing-plan.md) |

See [verification](web/docs/verification.md), [release notes](CHANGELOG.md), [contributing](CONTRIBUTING.md), [security reporting](SECURITY.md) and [third-party notices](THIRD_PARTY_NOTICES.md).

## Current boundaries

Wiki membership, co-GM roles, player wiki access, Kanka import and campaign reader MCP are not implemented. The existing player endpoint uses explicitly assigned adventure snapshots. The server does not enforce RPG mechanics, guarantee narrative quality, automatically merge consequences into campaign canon, provide email/password-reset automation or support realtime collaborative editing. See the sharing design for the next complete group workflow.

## Where it started

Narrative Framework began as a Unity toolkit for branching narrative simulations. As its game-focused use evolved, this independently hosted RPG workshop became the public open-source direction. The original code remains in [`Assets/NarrativeFrameworkForUnity`](Assets/NarrativeFrameworkForUnity/Documentation/README.md) as a legacy component; it is not required for the web app and was not validated by the web release tests. Its JSON format is separate, with no Unity/web conversion promise. Unity setup: [quick start](Assets/NarrativeFrameworkForUnity/Documentation/QUICK_START.md).

## License and support

Code and documentation use the [MIT License](LICENSE). [Brand-name/logo permissions](BRANDING.md) are documented separately. AI-generated demo illustrations are provided as-is; see [asset and dependency notices](THIRD_PARTY_NOTICES.md).

If the project helps your table, you can support it through [Buy Me a Coffee — Oil to my machine](https://www.buymeacoffee.com/StolemCentrumGier).
