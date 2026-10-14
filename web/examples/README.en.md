# Example: a night away from the party

[Polski](README.pl.md) · [Guide](../docs/en/individual-play.md)

This small adventure gives an absent scout something meaningful to do while the party explores ruins. Arin stays in the harbor and helps Mira look for traces of her missing father. At dawn Arin rejoins the party with an account of the night and proposed consequences for the human GM.

## Install the materials

Import [`absence.en.json`](absence.en.json) through **Import JSON**, then save it to your account. It uses the existing web scenario schema and includes private author secrets for the human GM. Do not use the full source file as the handout for a private adventure. This shipped example is public in the open-source repository: it demonstrates field filtering, not secrecy of its already-published plot.

Alternatively install the same private scenario for an existing account, from `web/`:

```sh
docker compose exec api python manage.py seed_absence_example --owner YOUR_USERNAME --language en
```

The command does not create accounts or tokens and does not overwrite an existing example with the same owner/title. Create the player's account with normal administrator tooling if needed. You may assign to your own account to test solo play.

## Prepare the activity

In **Between sessions**, assign the saved scenario with these values:

| Field | Example |
| --- | --- |
| Activity | Absent character |
| Character | Arin, a scout returning to the party at dawn |
| Opening | While the party explores the ruins, Mira asks Arin to search the pier for signs of her father |
| Instructions | Run a short investigation with conversation and a meaningful choice; follow ideas beyond the graph |
| Boundaries | No permanent harm, rare rewards or resolution of the party's main quest; lasting discoveries/relationships require GM approval |
| Return condition | At dawn, Arin can meet the party in the harbor |
| Published nodes | `harbor`, `market`, `mira`; the starting scene `harbor` is required |
| Pre-approved categories | None |
| Private GM notes | Reconcile the report with the main session; keep the saltworks/chapel revelations private |

Read the actual descriptions before publishing. The source graph also contains `salt`, `ledger` and `chapel`, with information not needed for this small activity. Do not publish them for this example. Node `secret` fields are excluded even on the three selected nodes.

## Play through MCP

The assigned player generates a play token in the web panel and configures `/play/mcp`. Use the panel's starter message. The agent reads context, asks what Arin does and improvises within the boundaries.

For a deterministic tool-level walkthrough, use the exact commit/finish argument examples in the [MCP contract](../docs/en/mcp-play.md). They produce:

1. A single turn recorded at version 2: Arin helps Mira and finds a page. Memory records the pending nature of the contact/discovery.
2. Repeating the exact operation UUID and arguments returns a receipt without another journal entry.
3. Completion at version 3 records Arin's return but leaves both consequences pending.
4. A fresh agent conversation reads the same persisted state; the original chat transcript is unnecessary.
5. The human GM reviews each effect. For example, approve Mira remembering Arin's help, but reject the page revealing anything about the main villain.

The scene details in these tool examples are illustrative, not server-verified story facts. The automated play integration test uses this same start scene and the same pending-contact pattern, checks token/catalog isolation and revocation, resumes in a fresh MCP client, and reviews the consequence through the human REST workflow.

## Expected report

The report should distinguish:

- **Played events:** Arin searched with Mira and returned at dawn.
- **Approved consequences:** only the entries the GM accepted (or categories explicitly pre-approved).
- **Pending/rejected consequences:** not accepted as campaign facts.
- **Private GM notes:** only present in a GM export, never a player export.

The GM brings agreed effects back to the table manually. No inventory, character sheet or main campaign state is updated by this example. Actual random rolls and game-system mechanics are outside this release.
