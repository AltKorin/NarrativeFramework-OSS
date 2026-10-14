# Unity Narrative System - Integration Guide

## Table of Contents
1. [Overview](#overview)
2. [System Requirements](#system-requirements)
3. [Quick Start Integration](#quick-start-integration)
4. [Step-by-Step Integration](#step-by-step-integration)
5. [The Narrative Linker](#the-narrative-linker)
6. [API Reference](#api-reference)
7. [Customization Guide](#customization-guide)
8. [Troubleshooting](#troubleshooting)

## Overview

The Unity Narrative System is a flexible, event-driven framework for creating branching narrative experiences in Unity. It is designed around a powerful editor tool, the **Narrative Linker**, and uses ScriptableObjects for all content, making it highly designer-friendly.

### Key Features
- 🎯 **Visual Editor**: A multi-tab Narrative Linker for all content creation.
- 🤖 **AI-Powered Tools**: Generate text and images directly in the editor.
- 📦 **Data-Driven**: All content is stored in ScriptableObjects.
- 📅 **Day-Based Progression**: A core `SimulationManager` for time-based games.
- 💾 **Save/Load System**: Built-in JSON-based persistence.
- 🧩 **Modular Architecture**: Easily extend and integrate with existing game systems.

## System Requirements

- **Unity Version**: 2022.3 LTS or newer
- **Render Pipeline**: Universal Render Pipeline (URP) recommended
- **Required Unity Packages**: Input System, TextMeshPro, Unity UI

## Quick Start Integration

For a fast, 5-minute setup, please refer to the **[QUICK_START.md](QUICK_START.md)** guide.

## Step-by-Step Integration

### Phase 1: System Installation

#### Step 1: Copy Core Files
Copy the entire `SimulationGame` folder into your project's `Assets` directory.

#### Step 2: Verify Dependencies
Ensure the required packages (Input System, TextMeshPro, UGUI) are installed via the Unity Package Manager.

### Phase 2: Scene Configuration

#### Step 1: Create Main GameObject Hierarchy
```
NarrativeSystem (Empty GameObject)
├── GameController          (GameController component)
├── SimulationManager       (SimulationManager component)
├── EventSystem             (EventSystem component)
├── ChoiceEngine            (ChoiceEngine component)
├── UIManager               (UIManager component)
└── SaveManager             (SaveManager component)
```

#### Step 2: Configure GameController
In the `GameController` inspector, assign the manager components to their corresponding fields. For example, drag the `SimulationManager` GameObject to the `Simulation Source` field.

#### Step 3: Setup UIManager
1.  Create a Canvas and the necessary UI elements (Text for event titles/descriptions, a container for choice buttons, etc.).
2.  Assign the UI element references in the `UIManager` inspector.
3.  The provided sample prefabs in `Assets/SimulationGame/Prefabs/` can be used as a starting point.

### Phase 3: Content Creation

This is where the **Narrative Linker** becomes your primary tool. See the dedicated section below.

#### Step 1: Create the GameData Asset
1.  Right-click in the Project window: `Create > Narrative Framework > GameData`.
2.  Name the file `GameData.asset` and place it in `Assets/Resources/`. **This path is required.**

#### Step 2: Use the Narrative Linker
Open the editor via **`Tools > Narrative Framework > Narrative Linker`** to create all your content: events, choices, outcomes, actors, variables, and story arcs.

## The Narrative Linker

The Narrative Linker is the central hub for all narrative design. It replaces the need to manually create and link dozens of ScriptableObjects.

-   **Game Data Tab**: Configure global settings and see all project assets at a glance.
-   **New Event Tab**: Use the AI-powered wizard to create new events quickly.
-   **Event's Choices Tab**: The main workspace for editing events, choices, and outcomes.
-   **Other Tabs**: Dedicated editors for Actors, Variables, Story Arcs, and more.

Refer to the main **[README.md](README.md)** for a detailed breakdown of each tab.

## API Reference

For a detailed breakdown of all classes, interfaces, and methods, please see the **[API_REFERENCE.md](API_REFERENCE.md)** file.

### Key Interfaces

-   **`ISimulationManager`**: Controls time progression and core game state.
-   **`IEventSystem`**: Manages event generation and presentation.
-   **`IChoiceEngine`**: Processes player choices.
-   **`IUIManager`**: Handles UI display and interaction.

### Key Classes

-   **`GameController`**: The central orchestrator.
-   **`SimEventSO`**: Represents a single narrative event.
-   **`Choice`**: A player decision within an event.
-   **`Outcome`**: The consequence of a choice. Now supports multiple `OutcomeAction`s.
-   **`GameData`**: The central configuration asset.

## Customization Guide

### Creating a Custom UI

You can either extend the provided `UIManager` class or implement the `IUIManager` interface from scratch to integrate with your own UI system.

**Example: Extending UIManager**
```csharp
public class MyCustomUIManager : UIManager
{
    [SerializeField] private Animator characterAnimator;

    public override void DisplayEvent(SimEventSO evt)
    {
        base.DisplayEvent(evt);

        // Custom logic: trigger a character animation
        if (evt.InvolvedActors.Count > 0)
        {
            characterAnimator.SetTrigger(evt.InvolvedActors[0].ActorName + "_event");
        }
    }
}
```

### Creating Custom Outcomes

Create a new class that inherits from `BaseOutcome` and implement the `Apply` method.

```csharp
[CreateAssetMenu(fileName = "MyCustomOutcome", menuName = "Narrative Framework/Custom Outcomes/My Custom Outcome")]
public class MyCustomOutcome : BaseOutcome
{
    [SerializeField] private string myParameter;

    public override void Apply(ISimulationManager simulation)
    {
        // Your custom game logic here
        Debug.Log($"My custom outcome was applied with parameter: {myParameter}");
        // Example: simulation.AddFlag("custom_outcome_triggered");
    }
}
```

## Troubleshooting

For a list of common issues and solutions, please refer to the **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** guide.

-   **`GameData.asset` Not Found**: Ensure the asset is at `Assets/Resources/GameData.asset`.
-   **Null Reference Errors**: Check that all component references are assigned in the `GameController` inspector.
-   **Events Not Triggering**: Verify the `Starting Event` is set in `GameData` and that event conditions (day range, flags) are met.
