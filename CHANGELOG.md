# Changelog

## v0.3.0-alpha.1 — 2026-10-14

First public open-source preview of the independently hosted Narrative Framework RPG workshop.

- Docker Compose deployment: Django/DRF, PostgreSQL, Redis, React/TypeScript, React Flow, Caddy and separate MCP workers.
- Visual adventure editing, private accounts, browser-local demo, versioned saves and JSON/Markdown exports.
- Owner-private campaign wiki with safe Markdown, categories, search, relationships/backlinks, graph and explicit snapshots into adventures.
- Individual downtime/solo/absent-character assignments, scoped player MCP, persistent memory/journal and GM-reviewed consequences.
- English and Polish interface/documentation, public landing page and donation link.
- Ownership, version/retry and hostile-input regressions: 73 PostgreSQL tests and real MCP/browser workflows verified before release. CI reproduces core checks; a green public CI run is reported separately.
- Historical Unity toolkit retained as legacy source; no Unity validation or web-format compatibility claimed.

Known boundaries: campaign membership/sharing and reader MCP are design documents, not implemented functionality. No Kanka importer, built-in AI chat/model, automatic campaign canon merging, realtime collaborative editing or automated email/password recovery. No supported-user count or completed backup-restore test is claimed.

Polski: pierwsza publiczna wersja testowa. Wiki jest prywatna dla właściciela; współdzielenie kampanii opisano w planie, ale nie działa jeszcze w aplikacji.
