# Narrative Framework 0.1 — decisions

## Product boundary
A self-hosted RPG writing workshop: scene/NPC/location/clue graph, GM secrets, structural checks, Markdown/JSON export, browser-local anonymous demo and authenticated private scenarios. No paid AI calls, multiplayer live editing, registration, public scenario sharing or background worker in this release. Account provisioning uses Django management commands. Public demo data is intentionally public, including sample GM secrets. User scenarios are private.

## ADR 001: Django domain, two entry points
Django + DRF + PostgreSQL. React/TypeScript + React Flow. Official Python MCP SDK uses Streamable HTTP in a separate ASGI process. HTTP and MCP call the same domain validation/save functions. MCP does not call the public REST API. Sync transactions remain sync; async transport does not make database operations cheaper. PostgreSQL limits statement and lock duration.

## ADR 002: bounded aggregate and optimistic concurrency
A scenario is a versioned JSON aggregate, maximum 250 nodes, 1000 edges and 256 KB. Saves send the whole aggregate in v0.1 (no per-keystroke server autosave). One conditional database UPDATE matches owner + ID + expected version, preventing lost updates across API/MCP processes. Stale writes fail with 409 without side effects. Retrying a successful save with its old version also returns 409. Creation accepts a client UUID to prevent duplicate scenarios on retry. Per-owner cap: 50 scenarios; enforced under an owner-row lock across concurrent creation. Each successful save increments the version; revision history is not implemented.

This trades fine-grained merge for small, atomic batches. Patch operations and revisions can follow after measurements. The UI preserves unsaved work on conflict and offers export; users must reload/reconcile manually. Browser demo is localStorage and survives reload, not a private account or server backup.

## ADR 003: MCP admission and isolation
Bearer personal token maps to one active Django user; no anonymous MCP. Tokens are generated/rotated explicitly in the authenticated UI, stored in Django's token table and treated as secrets. REST accepts session authentication only; MCP accepts Bearer auth. Tokens cannot bypass MCP admission via REST. Default per-token admission 60 requests/minute and 2 concurrent requests; global MCP cap 8, shared via atomic Redis Lua. Expiring leases recover from worker death. Redis admission failure returns 503 (fail closed). Request bodies are bounded, with a receive timeout; database work has server-side timeouts. Tools return up to one bounded scenario. MCP does not offer arbitrary SQL, filesystem access or free-form code execution.

MCP has separate processes and CPU/memory ceilings; database remains shared, so isolation is partial. API throttles also use shared Redis. Database calls in each MCP worker use Django’s thread-sensitive execution and connection cleanup; database execution is therefore serialized per worker. The admission cap limits transport requests, not a promise of eight simultaneous SQL statements. PostgreSQL timeouts bound SQL; cancelling an async caller cannot forcibly stop a running Python thread. Limits are starting settings, not measured capacity or DDoS protection. Invalid-token traffic has a per-source admission limit before database lookup. External agents must support configured Bearer headers: OAuth discovery/client registration is not provided.

## ADR 004: new schema, explicit Unity boundary
`narrative-web/1` is independent of the inconsistent Unity JSON formats. No claim of Unity round-trip compatibility. Existing C# assets are retained; concepts and graph checks informed the Python implementation, not executable reuse of Unity assemblies. A future adapter must test explicit field mappings and loss reports.

## ADR 005: deployment
Docker Compose services: migration job, Django API, MCP, PostgreSQL, Redis and Caddy/static frontend. Only Caddy publishes ports. Database volume persists. Caddy supports direct HTTPS using a public hostname; development uses HTTP explicitly. No server deployment is attempted without host access. Backups must be taken and restored separately. Docker inside LXC requires suitable host/container configuration; this repo does not alter Proxmox security settings.

## Validation semantics
Scene reachability follows scene-to-scene `leads_to` links only. Clue-source hints count distinct incoming `reveals` sources. Three sources are a writing heuristic, not proof of independent discoverability. Endpoints and structural bounds are checked on save. No plot-quality, satisfiability or RPG-rules guarantees.

## Acceptance and performance
Exercise browser demo, login, private save, export/import, API ownership, stale writes and MCP with two users. Test Redis outage, oversize body and concurrent admission. PostgreSQL and real Streamable HTTP tests supplement SQLite unit tests. Measure human edits while MCP is saturated. No concurrency capacity claim before load testing on target hardware.

## Glossary
- Scenario: versioned adventure document owned by one account.
- Scene: playable situation; progression uses leads_to.
- Clue: information discoverable from linked sources.
- NPC: actor with author-defined motivation/content.
- GM secret: author-facing text; included in GM exports and owned MCP reads.
- Version: optimistic concurrency counter, not a revision archive.
- Admission lease: expiring Redis entry reserving MCP execution capacity.

## ADR 006: review findings and resolved boundaries
A bounded read-only Claude review used the installed grill-with-docs skill. It identified malformed-field crashes, unsafe default secrets, token scope and stale database connections. Validation now type-checks references and coordinates before membership checks, production startup requires a secret, REST is session-only, login has a dedicated shared-cache throttle, and MCP calls clean up database connections. No inherited Claude home-directory style/audit requirements were adopted as repository requirements. No historical revision/audit product is claimed for v0.1.

## ADR 007: individual play extends authoring (0.2)

The editor and author `/mcp` are retained. A PlaySession copies a selected public subset of a private owned Scenario, its title/version and assignment policy. It belongs to one GM and one existing player. Mode distinguishes solo, downtime and absence as author intent, not separate mechanics. The assignment cannot be edited/reassigned/reopened in this version. Deleting a source scenario sets its FK null; the snapshot persists.

