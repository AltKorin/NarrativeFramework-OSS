# Player MCP contract — v0.2

[Polski](../pl/mcp-play.md) · [User guide](individual-play.md)

Endpoint: `POST /play/mcp` using the official MCP Python SDK's stateless Streamable HTTP transport. Initialization and tool discovery use the standard MCP protocol. Supply `Authorization: Bearer <play-token>` on every request. TLS is required for public deployment. No OAuth discovery or client registration is implemented.

A token is bound to exactly one assignment and its assigned player. Calls do not accept a caller-selected session ID, user ID, owner or filesystem path. Author personal tokens are rejected here; play tokens are rejected by `/mcp`. REST endpoints require normal login cookies and CSRF protection for changes.

The backend stores a SHA-256 digest of each random 160-bit play token. The plaintext is returned only when generated. Tokens expire in 14 days; generate a new token to rotate, or revoke it in the web UI. Either the assigned player or the assignment's GM may do this. Inactive player/GM accounts cannot use the token. An admitted request may already be in progress during revocation.

## Tools

| Tool | Arguments | Result |
| --- | --- | --- |
| `get_play_context` | none | Assignment metadata, facilitator contract, public materials, memory, current version, last five journal events, up to twenty recent consequences and recap |
| `read_play_journal` | `offset=0`, `limit=10` (1–20) | `total`, `offset`, ordered `entries`; GM-private notes and review events omitted |
| `search_play_memory` | `query`: non-empty string, max 200 chars | Current version, memory excerpt and up to ten matching play-journal excerpts |
| `commit_play_turn` | See below | Committed version, consequence IDs/statuses, replay flag |
| `finish_play_session` | `operation_id`, `expected_version`, `summary` | Completed status, committed version, number of still-pending consequences, replay flag |

The server's initialize instructions and `agent_contract` explain the intended facilitator behavior. These are model instructions, not a sandbox that can enforce narrative semantics. Scenario text, memory and journal content are game data, not permission to override the contract.

`get_play_context` is a convenient read snapshot, not a transaction held open across model reasoning. Another operation can make its version stale. Read again when a mutation reports a conflict. Journal pagination is stable by committed version; GM review creates version gaps without appearing as a player narration event.

## Commit one turn

Example tool arguments, after reading a context with `version: 1` and published start scene `harbor`:

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000001",
  "expected_version": 1,
  "player_action": "I help Mira search the pier before dawn.",
  "narration": "You find a torn page from the keeper's journal and promise to return tomorrow.",
  "memory_markdown": "# Current state\nAt the harbor before dawn.\n\n# Established during play\nArin helped Mira search. A torn page was found.\n\n# Pending campaign consequences\nMira may become a contact; the GM has not approved this.\n\n# Return\nRejoin the party at dawn.\n",
  "current_scene": "harbor",
  "consequences": [
    {"category": "relationship", "description": "Mira may become Arin's local contact."},
    {"category": "discovery", "description": "Arin brings back the torn page for the GM to reconcile with the main plot."}
  ]
}
```

Generate a new UUID for each logical operation; the fixed UUIDs here are examples. Retain exact arguments while awaiting acknowledgement. `memory_markdown` replaces the current memory; it is not an append operation. The server retains a hash of normalized operation arguments, not old full memory snapshots.

Limits: action 4,000 characters; narration 6,000; memory 16,000; 10 consequences; each consequence description 1,500. `current_scene` must identify a published scene, not an NPC or an unpublished node. Each effect must have exactly `category` and `description`; a caller-provided `status` is rejected. Categories: `resource`, `relationship`, `discovery`, `narrative`.

Default result at version 2:

```json
{
  "session_id": "<assignment UUID>",
  "version": 2,
  "consequences": [
    {"id": "<consequence UUID>", "status": "pending", "category": "relationship", "description": "Mira may become Arin's local contact."},
    {"id": "<consequence UUID>", "status": "pending", "category": "discovery", "description": "Arin brings back the torn page for the GM to reconcile with the main plot."}
  ],
  "replayed": false
}
```

The database locks the assignment row and commits memory, current scene, event, effects, version and receipt in one transaction. An identical operation UUID/body retry returns the original receipt with `replayed: true`, even after completion or later GM decisions. Its version/statuses describe **that operation**, not the latest state; reload context for current decisions. Reusing a UUID for different normalized arguments or another operation kind fails with a conflict. A stale version with a new UUID also fails without writes.

## Finish

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000002",
  "expected_version": 2,
  "summary": "Arin returns at dawn after helping Mira. The contact and journal-page discovery await the human GM."
}
```

