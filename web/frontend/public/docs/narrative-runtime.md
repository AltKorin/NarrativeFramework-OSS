# Narrative runtime in the web application

[Polski: prowadzenie kampanii](pl/campaign-runtime.md) · [Campaign wiki](en/campaign-wiki.md)

The campaign screen now uses a Python port of the repository's MIT-licensed Unity
narrative core. This code is part of the same open-source distribution. It is a
working deterministic subset, not a complete replacement for the Unity runtime.

## Source and behavior

| Unity source under `Assets/NarrativeFrameworkForUnity/Runtime/Scripts/` | Web implementation |
| --- | --- |
| `Data/SimEventSO.cs`, `IsAvailable` | Inclusive `MinTimeUnit` / `MaxTimeUnit`, all required flags and no forbidden flags; explicit blocker explanations |
| `Data/StoryArcCondition.cs`, `Evaluate` | Numeric resource, narrative variable and actor attribute comparisons (`GreaterThan`, `LessThan`, `Equal`); missing values block the event |
| `Data/ActorSO.cs` | Actor identity comes from an NPC, player character or organization wiki page; mutable numeric attributes are stored separately from prose |
| `Data/Outcome.cs` | Add/remove flags, resource and variable deltas, narrative booleans, directional relationship values |
| `Managers/SimulationManager.cs` | Explicit GM-controlled advance by one campaign day |

`ModifyActorAttribute` is a web extension for numeric character stat deltas.
Relationship values identify both actors and the relationship definition. They
are set absolutely, while resource, variable and attribute changes are deltas.
Float equality uses a small tolerance; this port uses Python numbers rather than
Unity single-precision floats and does not promise bit-for-bit numeric parity.

Unity's random selection, arbitrary custom C# outcomes, story arc activation,
linked event execution, actor switching, achievements and feature unlocking are
not implemented here. Outcomes with probability other than 1 and unsupported
outcome types are rejected. An expired event remains visible; advancing time does
not automatically resolve it or apply a hypothetical consequence of inaction.
The GM can define an alternative resolution or a separate later event for that.
Events resolve at most once. Future/unresolved definitions can be edited;
completed decisions are preserved.

## A concrete workflow

1. Add a Guard NPC and a Hero player character to the campaign.
2. Open **World state and stats** and set party `gold` to 12. In **Characters**,
   open Guard and add `health` with value 10. New names require **Create a new name**.
3. Plan **The gate closes**, available on days 2–3 while Guard `health > 0`.
4. Add **Pay the guard**, with `ModifyResource gold -5` and `AddFlag passage_open`.
5. Plan a later event requiring `passage_open`.
6. On day 1 the gate event explains that it is too early. Choose **Next day**,
   review changed deadlines/availability, then **Confirm and save**.
7. Choose **Review consequences: Pay the guard**. The preview shows gold 12 → 7,
   the new fact and any event becoming available. **Back** cancels without changing
   state; **Confirm and save** applies the decision once and shows its result.
8. Reload: values, event completion and the day of the decision persist.

This connects prep, decisions and state changes; Markdown articles provide
background rather than acting as the authoritative stat store. Existing prose is
not parsed into rules or changed automatically.

## Storage and API

`Campaign.runtime` stores `narrative-runtime/1`. The schema contains `day`, `flags`,
`resources`, `variables`, `bools`, `actors`, `relationships`, `events`, `completed`
and `history`. It is initialized lazily for existing campaigns.

`GET /api/campaigns/{id}/runtime/` returns state, current campaign `version` and
computed event blockers. `POST` accepts a command and that expected `version`:

- `set_value`: `scope` (`resources`, `variables`, `actors`), `key`, `value`, and
  `actorId` for an actor stat;
- `set_flags`: complete `flags` list;
- `add_event` / `update_event`: an `event` definition;
- `advance`: one day, without automatic event execution;
- `resolve`: `eventId`, zero-based `choice` index.

