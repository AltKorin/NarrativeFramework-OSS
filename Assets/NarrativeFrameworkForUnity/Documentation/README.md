# 🎮 Narrative Framework

Welcome to the Narrative Framework for Unity! This package provides a complete, AI-enhanced system for building branching narrative simulations. It is built around designer-friendly ScriptableObject-based workflows and a powerful central editor, the **Narrative Linker**.

## 🌟 Core Features

- **Visual Narrative Management**: A multi-tab **Narrative Linker** window to manage all aspects of your story.
- **AI-Powered Content Creation**: Integrated OpenAI tools to generate event descriptions, choice text, and even illustrations directly within the editor.
- **Data-Driven Design**: All content (Events, Actors, Story Arcs, etc.) is stored in ScriptableObjects for easy editing and extension.
- **Day-Based Simulation**: A robust `SimulationManager` to handle day-based progression.
- **Branching Narratives**: Create complex stories with `SimEventSO`, `Choice`, and `Outcome` objects.
- **Advanced State Tracking**: Use `NarrativeVariableSO` (float, bool) and flags to manage complex game states.
- **Character & Relationships**: Define `ActorSO` characters and track their `Relationship` dynamics.
- **Story Arc System**: Weave events together into larger `StoryArcSO` narratives that can be triggered by game state.
- **Save/Load System**: A complete JSON-based persistence solution.

## 🖥 The Narrative Linker Window

The **Narrative Linker** is your central hub for creating and managing all narrative content. Access it from the Unity menu: **`Tools > Narrative Framework > Narrative Linker`**.

The window is organized into the following tabs:

- **Game Data**: Configure global settings, including campaign length, starting event, and lists of all core assets (Resources, Actors, Variables). Includes validation tools to find and fix issues.
- **🪄 New Event**: A dedicated wizard for creating new events, with options for both manual creation and one-click AI generation of the entire event, including text and a main illustration.
- **Event's Choices**: The primary editor for existing events. Here you can write event text, link choices, and define complex outcomes. It includes AI helpers to auto-fill descriptions and generate images.
- **Outcomes**: A project-wide browser and editor for all `Outcome` assets. Supports the new multi-action system, allowing a single outcome to trigger multiple effects.
- **Story Arcs**: Visually build and manage your `StoryArcSO` assets, linking events together to form larger plotlines.
- **Story Stacks**: A tool for managing and triggering sequences of events based on game state.
- **Variables**: Create and manage all `NarrativeVariableSO` assets in your project with a handy project-wide browser.
- **Actors**: Define your `ActorSO` characters and their `ActorTypeSO` templates (e.g., "Villager" with attributes like "Health", "Loyalty").
- **Relationships**: Define and edit the relationships between actors, including their strength and type.
- **Endings**: A powerful visualizer tool that traces your narrative from any starting event to all possible endings, helping you find dead-end branches or logic errors.
- **Event Tree**: A visual graph of all events and their connections.

## 🚀 Getting Started

1.  **Create GameData**: Right-click in your project `Assets` folder, then go to **`Create > Narrative Framework > GameData`**. Name the file `GameData` and place it in your `Assets/Resources/` folder. This is critical.
2.  **Open the Narrative Linker**: Go to **`Tools > Narrative Framework > Narrative Linker`**.
3.  **Configure OpenAI (Optional)**: To use the AI features, you must enter your OpenAI API key: select `Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset` in the Project window (it is created automatically on first AI use, or via `Assets > Create > Tools > NarrativeFramework > OpenAI Settings`) and enter your key in the Inspector.
4.  **Create Content**: Use the Narrative Linker tabs to create your game's content:
    - Start in the **Actors** and **Variables** tabs to define your characters and state variables.
    - Use the **New Event** tab to create your first few events. Try the AI generator!
    - Link them together using the **Event's Choices** tab.
5.  **Set the Starting Event**: In the **GameData** tab, assign your first event to the `Starting Event` field.
6.  **Set up a Scene**:
    - Create an empty GameObject and add the `GameController` component to it.
    - Add the other required manager components (like `SimulationManager`, `UIManager`, etc.) to child GameObjects.
    - Wire the references in the `GameController` inspector.
7.  **Hit Play!**

## 🛠 Key Components

-   **`GameData.asset`**: The central ScriptableObject that holds references to all your narrative content.
-   **`SimEventSO`**: A single narrative event. Contains the description, illustration, and a list of `Choice` objects.
-   **`Choice`**: A player decision. Contains the choice text and a list of `Outcome` objects.
-   **`Outcome`**: The result of a choice. Can modify resources, set flags, change relationships, trigger new events, and more. Outcomes can now contain a list of `OutcomeAction`s to create complex, probabilistic results.
-   **`ActorSO`**: A character, faction, or other entity in your story.
-   **`NarrativeVariableSO`**: A float or boolean variable for tracking game state.

## 🛑 Troubleshooting

-   **"GameData.asset not found"**: Make sure it's located at the exact path `Assets/Resources/GameData.asset`.
-   **AI features not working**: Ensure your OpenAI API key is correctly entered in `OpenAISettings.asset` (Inspector). Check your internet connection and OpenAI API status.
-   **No ActorTypes in dropdowns**: You must create at least one `ActorTypeSO` asset first using the **Actors** tab in the Narrative Linker.

---
Created by Konrad Komorowski. This package is designed for indie studios & solo devs building narrative-heavy simulations. We'd love your feedback and contributions! 🚀
