# Individual adventures: downtime, absence and solo play

[Polski](../pl/individual-play.md) · [Installation](../../README.md) · [MCP contract](mcp-play.md)

Narrative Framework keeps its adventure editor and author MCP. Individual play adds a way for a human GM to assign a small adventure to one player, who plays it with an external agent. It is useful between sessions, during downtime, or to explain what an absent character did while the party adventured elsewhere.

The application supplies materials and durable memory. Your MCP-capable client supplies the language model and conversation. There is no built-in AI chat, model subscription, dice engine or autonomous background agent in this release.

## Three objects, three responsibilities

| Object | Purpose |
| --- | --- |
| Scenario | Author's editable graph of scenes, NPCs, locations and clues, including secrets |
| Play session | Frozen assignment for one player/character, with selected public materials and instructions |
| Report | Markdown record of played events, working memory, proposed effects and GM decisions |

An account is a GM for scenarios it owns and a player for assignments addressed to it. There is no global GM role. A solo player can assign their own scenario to themselves. Modes label the intended activity; they do not implement different RPG rules.

## 1. Prepare and assign

1. Open the public demo, import an example or write a scenario. Log in and save a private copy.
2. In **Between sessions**, choose **Assign this adventure**. Save outstanding scenario edits first.
3. Enter the existing player's username and character description. Players can create test accounts in the workshop when public registration is enabled; administrators can also provision accounts.
4. Choose **Absent character**, **Downtime** or **Solo**, and an adventure language.
5. Write the opening situation, agent instructions, limits of improvisation and return condition. The **Use example briefing** button provides an editable starting point.
6. Select the node descriptions to publish to the player's agent. The starting scene is mandatory. Only links whose endpoints are both selected are copied.
7. Leave all pre-approved categories unchecked for human review of every lasting consequence, or explicitly allow categories appropriate to this activity.
8. Put private reminders in **Private GM notes** and create the assignment.

The assignment copies the selected descriptions, scenario title/version and instructions. Later scenario edits or deletion do not rewrite a running activity. Instructions and publication cannot be edited after assignment in this first release; create a new assignment for a revised brief.

### What the player can inspect

Everything delivered to an agent in a player's own MCP client is inspectable by that player. **Agent instructions are not a secret channel.** Publish only material you are willing to reveal in that client. Original node `secret` fields, extra node fields, unselected nodes and private GM notes are excluded by the backend's field allowlist.

The API does not infer which ordinary description text contains spoilers. A secret pasted into a public description or agent instruction will be visible. Publication is a deliberate author decision, not automatic spoiler detection. Dynamic secret unlocking is not implemented.

## 2. Connect the player's agent

The assigned player logs into the web app and selects the activity. Open **Connect your agent**, generate a play token and copy it. The GM can also issue/revoke it, but should normally let the player generate their own. There is one active token per assignment, expiring after 14 days; rotation invalidates the previous token.

Configure a client supporting Streamable HTTP and custom Bearer headers:

```json
{
  "url": "https://narrativeframework.com/play/mcp",
  "headers": {"Authorization": "Bearer YOUR_PLAY_TOKEN"}
}
```

Locally the URL is `http://localhost:8080/play/mcp`. This is transport information, not a universal client configuration file. Do not paste the token into the adventure, chat transcript, repository or report. The web panel's starter message does not include it.

Give the agent the starter message from the activity panel. It should read `get_play_context`, ask what the character does, improvise within the brief and save meaningful resolved actions. A new conversation/client can resume by reading the same context with a valid token.

Player tokens only work on `/play/mcp`, with a separate catalog of five play tools. They cannot call `/mcp` author tools or authenticate to the browser REST API. The author endpoint and its personal tokens remain available for existing writing workflows.

## 3. Play and preserve memory

`commit_play_turn` atomically records the player's action, narration, new Markdown working memory, current published scene and proposed consequences. The journal preserves actions/narration; current memory is replaced with the agent's latest concise summary. Keep important facts in that summary. Earlier memory revisions are not retained as a separate version archive.

Memory is Markdown **stored as text in PostgreSQL**, not a shared loose file edited by several processes. This lets the same database transaction commit memory, consequences, version and retry receipt. Download a readable `.md` report at any time; backups use the existing PostgreSQL backup procedure. No new persistent filesystem volume is required.

The initial context includes published materials, current memory, the last five play events and up to twenty recent consequences. Read older journal pages explicitly. Search uses literal case-insensitive matching, not embeddings. A context is a read snapshot; another committed operation may make its version stale immediately afterwards.

The server provides instructions for the facilitator. It does not ensure a model follows them. There are no server-side random rolls or RPG rules enforcement. State the chosen resolution method honestly; an agent must not claim this server rolled dice.

## 4. Finish, review and return to the party

The agent calls `finish_play_session` with a recap. The activity becomes completed and accepts no new turns. Identical retries still return their earlier receipts. Pending consequences stay pending, and the GM may review them after completion.

In the web panel, the GM approves or rejects each pending consequence and can add a note visible to the player. Use the Markdown report to reconcile the character's return with the main session. Player exports omit private GM notes; GM exports include them.

### What counts as an accepted consequence

Categories are `resource`, `relationship`, `discovery` and `narrative`. The agent submits category and description; it cannot submit its own approval status. The backend marks a consequence approved only if the GM pre-approved its category, or if the GM approves it later. Otherwise it remains pending or is rejected.

This is an **approval ledger**, not a proof that prose obeys the brief. A model could narrate an out-of-bounds event, mislabel a consequence or omit one. Memory, narration and recap are unverified play records; only approved consequence entries are accepted within this assignment. Even approved entries do not automatically update a main campaign, inventory or character sheet. The human GM reconciles that integration.

For conservative play, leave every category pending. Pre-approving `resource` permits all submitted resource descriptions, not just small purchases; numeric caps and semantic classification are not implemented.

## Current limits

- One assigned player per activity; no multi-player simultaneous conversation or anonymous invitations.
- Account required; self-service registration depends on the instance setting. One active scoped token, 14-day lifetime; revocation blocks future calls, not a response already in flight.
- 100 resolved turns per activity, 10 proposed consequences per turn, 16,000 characters of current Markdown memory.
- No reopening, reassignment, per-turn dice service, automatic campaign merge, full memory history or file-system editing through MCP.
- Play and author MCP share global admission and PostgreSQL; process separation from the web API remains partial overload isolation, not a capacity guarantee.

See the [complete example](../../examples/README.en.md), [MCP contract](mcp-play.md) and [architecture decisions](../architecture.md).