See `test_narrative_engine.py` for executable examples. This API uses the web
runtime schema. It does **not** import Unity `ScenarioJson` 1.1.0 wholesale: that
export format does not carry all runtime scheduling/actor attribute data, and
some Unity outcomes need runtime object references or C# implementations.

Commands require GM/owner access, hold the campaign lock, compare the version and
persist state plus the campaign audit in one database transaction. A stale retry
returns 409; a resolved event cannot apply twice even after loading a new version.
PostgreSQL provides the deployment row lock; SQLite is for local development.
The engine computes on a copy so a failed effect leaves the original state intact.
Bounds: 250 events, 20 choices/event, 50 effects/choice, 50 stat conditions/event,
2,000 history entries, 1 MB serialized state and the shared JSON complexity limit.

The runtime is GM-only. Player publications, player exports and player previews
never include it. A GM campaign export includes the stored runtime so world state
is not lost when exporting. Published Markdown remains an explicit snapshot;
private live stats are not automatically published. Referenced actor pages cannot
be deleted or changed into a non-actor category, preserving rule references.

## Verification

Run `cd web/backend && ../.venv/bin/python manage.py test stories --noinput` and
`cd web/frontend && npm run build`. Engine tests cover Unity time/flag semantics,
stat conditions, consequence chains, actor stats, relationships, failed-command
atomicity, invalid values, explicit unsupported outcomes, duplicate resolutions,
version conflicts and the GM/player boundary.

For a local browser regression, run `web/.venv/bin/python web/tests/runtime_local_browser.py`
from the repository root with the same database settings as the running backend.
Set `SITE_TEST_BASE` if the frontend is not at `http://127.0.0.1:5173` and configure
that origin in the backend's `CSRF_TRUSTED_ORIGINS`. Playwright needs an installed
Chromium; `BROWSER_EXECUTABLE_PATH` optionally selects one. The test creates and
removes its own user/campaign and writes screenshots to a temporary directory.

Apply migration `0007_campaign_runtime` before using the screen on an existing
installation. No Unity assets or existing wiki prose need migration.

## Campaign workspace

The GM opens a campaign in **Run session**, with currently available events ordered
by deadline. Upcoming events and unmet conditions remain visible alongside the
session; resolved and expired events are collapsed. **Timeline** shows event
windows and an archive filter. **Characters** opens NPC, player character and
organization sheets combining wiki descriptions, current numeric stats,
relationships and related events. **World material** retains the wiki editor,
publication controls, sharing and relationship map. Existing page URLs still open
the corresponding article; player accounts only receive their shared materials.

Preparing or editing an event uses a separate dialog. Conditions and participants
are optional, expandable fields; existing stat names are selected, with an explicit
option to create a new name.

`POST /api/campaigns/<id>/runtime/preview/` accepts the same versioned commands as
the runtime endpoint. It executes the authoritative engine on a copy, validates
actor references, and returns the predicted runtime without saving state,
advancing the campaign version or adding audit records. The UI compares that
result with the current state to show before/after values and event availability.
Both resolution and time advancement require reviewing this result before saving.
A stale version still returns 409, including when another GM changes the campaign
between preview and confirmation. The preview endpoint has the same GM-only access
as the runtime endpoint.

Time advancement does not automatically resolve overdue events. This release has
no undo command. Numeric relationships currently come from event outcomes;
editorial relationships remain in the material map.

Browser acceptance tests: `web/tests/runtime_local_browser.py` uses the local
Django database and dev proxy; `web/tests/runtime_browser.py` defaults to disposable accounts on the local Compose instance
(`http://localhost:8080`). Set `SITE_TEST_BASE` to override the URL;
`SITE_TEST_SSH` explicitly selects a remote Compose host under
`/opt/narrativeframework/web`. `BROWSER_EXECUTABLE_PATH` optionally selects Chromium.
CI runs this same browser flow against its disposable stack. Their shared flow checks preparation, character
stats, preview cancellation, deadlines, downstream availability, commit,
persistence, history, material deep links and mobile layout.
