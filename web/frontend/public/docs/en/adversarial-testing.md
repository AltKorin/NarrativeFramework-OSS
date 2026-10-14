# Adversarial testing and regression foundation

[Polski](../pl/adversarial-testing.md) · [Verification history](../verification.md)

This pass intentionally attacked the application rather than only exercising successful author/player workflows. The target was local disposable data: temporary accounts, private sample scenarios and an isolated Docker Compose project. No public domain or real campaign was targeted.

## Reproduced failures and fixes

| Attack / interleaving | Before | Regression / resulting behavior |
| --- | --- | --- |
| Password supplied as object/list/boolean/number | HTTP 500 from authentication code | Type/length validation; HTTP 400 |
| NUL, unpaired surrogate, invalid text hidden in metadata | PostgreSQL/encoding errors; some invalid values accepted on SQLite | Validate keys and values before persistence; HTTP 400, no writes |
| A 1,001-digit coordinate | `math.isfinite` raised OverflowError | Range check before float conversion; HTTP 400 |
| JSON nested 1,200 levels | RecursionError / HTTP 500 | Bounded JSON parser and iterative depth/value validation; HTTP 400 |
| Valid schema missing optional text/secret/position fields | Saved document could not be reopened by the editor | Canonical empty text and zero coordinates added on save |
| Same scenario creation UUID with changed title/document | Silent 200 ignoring new content | Immutable creation fingerprint; changed-content retry is 409 |
| Lost create response, then retry (also after reload) | Duplicate private scenarios | Browser retains the operation UUID until acknowledgement, including across reload |
| Edit draft after a lost acknowledgement, then retry | Duplicate scenario or risk of overwriting local work | Same operation ID conflicts; edited local draft remains available |
| Logout while editing a local demo | Local draft replaced with seed content | Logout preserves local draft; private server story returns to the separately stored demo |
| Import a node with `kind: ["npc"]` | Coercion let a malformed document reach confirmation | Strict string-kind/JSON validation rejects it before replacement |
| Four concurrent author-token rotations with simulated DB latency | Three HTTP 500s in the reproduced schedule | Per-user row lock makes rotation transactional; one current token remains |
| Read paused after version lookup while another turn commits | Old version/memory mixed with a newer journal | Context read locks the same session row as mutations |
| Repeated DB health checks | Probe consumed an anonymous throttle and returned 429 | Probe is independent of the user/cache throttle |
| Incomplete MCP body / execution deadline | Unhandled timeout, connection failure or generic server error | 408 during receive; 504 before response start; abort an already-started response without sending a second status |
| Redis/PostgreSQL unavailable | Generic errors on affected paths | Controlled 503; no rejected write appears after recovery; existing workers reconnect |

The initial PostgreSQL suite of 12 adversarial test methods produced 20 failed assertions and two errors, including subtests. Those are case counts, **not** twenty distinct vulnerabilities. The browser baseline failed four of its five initial checks; HTML injection already rendered as text. The final suite contains 48 Django test methods plus browser, real-protocol and fault-injection checks.

## Tested boundaries that held

- Wrong user cannot read another private scenario/session or review its consequences.
- A player token cannot authenticate to author MCP or REST; author tokens cannot authenticate to player MCP.
- Player tool discovery excludes author tools. Extra arguments cannot retarget the scoped session.
- Caller-selected `status: approved` and unpublished current scenes are rejected without a committed turn.
- Source secret fields, extra private fields and GM notes are absent from player context/report.
- Script/HTML-like imported content is rendered as text, not executable DOM.
- Oversize bodies, malformed JSON, disconnects and timeouts release admission leases. A response already started never receives a second HTTP status.
- Identical operation retries do not duplicate turns/effects; stale/changed operations conflict. Human approval remains a separate operation.

These checks exercise backend authority, not a language model's willingness to obey a prompt. A model may still narrate false or forbidden events; the approval ledger remains the boundary for accepted consequences.

## Start a disposable target

Use the existing configured `web/.env` and Docker Compose. From repository root:

```sh
HTTP_PORT=127.0.0.1:18080 HTTPS_PORT=127.0.0.1:18443 \
  docker compose -p narrative-adversarial -f web/compose.yaml up --build -d
```

Wait for startup to finish. This project has separate PostgreSQL/Redis containers and named volumes. Its HTTP port is bound to loopback. It shares the source configuration and build cache, not the main database. Never point destructive/fault tests at a real campaign service.

## Repeat the checks

Database and deterministic ASGI cases:

```sh
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api \
  python manage.py test stories --verbosity 1
```

Real MCP (including hostile player calls) and real slow-body test:

```sh
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api python < web/tests/integration.py
docker compose -p narrative-adversarial -f web/compose.yaml exec -T \
  -e PLAY_TEST_BASE=http://web -e PLAY_TEST_HOST=localhost:18080 api python < web/tests/play_integration.py
docker compose -p narrative-adversarial -f web/compose.yaml exec -T api python < web/tests/adversarial_slow_body.py
```

Browser fault simulation, using the optional Playwright dependency described in verification:

```sh
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers \
  web/.venv/bin/python web/tests/adversarial_browser.py
```

This creates a temporary user in `narrative-adversarial`, aborts only its own scenario-create response **after** the server commits, retries, reloads and verifies the result. It also checks local-draft retention and hostile imports. Credentials are kept in process memory and not printed. It does not interrupt arbitrary application traffic.

Dependency failure/recovery, run **after** the other checks finish:

```sh
web/.venv/bin/python web/tests/adversarial_faults.py
```

This script is hardcoded to the disposable project and loopback port. It stops/restores only its Redis/PostgreSQL, verifies 503/no write/recovery and restores both dependencies in `finally`. Do not rebuild containers or run other workflows against this target concurrently with it.

Stop the target afterwards (volumes retained):

```sh
docker compose -p narrative-adversarial -f web/compose.yaml down
```

## Input, retry and failure contracts after hardening

- HTTP request bodies use `application/json`; parser read limit 512 KiB, nesting at most 32, at most 50,000 values. MCP's outer body limit remains 300 KB; schema documents remain capped at 256 KB after canonicalization.
- JSON text/keys must encode as valid UTF-8 and contain no NUL. Nonfinite numeric values are rejected. Ordinary Polish, newlines and Unicode text remain supported.
- New scenarios store a fingerprint of the normalized creation body. A matching retry returns the current owned scenario, even after later edits. Legacy records without fingerprints compare against their current content because the original creation body cannot be reconstructed.
- API scenario creation serializes the 50-private-scenario quota under an owner lock. The management command/direct database access remains trusted administration, not the API quota path.
- A context is coherent while read; another operation may make its version stale immediately after return. Old mutation receipts still describe their original operation, not current approval state.
- Health checks test database availability, independently of Redis throttling. Other admission failures fail closed. Dependency errors use generic 503 responses rather than exposing internal connection details.
- Cancelling an async response does not guarantee cancellation of already-running synchronous database work. Retry ambiguous mutations with the original operation UUID/body and re-read context.

## Remaining scope

No claim of a complete penetration test, sustained capacity, semantic RPG enforcement or live-model prompt-injection immunity. The pass did not exercise every network partition, OS resource-exhaustion case, browser engine, OAuth flow (not implemented), TLS/DNS deployment or target Proxmox/LXC host. Context still includes only the most recent twenty consequence entries; the full ledger remains in the web detail/report. The public DB probe is deliberately not protected by a per-user throttle; deployment-wide edge/load controls are outside this regression suite.
