# Game Flow

This document outlines the canonical flow of a single time unit (e.g., a "Day") as orchestrated by the `GameController`. It details the sequence from the start of the day through event presentation, player choice, and outcome resolution.

## 1. Time Unit Start (e.g., Day Start)
- The `GameController` initiates the start of a new time unit.
- `SimulationManager.OnTimeUnitStarted` event is fired.
- The system checks for any story arcs that meet their activation criteria (`StoryArcManager`). If a high-priority arc activates, it may inject its starting event into the queue.

## 2. Event Generation
- The `EventSystem` selects an appropriate `SimEventSO` for the current day.
- **Selection Logic Priority**:
  1.  An event triggered by an activated `StoryArc`.
  2.  An event triggered directly by a previous choice (`Choice.NextEvent`).
  3.  A scheduled main event from `GameData` that meets all its conditions (day range, required/forbidden flags).
  4.  A random event from `GameData` that meets its conditions.
- If no event is found, the day may end automatically.

## 3. Event Presentation
- The selected `SimEventSO` is passed to the `UIManager`.
- The `UIManager` displays the event's title, description, and visual elements (illustration, background).
- The `ChoiceEngine` receives the event's choices, filters them based on their own conditions, and passes the available choices to the `UIManager`.

## 4. Player Choice
- The player selects a `Choice` from the UI.
- The `UIManager` fires an `OnChoiceSelected` event, passing the chosen `Choice` object.

## 5. Outcome Application
- The `GameController` receives the selected `Choice`.
- It retrieves the list of `Outcome` objects associated with that choice.
- For each `Outcome`:
  - The system checks if the `_actions` list is populated. If so, it processes the multi-action system. Otherwise, it falls back to the legacy single-action fields.
  - **Multi-Action Processing**: 
    - Each `OutcomeAction` in the `_actions` list is evaluated based on its individual `_probability`.
    - For each successful roll, the action is applied.
  - **Outcome Application**: The `Apply` method of the outcome (or action) is called. This is where the game state is mutated:
    - Resources are modified (`ModifyResource`).
    - Flags are added or removed (`AddFlag`, `RemoveFlag`).
    - Narrative variables are changed (`ModifyNarrativeVariable`, `SetNarrativeBoolTrue/False`).
    - Relationships are updated (`SetRelationship`).

## 6. Post-Outcome Evaluation
- After all outcomes from the choice have been applied, the system re-evaluates the state.
- **Story Arc Check**: The `StoryArcManager` checks again if any story arcs have now met their activation criteria as a result of the outcomes. If an arc triggers, it may queue up an immediate follow-up event.

## 7. Next Event Resolution
- The system determines the next step based on a clear priority:
  1.  **Immediate Story Arc Event**: An event triggered by a story arc in the previous step.
  2.  **Direct Next Event**: The `NextEvent` field on the selected `Choice`.
  3.  **End of Day**: If the selected `Choice.GoToNextDay` is true.
  4.  **End of Event**: If none of the above, the event simply ends, and the player is typically presented with the "Next Day" button.

## 8. Persistence & Loop
- The game state is saved via the `AsyncSaveManager`.
- If the day has ended, the `GameController` advances the `SimulationManager` to the next time unit, and the loop returns to Step 1.
- If a new event was queued, the loop returns to Step 3 (Event Presentation).