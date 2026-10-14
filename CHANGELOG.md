# Changelog

## v0.3.0-alpha.2 — 2026-10-14

- Shared campaign wiki: owner/co-GM/player roles, expiring single-use invitations, membership management and leaving/revocation.
- Separate saved drafts and explicit player publications, with all-player or selected-member audiences for pages and relationships.
- Server-filtered articles, counts, search, graph, backlinks and exports; GM preview as a specific player, plus focus/periodic access rechecks.
- Co-GM author MCP access with per-call authorization; personal player tokens cannot read GM drafts. Existing player-play MCP remains isolated.
- Additive migration 0005 preserves old pages/relations as private and unpublished; 0006 retains database defaults for legacy-code inserts during rollback. GM export is now `narrative-wiki/2`; reader export uses `narrative-wiki-player/1`.
- Invitation/publication/role audit, private API cache policy, bilingual guides and group browser/race regressions.
- FAQ easter egg without attribution.

Separate campaign-reader MCP, player editing, ownership transfer and Kanka import remain planned.

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
