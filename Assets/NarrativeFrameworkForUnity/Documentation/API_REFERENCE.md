# Unity Narrative System - API Reference

## Table of Contents
1. [Core Interfaces](#core-interfaces)
2. [Manager Classes](#manager-classes)
3. [Data Classes (ScriptableObjects)](#data-classes-scriptableobjects)
4. [Editor (Narrative Linker)](#editor-narrative-linker)
5. [AI Content Generation](#ai-content-generation)
6. [Save System](#save-system)
7. [Extension Points](#extension-points)

## Core Interfaces

### ISimulationManager
Controls game time progression and core state management.

```csharp
public interface ISimulationManager
{
    int CurrentTimeUnit { get; }
    void AddFlag(string flag);
    void RemoveFlag(string flag);
    void ModifyResource(string resourceId, float amount);
    void TriggerEventById(string targetId);
    // ... other properties and methods
}
```

### IEventSystem
Manages event generation, presentation, and completion.

```csharp
public interface IEventSystem
{
    UnityEvent<SimEventSO> OnMainEventGenerated { get; }
    void GenerateMainEvent();
    void PresentEvent(SimEventSO evt);
    // ... other properties and methods
}
```

### IChoiceEngine
Processes player choices and generates outcomes.

```csharp
public interface IChoiceEngine
{
    UnityEvent<List<Choice>> OnChoicesGenerated { get; }
    SimEventSO ProcessChoice(Choice selectedChoice, ISimulationManager simulationManager);
    // ... other properties and methods
}
```

### IUIManager
Handles user interface display and interaction.

```csharp
public interface IUIManager
{
    event System.Action<Choice> OnChoiceSelected;
    void DisplayEvent(SimEventSO evt);
    void DisplayChoices(IList<Choice> choices);
    // ... other properties and methods
}
```

## Manager Classes

### GameController
Central coordinator that orchestrates all systems. Implements `ISaveDataProvider` and `ISaveDataReceiver`.

```csharp
public class GameController : MonoBehaviour
{
    // State Properties
    public Dictionary<string, float> Resources { get; }
    public IEnumerable<string> Flags { get; }
    public int CurrentDay { get; }

    // Narrative Variables
    public float GetNarrativeVariable(string id);
    public void SetNarrativeVariable(string id, float value);
    public bool GetNarrativeBool(string id);
    public void SetNarrativeBool(string id, bool value);

    // Core Methods
    public void StartGame();
    public void AdvanceDay();
}
```

## Data Classes (ScriptableObjects)

### GameData
The central configuration asset for the entire narrative.

```csharp
[CreateAssetMenu(fileName = "GameData", menuName = "Narrative Framework/GameData")]
public class GameData : ScriptableObject
{
    public int CampaignLength;
    public SimEventSO StartingEvent;
    public List<SimEventSO> Events;
    public List<StoryArcSO> StoryArcs;
    public List<ActorSO> Actors;
    public List<Resource> Resources;
    public List<NarrativeVariableSO> NarrativeVariables;
    // ... and other lists for all content types

    public static GameData Instance { get; } // Singleton access
    public int IncrementEventId(); // Used by editor tools
}
```

### SimEventSO
Represents a single narrative event.

```csharp
[CreateAssetMenu(fileName = "SimEvent", menuName = "Narrative Framework/Sim Event")]
public class SimEventSO : ScriptableObject
{
    public string simEventTitle;
    public string simEventDescription;
    public Sprite mainIllustration;
    public Sprite backgroundImage;
    public List<Choice> choices;
    public List<ActorSO> InvolvedActors;
    // ... other properties like probability, day range, flags
}
```

### Choice
Represents a player decision within an event.

```csharp
[CreateAssetMenu(fileName = "Choice", menuName = "Narrative Framework/Choice")]
public class Choice : ScriptableObject
{
    public string ChoiceText;
    public SimEventSO NextEvent; // Optional event to trigger directly
    public bool GoToNextDay;
    public List<Outcome> Outcomes;
    // ... condition properties like requiredFlags
}
```

### Outcome
Represents the consequences of a choice. Now supports a list of actions.

```csharp
[System.Serializable]
public class Outcome : ScriptableObject
{
    // Legacy single outcome fields (can still be used)
    public OutcomeType _type;
    public string _targetId;
    public float _value;

    // New multi-action system (overrides legacy fields if populated)
    public List<OutcomeAction> _actions;
}
```

### OutcomeAction
A single action within an `Outcome`. Has its own type, probability, and parameters.

```csharp
[System.Serializable]
public class OutcomeAction
{
    public OutcomeType _type;
    public float _probability = 1.0f;
    public string _targetId; // Fallback ID
    public float _value;
    public ResourceSO _targetResource;
    public NarrativeVariableSO _targetVariable;
    // ... other fields for different action types
}
```

### ActorSO & ActorTypeSO
Defines characters and their archetypes.

```csharp
// Defines a template for actors (e.g., "Villager")
public class ActorTypeSO : ScriptableObject
{
    public string DisplayName;
    public List<string> Attributes; // e.g., "Health", "Morale"
}

// Represents a specific character (e.g., "John the Farmer")
public class ActorSO : ScriptableObject
{
    public string ActorName;
    public ActorTypeSO ActorType;
    public List<AttributeKeyValuePair> Attributes;
}
```

### NarrativeVariableSO
Tracks a float or boolean value in the game state.

```csharp
public class NarrativeVariableSO : ScriptableObject
{
    public string VariableName;
    public enum VariableType { Float, Bool }
    public VariableType Type;
    public float InitialFloatValue;
    public bool InitialBoolValue;
}
```

## Editor (Narrative Linker)

The primary tool for content creation, accessed via **`Tools > Narrative Framework > Narrative Linker`**. The editor is composed of several view classes, each handling a specific tab.

-   `NarrativeLinkerWindow.cs`: The main editor window container.
-   `NarrativeLinkerView.cs`: The main view that holds all the tabs.
-   `NarrativeLinkerGameDataView.cs`: Edits the `GameData` asset.
-   `NarrativeLinkerNewEventView.cs`: AI-powered wizard for creating new events.
-   `NarrativeLinkerEventView.cs`: Edits existing events, choices, and outcomes.
-   `NarrativeLinkerActorView.cs`: Manages `ActorSO` and `ActorTypeSO` assets.
-   `NarrativeLinkerVariableView.cs`: Manages `NarrativeVariableSO` assets.
-   `NarrativeLinkerStoryArcView.cs`: Manages `StoryArcSO` assets.
-   `NarrativeLinkerEndingView.cs`: Visualizes narrative paths to find dead ends.
-   `NarrativeLinkerGUI.cs`: Shared IMGUI helper functions.

## AI Content Generation

The framework integrates with OpenAI to accelerate content creation.

### OpenAIGenerator.cs
Static class that handles all communication with the OpenAI API.

```csharp
namespace SimulationGame.OpenAI
{
    public static class OpenAIGenerator
    {
        // Generates text (e.g., event descriptions, choice text)
        public static async Task<string> GenerateEvent(string prompt);

        // Generates an image and returns a URL
        public static async Task<string> GenerateImage(string prompt);
    }
}
```

### OpenAISettings.cs
ScriptableObject holding the API key, stored in `Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset` (auto-created; edit its key in the Inspector).

## Save System

### AsyncSaveManager.cs
Handles saving and loading game data asynchronously to JSON files to prevent frame drops.

### ISaveDataProvider & ISaveDataReceiver
Interfaces implemented by `GameController` to provide data to the save system and receive data upon loading.

### SaveData.cs
The serializable class that holds all the game state to be saved.

```csharp
[System.Serializable]
public class SaveData
{
    public int Version;
    public int CurrentDay;
    public List<string> Flags;
    public Dictionary<string, float> Resources;
    public Dictionary<string, float> NarrativeVariables;
    public Dictionary<string, bool> NarrativeBooleans;
    // ... and other state information
}
```

## Extension Points

### Custom Outcomes
Create a new class inheriting from `BaseOutcome` to add your own game-specific logic.

```csharp
[CreateAssetMenu(menuName = "Game/Custom/StartMinigameOutcome")]
public class StartMinigameOutcome : BaseOutcome
{
    [SerializeField] private string minigameSceneName;

    public override void Apply(ISimulationManager simulation)
    {
        // Custom logic to start a minigame
        UnityEngine.SceneManagement.SceneManager.LoadScene(minigameSceneName);
    }
}
```
