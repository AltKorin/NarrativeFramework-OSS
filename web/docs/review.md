# Architecture review

Claude performed a bounded read-only review using the installed `grill-with-docs` skill (grilling/domain-modeling), with the parent implementation agent resolving findings.

Applied: total validation of reference types and finite coordinates; no default production secret; session-only REST so an MCP token cannot bypass MCP admission; shared Redis throttles and dedicated login throttle; pre-authentication MCP admission; database connection cleanup outside Django requests; explicit aggregate-size and versioning semantics; negative and integration tests.

The review observed files while implementation was in progress. Missing lockfile/tests/cache configuration noted in that snapshot were completed and verified before handoff.

Deferred explicitly: OAuth, revision history/audit product, fine-grained patches, background jobs, live collaboration and Unity adapters. The configured global MCP admission cap is not a guarantee of that many parallel SQL executions: thread-sensitive Django calls are serialized within each worker. API and MCP still share database resources. Retries of a successful versioned save return a conflict rather than silently replaying it; creation uses a client UUID.

Product assumptions are recorded in architecture.md, including the anonymous demo boundary and the difference between structural hints and narrative correctness. No capacity claim is made.