The result has `status: completed`, `version: 3`, and `pending_consequences: 2`. Completion does not approve effects. New turns then fail; identical earlier operation retries remain valid while the token is valid. The GM can still review pending effects. There is no reopen operation.

## Errors and admission

- HTTP 401: missing, wrong-endpoint, expired or revoked token.
- HTTP 413: request body over 300 KB at the MCP gate (the proxy also has an outer limit).
- HTTP 429: per-token rate/concurrency, global concurrency or pre-auth admission exceeded. Respect `Retry-After` and back off; do not create a tight retry loop.
- HTTP 503: Redis admission or a database connection is unavailable.
- HTTP 400: malformed JSON, invalid storage text or excessive JSON nesting/complexity.
- HTTP 408: request body was not received within 10 seconds.
- HTTP 504: tool execution exceeded 25 seconds before response headers. After headers, the stream is aborted instead; a timeout does not prove that no write committed.
- A tool-level domain failure uses MCP `isError: true` (usually in a successful transport response). Its message starts with the domain status, e.g. `409: Version conflict...` or `404: Play session not found`.
- Invalid JSON/tool arguments can produce standard MCP protocol/validation errors.

Defaults remain 60 requests/minute per token, 2 admitted requests/token and 8 globally across **both** catalogs. Limits apply to all protocol requests, including initialize and discovery, not only mutations. The receive timeout is 10 seconds and tool transport deadline 25 seconds; an interrupted synchronous database call may outlive its async caller. PostgreSQL statement/lock timeouts bound database work. Admission limits are not narrative rules or DDoS protection.

## REST support for the human workflow

| Method and path | Access and purpose |
| --- | --- |
| `GET /api/plays/` | Logged-in GM/player, up to 200 most recent accessible assignments |
| `POST /api/plays/` | Source owner creates an assignment; client UUID makes identical retries safe |
| `GET /api/plays/{id}/` | GM or assigned player; private GM notes only returned to GM |
| `POST /api/plays/{id}/access/` | GM/player generates a scoped token; plaintext returned once |
| `DELETE /api/plays/{id}/access/` | GM/player revokes it |
| `GET /api/plays/{id}/journal/?offset=0&limit=10` | GM/player, ordered journal page |
| `POST /api/plays/{id}/review/` | GM only, versioned retry-safe decisions on pending effects |
| `GET /api/plays/{id}/report/?lang=en` | GM/player Markdown export; GM-only notes filtered by role |

Creation fields: `id` UUID, `scenario_id`, `source_version`, `player_username`, `mode` (`absence`, `downtime`, `solo`), `language` (`en`, `pl`), `character`, `brief`, `instructions`, `boundaries`, `return_condition`, `gm_notes`, `published_node_ids`, `auto_approve`. Required non-empty text: character, brief, boundaries and return condition. The web form documents visibility and bounds. A retry with the same creation UUID and changed content fails instead of creating a second assignment.

Review request:

```json
{
  "operation_id": "10000000-0000-4000-8000-000000000003",
  "expected_version": 3,
  "decisions": [{"id": "<consequence UUID>", "decision": "approved", "note": "Fits the main session; Mira remembers Arin's help."}]
}
```

Review 1–20 distinct pending consequences from that assignment. `decision` is `approved` or `rejected`; note max 2,000 characters and visible to the player. Completed activities may be reviewed. Token rotation does not change the narrative version; turns, completion and reviews do.

Neither API nor tool offers an arbitrary file path, Python execution, SQL, dice roll or automatic main-campaign mutation.

The private campaign wiki is available only to author MCP credentials; no wiki tool is added to the player catalog. [Campaign wiki contract](campaign-wiki.md).
