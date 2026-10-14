# Plan: a campaign wiki for the whole group

Status: **implementation plan**, not currently shipped functionality. [Polski: full design](../pl/campaign-sharing-plan.md) · [Current wiki](campaign-wiki.md).

## Product contract

A campaign owns its wiki. Accounts can own one campaign, co-GM another and play in a third. The minimum useful group release includes membership, invitations, role enforcement, publication, whole-group and individual audiences, filtered navigation/search/graph/export, revocation and a GM preview of a particular player's view.

| Capability | Owner | Co-GM | Player |
| --- | --- | --- | --- |
| Read drafts and GM notes; edit and publish articles/relations | Yes | Yes | No |
| Read/download published material | Yes | Yes | Own audience only |
| Invite, change roles and remove members | Yes | No | No |
| Full GM export | Yes | Yes | No |
| Delete campaign | Yes | No | No |
| Leave campaign | Ownership transfer required | Yes | Yes |

`Campaign.owner` remains the single owner. Other roles use unique campaign/user memberships. Ownership transfer/account deletion for owners is deferred. Co-GMs see every GM secret; promotion must explicitly explain this. Player edits, proposed changes and personal character journals are later work.

## Publication and audiences

Pages and relationships default to GM-only. Audiences are GM-only, all campaign players, or selected player memberships. Players see only a deliberately published snapshot of allowed fields, never a draft or `gm_notes`. Saving does not republish. Publishing/replacing a snapshot, changing audiences and withdrawing publication require an expected version, atomic authorization and an audit event. The UI distinguishes draft and published versions and previews an individual player's view. No per-paragraph secrets in this release.

Relationships have their own audience and published label snapshot: revealing both endpoint articles does not reveal a secret relationship. A player sees an edge only if both published endpoints and the edge itself are visible. Removing an endpoint or its access hides the edge immediately.

The same policy controls direct reads, index, search, counts, backlinks, graph, exports and MCP. Hidden resources return 404. Search indexes only the recipient's published content. The backend projects allowed fields; React never receives hidden records for client filtering. Hand-authored Markdown links grant no permissions, but their visible text remains authored published content for GM review. Campaign summary is group-facing, not a secret-note field.

## Invitations and revocation

Owner-created invitations use random, single-use bearer links, default expiry seven days, revocable and stored as digests. No email delivery is required. A recipient signs in/registers, sees campaign and role, and explicitly accepts. First accepting account consumes the link; the owner sees who joined. Opening a link changes nothing. Existing members cannot change their role by accepting/replaying an invitation. No account directory is exposed.

Acceptance and writes serialize on the campaign together with membership changes/revocation. Every request checks current authorization. Membership removal overrides page grants and deletes individual grants; rejoining does not restore them. No downgrade/removal of the owner through membership APIs. Initial limits: 50 members and 20 active invitations per campaign, with admission controls; these are resource limits, not capacity guarantees.

Private responses are excluded from shared caches. UI rechecks on focus/refresh and clears inaccessible records. Revocation prevents subsequent requests/writes; it cannot retract already delivered content, downloads or an external agent's memory.

## Architecture and migration

Planned models: `CampaignMember`, `CampaignInvitation`, page/relationship audiences and membership grants, published page/relationship snapshots, and `CampaignAuditEvent`. Audit actor/action/IDs/version, without secrets or plaintext tokens. A shared `campaign_access` policy serves `stories/wiki.py`, REST and MCP. GM and player serializers use separate positive field allowlists.

Migrations are additive: existing owner access remains, existing pages and edges stay GM-only and unpublished. No automatic sharing. Full GM backups and player exports require separate versioned schemas and exclude credentials/invitations. Existing author wiki tools remain GM-only even when a personal token belongs to someone who plays in that campaign.

A GM may copy an article from a campaign they co-run into their own scenario. Campaign membership does not grant access to somebody else's scenarios or play sessions. Existing scenario/assignment snapshots remain independent; withdrawing wiki access does not retract a separately published play snapshot. Document this explicitly.

## Campaign reader MCP

After the group wiki release, add separate `/campaign/mcp` with expiring, revocable, digest-stored read tokens scoped to one campaign/account. Search/read/visible-relations tools always return the player publication view, including for an owner using a reader token. Check membership/grants on every call. Role changes/removal revoke campaign tokens. No writes or GM notes.

Existing `/play/mcp` tokens gain no campaign access. Connecting a pseudo-GM to campaign knowledge later needs an explicit character/session scope, never the owner's personal author token. Model instructions are not authorization.

## Delivery and acceptance

1. Add data, shared authorization, invitations, publications, grants and audit, with PostgreSQL race tests.
2. Deliver the complete group UI: membership/invitation acceptance, roles, publication/audiences, GM preview, player pages/graph/search/export, revocation, PL/EN and mobile. Steps 1–2 together form the minimum useful release; do not expose a partially protected feature.
3. Add the separate reader MCP catalog and real-protocol isolation tests.
4. Later: player contributions, character journals, Kanka import and ownership transfer.

Acceptance: owner invites a co-GM, Alice and Bob. Both players see published “Port”; only Alice sees “Alice's debt”; only GMs see the mayor's true identity and private notes. A GM-only edge between two visible pages stays hidden. Draft edits do not change the published view. Removing Alice blocks direct reads, search, exports and MCP without changing Bob's scope. Demoting a co-GM removes private reads and writes.

Also test privilege escalation and swapped campaign/page/member IDs, hidden titles/counts/edges/drafts, export secret filtering, concurrent invitation consumption, publication versus edit, member removal versus write, rejoining without old grants, role-change cache behavior and unchanged player-play MCP. Deployment requires a private DB backup, all old content remaining private, independent-account HTTPS checks, bilingual docs and a rollback procedure. Old owner-only code may safely hide shared data on rollback but cannot preserve group collaboration.
