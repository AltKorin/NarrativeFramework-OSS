# Campaign wiki: one world, multiple adventures

[Polski](../pl/campaign-wiki.md) · [Individual play](individual-play.md) · [MCP contract](mcp-play.md)

The campaign wiki is a separate, durable collection of articles. It does not use a scenario's 250-node graph as the world database. Its article list, relationship map and backlinks are views over the same campaign/page/relation records in PostgreSQL.

## Using the wiki

1. Sign in to the workshop, then choose **Campaign wiki**, or open `/campaigns/`.
2. Create a campaign. **Campaign settings** lets you rename it, edit its description, reload or delete it. Deletion asks for confirmation and removes its wiki pages/relations, while adventure copies remain.
3. Add articles in Characters, Locations, Organizations, Items, World lore, Journal, Scenes or Clues.
4. Write the main body as Markdown. Use **Preview** to read headings, lists, tables and formatted text. HTML is not executed; inline images/uploads are not implemented. Keep private reminders in **Private GM notes**.
5. Add a labelled relationship to another saved article. It appears on both articles (outgoing/incoming) and on the campaign map. Clicking a map node opens the article. Map positions are an automatic layout, not persisted editing coordinates.
6. Search article titles and main body. Search returns at most 100 matches and does not index private GM notes. Empty search displays the campaign index, grouped by category.
7. Export the whole campaign as `narrative-wiki/1` JSON, or download an article as Markdown. Both exports are **GM copies containing private notes**. Whole-campaign reimport and Kanka import/synchronization are not implemented.

The first release is private to the owning account. “Shared world” means reused across that owner's adventures; it does not yet add player wiki access, co-GM roles or collaborative campaign membership. Interface PL/EN switching preserves authored text and drafts; it does not translate the world.

## Using an article in an adventure

Save the article, select one of your saved private adventures and choose **Copy to adventure**. The server checks ownership and both the source article version and destination scenario version. A concurrent edit returns 409 instead of silently copying a version the author has not reviewed.

The copy becomes a scenario node with source campaign ID, page ID and version in `wiki_origin`. Its title/body become the node's title/public description; GM notes become the node's `secret`. NPC/location/scene/clue retain their types. Other wiki categories become clue/material nodes because the adventure schema currently has four node types. Connect the copied node as appropriate in the adventure graph. The node inspector links back to its source article.

The same article can be copied into multiple adventures. A second copy of the same article into one adventure is rejected, avoiding accidental duplicates after a lost response. Copies are deliberate snapshots: later wiki edits or deletion do not rewrite an adventure, player assignment, journal or memory. Live synchronization/refresh is not implemented.

Player publication still uses the existing explicit field/node allowlist. A wiki copy is not automatically published. Source metadata and node `secret` are excluded from player snapshots; any sensitive prose placed in an ordinary body/description is still the author's responsibility.

## Persistence, versions and limits

Migration `0004_campaign_wikipage_wikirelation` adds Campaign, WikiPage and WikiRelation without changing old scenarios or play data. Compose runs migrations before starting API/MCP.

- 20 campaigns per owner, 1,000 articles and 2,000 explicit relationships per campaign.
- Titles: 200 characters; campaign description: 4,000; article body and private notes: 12,000 each; relationship label: 80.
- Campaign/page/relation creation accepts client UUIDs. Campaign/page creation records immutable content fingerprints: exact replay returns the current record, changed-content replay conflicts. A duplicate semantic relation returns the existing relation.
- Campaign mutations serialize on the campaign row. Article writes/deletion require the current article version. Campaign settings/deletion require the current campaign version, which also changes when its pages/relations change.
- Stale writes return 409. The browser retains the current draft and provides an explicit reload confirmation. Current-tab creation retries reuse their UUID; an unsaved browser draft is not a server backup. Navigation away from a dirty article warns before leaving.
- Existing JSON/storage/body limits and authentication/admission rules still apply. Full wiki export is owner-only and may be larger than a request body; there is no unbounded MCP whole-wiki download.

## REST and author MCP

Session-authenticated REST:

| Endpoint | Purpose |
| --- | --- |
| `/api/campaigns/` | GET own campaigns; POST create |
| `/api/campaigns/{id}/` | GET index/relations; PUT settings; DELETE campaign (version required) |
| `/api/campaigns/{id}/pages/?q=...` | GET bounded search; POST article |
| `/api/campaigns/{id}/pages/{page}/` | GET article; PUT full replacement; DELETE (version required) |
| `/api/campaigns/{id}/relations/` | POST labelled relationship |
| `/api/campaigns/{id}/relations/{relation}/` | DELETE relationship |
| `/api/campaigns/{id}/pages/{page}/use/` | POST `{scenario_id, version, source_version}` for an explicit copy |
| `/api/campaigns/{id}/export/` | GET private campaign JSON |

Article write fields are `title`, `kind`, `body`, `gm_notes` plus `version` on update or `id` on create. Use all fields on replacement; omitted body/notes become empty. The backend rejects foreign campaigns/pages/scenarios and cross-campaign relationships.

Author `/mcp` adds `list_campaigns`, `search_wiki`, `read_wiki_page`, `save_wiki_page`. Search returns metadata, read returns one page including its GM notes, save replaces an existing page with an expected version. Read first and preserve private notes when saving. These tools are absent from player `/play/mcp`; player tokens cannot browse the campaign.

## Verification

```sh
docker compose -f web/compose.yaml exec -T api python manage.py test stories
docker compose -f web/compose.yaml exec -T api python < web/tests/wiki_integration.py
PLAYWRIGHT_BROWSERS_PATH=/tmp/narrative-browsers web/.venv/bin/python web/tests/wiki_browser.py
```

Run against a development/test instance. The integration/browser scripts create disposable users and delete their data on completion. PostgreSQL-only tests exercise concurrent edits and cross-owner UUID collisions. Browser checks cover Markdown injection, ownership, search, relationships/backlinks/map, stale-write draft retention, export, copied secrets/source metadata, direct adventure navigation, PL/EN and mobile layout.

## Campaign sharing plan

[Campaign sharing plan](campaign-sharing-plan.md) — planned next release, not implemented yet.
