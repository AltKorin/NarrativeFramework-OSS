# Campaign wiki: a world for the whole group

[Polski: full guide](../pl/campaign-wiki.md) · [Individual play](individual-play.md) · [Sharing design and delivery stages](campaign-sharing-plan.md)

A campaign owns its wiki. An account may own one campaign, co-GM another and play in a third. Articles, index, graph and backlinks use the same PostgreSQL records. Versioned copies reuse material across adventures.

## Joining and roles

Sign in, open `/campaigns/`, create a campaign and use **Members and invitations**. The owner creates a single-use link with a player/co-GM role and seven-day expiry, then sends it privately. The first signed-in account explicitly accepting it joins. Opening the link changes nothing; there is no email delivery or public account directory. Invitation tokens use the URL fragment rather than server-visible page query parameters. Login/registration returns the recipient to acceptance.

| Role | Capabilities |
| --- | --- |
| Owner | All drafts/secrets, editing/publication, members/invitations, settings and campaign deletion |
| Co-GM | All drafts/secrets, editing/publication, player preview and full GM exports; no member-role management or campaign deletion |
| Player | Only their published audience: read/search/graph/backlinks and filtered exports |

Co-GMs see **all GM secrets**. The owner can revoke unused invitations, remove members and change roles; role changes clear individual page/relationship grants. Members may leave. Rejoining needs a fresh invitation and does not restore old grants. The owner cannot leave; ownership transfer and player editing are not implemented. Campaign summary is group-facing: keep secrets in GM notes or private articles.

## Drafts, audiences and publication

Articles support Characters, Locations, Organizations, Items, World lore, Journal, Scenes and Clues. Safe Markdown includes formatting/tables; HTML is not executed and inline images/uploads are not implemented. GM notes remain separate.

New pages are GM-only and unpublished. **Save article** changes the draft, not the player view. In **Player publication**, choose all campaign players (including future players) or selected current player memberships, then **Publish for players**. This stores only saved title/category/body/version in a publication snapshot, never `gm_notes`. The panel shows draft and published versions. Later draft changes stay private until republished. **Withdraw publication** hides the page. Audience changes republish the currently saved content: review it first. Unsaved edits must be saved before publishing.

Publication and writes require current versions; stale operations return 409. **Preview as player** returns that member's actual published projection, without editing controls. Switch back to **GM view** for drafts. PL/EN switching preserves authored text/drafts. Secrets in ordinary prose or manually authored link labels are not automatically detected: review them, use GM notes or a narrower article audience. Per-paragraph secrecy is not implemented.

Relationships have independent GM/all/selected audiences and published label snapshots. Revealing both articles does not reveal a private edge. Players see a relationship only when both published endpoints and the edge are visible to them. Deleting/hiding an endpoint hides its edges. The map uses automatic positions, without persisted manual layout.

Index, counts, search, graph, backlinks, direct reads and export use the same server-side policy. Hidden records return 404. Players search published title/body only; GMs search drafts, excluding GM notes. Search returns at most 100 matches. The browser never receives hidden GM records for client-side filtering.

## Exports and adventure copies

**Export campaign (GM)** uses `narrative-wiki/2`: drafts, private notes, publications and audiences/grant IDs; no credentials/invitations. **Export shared material** uses `narrative-wiki-player/1`, with only the reader's current projection. GM preview exports the player schema too. Player Markdown omits notes. Whole-wiki reimport and Kanka import/sync are not implemented.

A GM may explicitly copy a saved campaign article into their own private adventure. Both source/destination versions and permissions are checked. Co-GM status grants no access to other users' adventures. Body becomes public description, GM notes become node `secret`, and `wiki_origin` records campaign/page/version. Existing four node types are preserved; other wiki kinds become clue/material nodes. Repeated copy of the same page into one scenario returns 409. Connect the copied node as appropriate.

Copies remain independent snapshots. Wiki edits, deletion or access revocation do not retract existing personal copies or separately published play assignments. Wiki publication does not automatically publish `/play/mcp` materials. Existing player-play publication still excludes node secrets/source metadata.

## Revocation, limits and migration

Every request checks current membership. Campaign-row locking serializes writes/publications/role changes/acceptance/revocation; a revoked writer waiting for that lock cannot use old authority. The UI rechecks on focus/visibility return and every 30 seconds while visible and idle, clearing inaccessible records/drafts. API revocation applies immediately; no autosave loop retries forbidden writes. Already delivered responses, downloads and external-agent memory cannot be retracted.

