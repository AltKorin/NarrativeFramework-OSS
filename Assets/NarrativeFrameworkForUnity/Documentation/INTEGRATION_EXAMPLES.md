# Unity Narrative System - Integration Examples

## Table of Contents
1. [RPG Integration](#rpg-integration)
2. [Management Game Integration](#management-game-integration)
3. [Visual Novel Integration](#visual-novel-integration)
4. [Using the AI Content Tools](#using-the-ai-content-tools)

## RPG Integration

### Custom Outcome: Skill Check
This example shows a custom outcome that performs a skill check and triggers different results based on success or failure.

```csharp
// Create this script and then create an asset from it.
// Assets > Create > Narrative Framework > Custom Outcomes > Skill Check Outcome
[CreateAssetMenu(menuName = "RPG/Skill Check Outcome")]
public class SkillCheckOutcome : BaseOutcome
{
    [SerializeField] private string skillToCheck = "Strength";
    [SerializeField] private int difficultyClass = 12;
    [SerializeField] private SimEventSO successEvent;
    [SerializeField] private SimEventSO failureEvent;

    public override void Apply(ISimulationManager simulation)
    {
        // Assume you have a PlayerStats class that holds skill values
        int playerSkill = PlayerStats.Instance.GetSkill(skillToCheck);
        int roll = Random.Range(1, 21);

        if (roll + playerSkill >= difficultyClass)
        {
            simulation.Log($"Skill check {skillToCheck} succeeded!");
            if (successEvent != null) simulation.TriggerEventById(successEvent.simEventId);
        }
        else
        {
            simulation.Log($"Skill check {skillToCheck} failed.");
            if (failureEvent != null) simulation.TriggerEventById(failureEvent.simEventId);
        }
    }
}
```

### Event Setup in the Narrative Linker

1.  **Create the Custom Outcome**: Create the `SkillCheckOutcome` asset in your project.
2.  **Create an Event**: In the Narrative Linker, create an event (e.g., "Force open the rusty gate").
3.  **Create a Choice**: Add a choice like "Attempt to force the gate".
4.  **Create an Outcome**: Add an `Outcome` to the choice.
5.  **Configure the Outcome**:
    *   Set its `Type` to `CustomOutcome`.
    *   In the `Custom Logic` field, assign your `SkillCheckOutcome` asset.

### Multi-Action Outcome: Rewarding Loot
This example shows how to use the new multi-action system to give the player a guaranteed reward plus a chance for a rare item.

1.  **Create an Event**: "Search the ancient chest".
2.  **Create a Choice**: "Open it".
3.  **Create an Outcome**: Add a new `Outcome` to the choice.
4.  **Configure the Actions** in the `NarrativeLinkerEventView` or `NarrativeLinkerOutcomeView`:
    *   **Action 1 (Guaranteed Gold)**:
        *   **Type**: `ModifyResource`
        *   **Target Resource**: Your "Gold" `ResourceSO`.
        *   **Value**: 50
        *   **Probability**: 1.0 (100%)
    *   **Action 2 (Chance for Rare Sword)**:
        *   **Type**: `AddFlag`
        *   **Flags to Add**: A flag named `has_rare_sword`.
        *   **Probability**: 0.1 (10%)

## Management Game Integration

### Custom Outcome: Hire a New Employee

```csharp
[CreateAssetMenu(menuName = "Management/Hire Employee Outcome")]
public class HireEmployeeOutcome : BaseOutcome
{
    [SerializeField] private int hiringCost = 500;
    [SerializeField] private int employeeUpkeep = 50;

    public override void Apply(ISimulationManager simulation)
    {
        // Assume you have a resource for "Money" and "Employees"
        simulation.ModifyResource("Money", -hiringCost);
        simulation.ModifyResource("Employees", 1);

        // You could also add a recurring cost via another system
        // For example, by adding a flag that your economy manager checks daily
        simulation.AddFlag("new_hire_upkeep");
        simulation.Log($"Hired a new employee for {hiringCost}. Upkeep is {employeeUpkeep}.");
    }
}
```

### Event Setup

1.  **Create an Event**: "Job Fair".
2.  **Create a Choice**: "Hire a promising candidate".
3.  **Link the Outcome**: Assign your `HireEmployeeOutcome` asset to the choice.

## Visual Novel Integration

### Custom UIManager
Extend the `UIManager` to handle character portraits and expressions.

```csharp
public class VisualNovelUIManager : UIManager
{
    [SerializeField] private Image leftPortrait;
    [SerializeField] private Image rightPortrait;

    public override void DisplayEvent(SimEventSO evt)
    { 
        base.DisplayEvent(evt);

        // Assuming you have a way to know which actor is speaking
        // or involved. Here we just use the first two involved actors.
        if (evt.InvolvedActors.Count > 0 && evt.InvolvedActors[0].Portrait != null)
        {
            leftPortrait.sprite = evt.InvolvedActors[0].Portrait;
            leftPortrait.gameObject.SetActive(true);
        }
        else
        {
            leftPortrait.gameObject.SetActive(false);
        }

        if (evt.InvolvedActors.Count > 1 && evt.InvolvedActors[1].Portrait != null)
        {
            rightPortrait.sprite = evt.InvolvedActors[1].Portrait;
            rightPortrait.gameObject.SetActive(true);
        }
        else
        {
            rightPortrait.gameObject.SetActive(false);
        }
    }
}
```

### Actor Setup

1.  In the **Actors** tab of the Narrative Linker, create your characters.
2.  You would need to add a `Portrait` field to your `ActorSO` script to hold the sprite reference.

## Using the AI Content Tools

This example shows the workflow for creating an event entirely with AI.

### Step 1: Configure OpenAI
- Select `Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset` (auto-created on first AI use, or `Assets > Create > Tools > NarrativeFramework > OpenAI Settings`).
- Enter your OpenAI API Key in the Inspector.

### Step 2: Use the New Event Wizard
1.  Open the **Narrative Linker** (`Tools > Narrative Framework > Narrative Linker`).
2.  Go to the **🪄 New Event** tab.
3.  **Write Prompts**:
    *   **AI Prompt (Description)**: `A traveler discovers a hidden, glowing cave behind a waterfall.`
    *   **AI Prompt (Illustration)**: `A hidden, glowing cave behind a waterfall, fantasy, digital art.`
4.  **Click `✨ One-Click Generate Full Event`**.

### Step 3: Review and Refine
- The system will generate a new `SimEventSO` asset.
- It will automatically fill in the **Title** and **Description** based on your prompt.
- It will generate an **Illustration** and assign it to the `mainIllustration` field.
- A default "Proceed" choice is created.
- The new event will be selected in the **Event's Choices** tab.

### Step 4: Use AI to Refine Choices and Outcomes
1.  In the **Event's Choices** tab, select the newly created event.
2.  Select the default "Proceed" choice.
3.  Click **`✨ Auto-fill Choice Text`** to get a more descriptive choice label.
4.  Add a new `Outcome` to the choice.
5.  Select the new `Outcome` and click **`✨ Auto-fill Description`** to get a narrative description for what happens.
