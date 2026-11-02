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

### Characters and rules
- **Campaign role**: a member's standing in a campaign — owner GM, co-GM or player.
  _Avoid_: role (alone)
- **Ruleset pack**: a data file describing one RPG system's character rules, attached to a single campaign.
  _Avoid_: system, rules file
- **Built-in pack**: a ruleset pack shipped with Narrative Framework from an openly licensed source, which a GM
  selects instead of uploading.
- **Character axis**: a named list of options every character picks from, such as Role, or Class, Background and
  Species; its name comes from the pack.
  _Avoid_: role, origin
- **Starting level**: the level new characters in a campaign are built at, set by the GM.
- **Rules pages**: wiki pages carrying the full rules text of a built-in pack, copied into one campaign, where the GM
  may edit them as house rules.
  _Avoid_: rules library, compendium
- **Advancement**: a GM-granted chance for an approved character to grow under its pack's rules, completed by the
  player and approved by the GM.
  _Avoid_: level-up, XP

### Party
- **Party**: the group of all players in one campaign and their characters; a campaign has exactly one. Shown as
  "Drużyna" in fantasy campaigns and "Ekipa" in cyberpunk ones.
  _Avoid_: team, group, crew (as domain terms)
- **Display name**: how a player appears to the rest of the party in one campaign, chosen by that player; until they
  choose one they appear as a numbered player. Other players never see login names; the GM does.
  _Avoid_: username, nick
- **Specialization**: what a character is good at within the party — its three highest skills, plus one optional
  sentence from the player about their job in the party.
  _Avoid_: class, role (both are character axes)
- **Player material**: something a player writes or adds for their character, kept on the party page apart from the
  GM's wiki; only its author changes it.
  _Avoid_: player page, wiki page
- **Sharing**: who among the players sees a player material — private, selected players or the whole party, chosen
  by the author every time they create one. The GM always sees every player material, private ones included.
  _Avoid_: visibility, audience (those belong to GM wiki publication)
- **Party board**: a shared surface on the party page made of cards, viewed as a list or as a map; every party member
  adds cards, and each card is changed only by its author. On the map anyone moves any card, since placement is
  shared and is not content.
  _Avoid_: whiteboard, notes
- **Card**: one item on the party board — a title, short text and optionally a reference to a published wiki page, a
  player material or an outside web address; anyone who cannot see the referenced material sees the card without it.
- **Board link**: an arrow between two cards on the party board, drawn by any party member in that member's colour;
  only its author changes or removes it.
  _Avoid_: relation (that is the GM wiki's term), edge
- **Former player**: someone who has left the campaign; their player materials, cards and board links stay in the
  party until the GM removes them.

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

## ADR 011: rules pages are copied into each campaign

Every installation is self-hosted and independent: there is no central Narrative Framework service to host a shared
rules library, and an instance must work offline from its own release. A built-in pack therefore ships its rules
text as data inside the release, and selecting the pack copies that text into the campaign wiki as rules pages with
stable identities. The GM may edit them as house rules. Hundreds of pages per campaign are acceptable at the scale of
a self-hosted group. A newer pack version arrives only with an upgraded release and is applied to a campaign only when
its GM chooses to; switching to another pack leaves existing rules pages in place until the GM removes them. The
rejected alternative, one read-only library per instance, saves storage but forbids house rules.

## ADR 012: a character stays an event log after approval

Creation already stores a draft as an append-only event log and computes the sheet from it. Advancement keeps the same
model after approval: a GM-granted advancement, the player's choices and the GM's decision are appended to the same
character, and the published card and runtime actor are recomputed. Undoing the latest approved advancement appends a
reversal rather than rewriting history, so the GM can always answer what happened and when. The rejected alternative,
storing the approved sheet as mutable state, would make undo, review of past choices and later rules corrections lossy.

## ADR 013: the GM sees private player materials

A player material marked "Prywatne" (private) is hidden from other players but not from the GM. The GM reads every
player material so they can weave a character's secrets into the story and remove inappropriate content; the
player-facing guide and privacy notes state this plainly, while the sharing control itself just says "Prywatne". The
rejected alternatives were a label naming the GM ("only me and the GM"), which the owner found to spoil the feeling
of a secret, and truly GM-proof private notes, which would leave the GM blind to material the story depends on.
Changing this later is trust-sensitive: players will have written private material under this rule.