The player's `/play/mcp` is a separate FastMCP catalog. A random 160-bit token is bound to one assignment; only its SHA-256 digest and 14-day expiration are stored. It is never accepted as an author or REST credential. Player and GM can rotate/revoke it. Authorization is checked at admission and again at tool execution; mutation/rotation lock the same session row. In-flight responses are not recalled. Both transport managers run under the parent ASGI lifespan; mounted child applications do not start their lifespans automatically.

## ADR 008: publish only player-inspectable material

No secret channel is implied by a player-operated model. Positive allowlists copy node `id/kind/title/text` and links with both endpoints published; `secret`, arbitrary extra fields, unpublished nodes and GM-private notes are excluded. Instructions/brief/boundaries/return condition are player-visible. This prevents field leakage, not semantic spoilers pasted into ordinary public text. Dynamic unlocking and trusted server-side model execution are deferred.

## ADR 009: Markdown memory, atomic PostgreSQL persistence

Working memory is a TextField containing Markdown, exported as `.md`, rather than an independently edited volume file. A transaction commits it with the current scene, PlayEvent, consequences, version and operation receipt. This avoids a database/filesystem dual-write problem and keeps backups in the existing database procedure. Journal entries retain actions/narration; old complete memory versions are not archived. Context is a read snapshot; later mutations may make its version stale.

Play mutations use row locking and an expected version. A (session, operation UUID) unique constraint plus a normalized-payload fingerprint makes identical retries return their original receipt. Changed content with an old operation UUID is rejected; a new UUID at a stale version also conflicts. Receipts remain the original result even after later reviews/completion; clients reload context for current state. Creation uses a client UUID and fingerprint; same-ID changed-body retries conflict. Read access and token rotation do not increment the narrative version.

## ADR 010: approval ledger, not semantic enforcement

Agent-authored narration/memory/recap are unverified play records. The agent submits consequence category+description only. Default status is pending; pre-approved categories or human GM decisions make entries approved. GM notes remain private; review notes are deliberately visible to the player. Completion freezes new turns but allows pending decisions and replay of previously acknowledged operations. No main campaign, inventory or character-sheet data is mutated automatically.

Category approval is coarse: a model can misclassify/omit a consequence or narrate out-of-bounds events. No claim of automatic canonical consistency, rules checking, randomness or model obedience is made. Keep all categories pending for conservative use. Constraints bound size (100 turns, 10 effects/turn, 16K current memory; paginated journal); they are not a capacity benchmark. List UI returns up to 200 most recent accessible assignments.

## Adversarial hardening

See the [reproduction report](en/adversarial-testing.md) for input bounds, creation fingerprints and lost-response retries, coherent play context reads, token rotation serialization, MCP deadlines and dependency recovery. These checks exercise application invariants; they are not a capacity benchmark or a complete penetration test.

## Campaign wiki

Campaign/WikiPage/WikiRelation are separate from the bounded scenario aggregate. A campaign row lock serializes mutations; page optimistic versions prevent lost edits. Immutable creation fingerprints reject changed-body replays and unique-ID races become controlled conflicts. Wiki list/article/graph share records. Copying to a scenario requires both source and destination versions and records provenance without creating a live reference. Player publication still uses the existing allowlist. Wiki MCP tools exist only in the owner author catalog. See the [full contract](en/campaign-wiki.md).

## Group wiki authorization and publication (alpha.2)

`campaign_access.py` is the shared authority boundary. `Campaign.owner` remains the single owner; `CampaignMember` stores co-GM/player roles with unique campaign/user membership. Mutations and coherent reads lock the campaign before checking current membership. Invitations are expiring single-use digest records; a fragment-based link requires explicit authenticated acceptance. Revocation/role changes use the same lock as writes/publication. Deleting a membership cascades its grants; role changes also clear grants.

Page/relationship audiences and published JSON snapshots are distinct from drafts. Player serializers positively select publication fields and filter both edge endpoints and edge audience. Search uses published JSON fields for readers; GM views use drafts. GM preview reuses the reader projection rather than client filtering. API responses are private/no-store. Audit contains actor/action/resource/version, no secret text or credentials. Full GM export is now `narrative-wiki/2`; reader export is `narrative-wiki-player/1`.

Author MCP explicitly requires a GM role for every wiki call; browser player membership does not grant author access. Existing play MCP/snapshots remain separate. Campaign reader MCP is a later stage. Migration 0005 is additive and makes no existing record public. Rollback of code to the owner-only release hides shared data; do not reverse migration 0005 over live data to roll back application behavior.

Migration 0006 retains database defaults for new non-null wiki fields, so the owner-only code can still insert private pages/relations during a code rollback. Keep additive tables/data in place; reversing migrations discards sharing data.

## Campaign runtime: shared deterministic engine for preview and commit

`narrative_engine.py` ports the documented deterministic Unity subset into a pure
copy-on-command state transition. `campaign_runtime.py` owns authorization,
campaign-local actor references, expected campaign versions and transactional
persistence. Migration 0007 adds `Campaign.runtime`; existing wiki text stays
unchanged. Actor identities are wiki page UUIDs for NPCs, PCs or organizations.

Both `POST runtime/` and `POST runtime/preview/` acquire the campaign lock and
require owner/co-GM access. Preview returns the computed state without persisting
state, version or audit; commit stores state and audit atomically. Confirmation
must still match the version reviewed in the preview. PostgreSQL serializes
concurrent mutations; a stale command returns 409. Resolved events cannot apply
again. The browser compares authoritative before/after states for value and
availability changes instead of implementing a second rules engine.

The runtime is absent from player publication/export/preview projections and is
not exposed through player MCP. Individual-play approval records remain separate;
no automatic campaign mutation or Unity scenario import is implied. Full GM
exports include runtime state. See [source mapping, bounds and API](narrative-runtime.md).