API responses use `Cache-Control: private, no-store`. GMs can view the last 100 audit events; stored events contain actor/action/resource/version, without secret text/tokens. Audiences and grant IDs are GM-only.

Limits: 20 owned campaigns/account; 50 invited members (owner separate), 20 active invitations and 30 invitation creations/hour/campaign; 1,000 articles and 2,000 relations/campaign. Titles 200, summary 4,000, body/notes 12,000 each, relation label 80 characters. Existing transport/admission limits apply. These are resource bounds, not a capacity guarantee.

Additive migration 0005 adds membership, invitations, audiences/publications and audit. All existing pages/relations remain GM-only and unpublished, with owners retaining access. Migration 0004 created the original wiki. Compose migrates before API/MCP startup. PostgreSQL backups include memberships/publications and are private data.

## REST and MCP

Session authentication; writes require CSRF.

| Endpoint | Operations |
| --- | --- |
| `/api/campaigns/` | GET accessible campaigns, POST create |
| `/api/campaigns/{id}/` | GET recipient projection; owner PUT settings/DELETE with campaign version |
| `/api/campaigns/{id}/pages/?q=...` | GET filtered search; GM POST article |
| `/api/campaigns/{id}/pages/{page}/` | GET filtered article; GM PUT full replacement/DELETE with page version |
| `/api/campaigns/{id}/pages/{page}/publication/` | GM POST `{version, audience, recipients}` |
| `/api/campaigns/{id}/relations/` | GM POST relationship |
| `/api/campaigns/{id}/relations/{relation}/` | GM DELETE |
| `/api/campaigns/{id}/relations/{relation}/publication/` | GM POST `{version, audience, recipients}` |
| `/api/campaigns/{id}/members/` | GM GET memberships |
| `/api/campaigns/{id}/members/{member}/` | Owner PATCH `{version, role}`; owner DELETE or member DELETE self |
| `/api/campaigns/{id}/leave/` | Non-owner POST |
| `/api/campaigns/{id}/invitations/` | Owner GET statuses/POST `{role}`; plaintext token returned only on creation |
| `/api/campaigns/{id}/invitations/{invite}/` | Owner DELETE (revoke) |
| `/api/campaign-invitations/inspect/` | POST `{token}`: inspect without joining |
| `/api/campaign-invitations/accept/` | POST `{token}`: explicit acceptance |
| `/api/campaigns/{id}/pages/{page}/use/` | GM POST `{scenario_id, version, source_version}` into own scenario |
| `/api/campaigns/{id}/export/` | GET role-filtered export |
| `/api/campaigns/{id}/audit/` | GM GET last 100 events |

Page fields: `title`, `kind`, `body`, `gm_notes`, plus `id` on creation or `version` on replacement. Omitted body/notes become empty. Publication audience values: `gm` (withdraw), `all`, `selected`; `recipients` contains current player membership UUIDs, nonempty only for selected. Role values: `gm`, `player`; owner cannot be assigned through memberships. Campaign/page/search/export reads accept `?preview=<player-membership-UUID>` for GMs only. Membership/invitation/audit responses are never accessible to players.

Author `/mcp` tools `list_campaigns`, `search_wiki`, `read_wiki_page`, `save_wiki_page` operate on owned/co-managed drafts and may include GM secrets. A player personal token cannot use them for that campaign. Each call checks current role, including demotion. Existing `/play/mcp` remains isolated, with no wiki access. **A separate `/campaign/mcp` reader catalog/token is not implemented yet**; it is the next stage, independent of browser sharing.

## Verification

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/wiki_integration.py
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/sharing_browser.py
```

Use a test instance. Scripts create and remove disposable fictional accounts. PostgreSQL tests cover concurrent invitation consumption, publication/edit conflicts and revoked waiting writers. Browser checks cover invitations/login return, all roles, selected audiences, filtered graph/search/export, preview, drafts, demotion/revocation, PL/EN and mobile.

Migration 0006 retains database defaults for new non-null wiki fields, so the owner-only code can still insert private pages/relations during a code rollback. Keep additive tables/data in place; reversing migrations discards sharing data.


Workshop entry: `/workshop/` now opens the dashboard. Choose **New campaign**, sign in and name your world; saved campaigns and adventures are listed here. **New adventure** creates a separate scenario with one empty opening scene. **Open adventure demo** opens the sample instead. Existing `/campaigns/` and scenario links remain valid.
