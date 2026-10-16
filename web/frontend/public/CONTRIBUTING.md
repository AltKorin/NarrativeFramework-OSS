# Contributing to Narrative Framework

[Instrukcja po polsku](web/docs/pl/contributing.md)

The repository contains the original Unity assets and an independently hosted RPG web workshop. Keep changes scoped: a web change should not rewrite Unity asset GUIDs or imply Unity import/export compatibility.

## Find the right code

| Path | Responsibility |
| --- | --- |
| `Assets/NarrativeFrameworkForUnity/` | Original Unity runtime/editor and documentation |
| `web/backend/stories/domain.py` | Scenario schema, save/version rules, structural checks, author export |
| `web/backend/stories/play.py` | Assignment snapshots, player access, memory/turns, receipts, approvals, report |
| `web/backend/stories/narrative_engine.py`, `campaign_runtime.py` | Deterministic Unity core port, GM-only campaign state, versioned preview/commit |
| `web/backend/stories/models.py` and `migrations/` | Database schema and additive migrations |
| `web/backend/stories/views.py`, `play_views.py` | Session-authenticated HTTP adapters |
| `web/backend/stories/mcp_server.py` | Separate author/player MCP catalogs and shared admission gate |
| `web/frontend/src/` | React editor, individual-play panel and PL/EN dictionaries |
| `web/tests/` | Real protocol and browser workflows |
| `web/docs/`, `web/examples/` | Architecture, bilingual guides, complete example |

## Development and checks

Follow [web setup](web/README.md). Use the checked-in Python lockfile and npm lockfile. Development-only browser/formatter tools are not runtime dependencies. Add migrations for model changes; never edit a previously applied migration to fit new code.

For domain changes run `python manage.py test stories` using the project environment. PostgreSQL-specific concurrency tests are skipped on SQLite and must pass on the Compose PostgreSQL instance before claiming concurrency safety. For MCP/auth changes run both real integration scripts. For frontend changes build with `npm run build`, and exercise relevant browser workflows in `web/tests/browser.py`, `languages.py` and `play_browser.py` using Playwright. Integration/browser scripts create and remove temporary accounts; use a development/test instance.

## Invariants to preserve

- Author scenarios and individual play sessions are separate aggregates; assignment snapshots do not silently follow source edits.
- A player-operated MCP client can inspect its tool outputs. Keep original `secret`, extra private fields, unpublished materials and `gm_notes` out of player context/search/journal/export. Use positive field allowlists, not model instructions as access control.
- Player-scoped tokens never authenticate to author MCP or REST. Store only scoped-token digests, and do not log or commit plaintext tokens.
- A turn commits working memory, event, consequences, version and receipt together. Same operation UUID + same normalized body replays; changed content conflicts. Concurrency needs real PostgreSQL tests.
- Only the server chooses consequence status. Only an activity's GM reviews its pending effects. Narrative text is not mechanically validated campaign canon; do not advertise semantic enforcement or automatic campaign merging.
- Runtime preview and commit must execute the same engine; preview must not persist state, history, audit or campaign version. Keep actor references campaign-local and live runtime out of player publications/exports. Preserve the documented supported Unity subset.
- Keep public-facing labels and new user workflows in both English and Polish. IDs, enum values and schemas remain stable across locales. Changing UI language must not translate/overwrite authored text.

## Explain behavior for reviewers

Describe the user-visible trigger and resulting behavior, affected boundary and checks actually run. Update the bilingual guides for user behavior, the MCP contract for tools/arguments/errors, and architecture decisions when storage/authority/retry semantics change. Mark deferred behavior explicitly. Do not claim load capacity from functional tests.

Do not commit `.env`, database files, generated local state, real campaign content, credentials, build output or `node_modules`. Sample adventures should be fictional and distributable. The root MIT license applies, with separate [brand permissions](BRANDING.md).

Adversarial regression procedures: [English](web/docs/en/adversarial-testing.md) · [Polski](web/docs/pl/adversarial-testing.md). Run dependency-stop tests only against the dedicated disposable Compose project described there.

Campaign membership/publication policy is centralized in `campaign_access.py` and `wiki_sharing.py`. Keep player JSON as an explicit projection of published snapshots; never serialize a GM view and filter it in React. Lock the campaign before role checks/mutations and cover revocation races on PostgreSQL. Run `web/tests/sharing_browser.py` and the wiki MCP integration after access changes.

Campaign wiki architecture, limits and checks: [English](web/docs/en/campaign-wiki.md) · [Polski](web/docs/pl/campaign-wiki.md).
