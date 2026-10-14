# Unity Narrative System - Troubleshooting Guide

## Table of Contents
1. [Installation & Setup Issues](#installation--setup-issues)
2. [Narrative Linker Editor Issues](#narrative-linker-editor-issues)
3. [Runtime Errors](#runtime-errors)
4. [Content & Logic Problems](#content--logic-problems)
5. [AI Content Generation](#ai-content-generation)
6. [Save/Load Issues](#saveload-issues)

## Installation & Setup Issues

### ❌ Problem: `GameData.asset` not found
**Error Message**: `GameData.asset not found in Resources folder!`

**Solution**: The `GameData.asset` file **must** be located at the exact path `Assets/Resources/GameData.asset`. 
- Create the `Resources` folder if it doesn't exist.
- Create the `GameData` asset via `Assets > Create > Narrative Framework > GameData` and place it there.

### ❌ Problem: Missing package dependencies
**Error Message**: `The type or namespace name 'TextMeshPro' could not be found` (or similar for Input System, URP).

**Solution**: Open the Unity Package Manager (`Window > Package Manager`) and ensure the following packages are installed:
- `Input System`
- `TextMeshPro`
- `Unity UI`

## Narrative Linker Editor Issues

### ❌ Problem: The Narrative Linker window is blank or throwing errors.
**Solution**: 
1.  **Re-open the window**: Close and re-open it from `Tools > Narrative Framework > Narrative Linker`.
2.  **Check for console errors**: An error in one of the view classes can stop the entire window from rendering. Address any reported compilation errors.
3.  **Check `GameData.asset`**: Ensure your `GameData.asset` is not corrupted and is assigned in the project settings if required.

### ❌ Problem: Dropdowns for Actors, Variables, etc., are empty.
**Solution**: You must create the content first. For example, to see an actor in a dropdown, you must first create an `ActorTypeSO` and then an `ActorSO` in the **Actors** tab.

### ❌ Problem: Renaming an asset in the linker doesn't work.
**Solution**: The linker renames the underlying asset file. Ensure you don't have file permission issues and that the asset is not locked by another process. Click the "Apply Rename" button after typing the new name.

## Runtime Errors

### ❌ Problem: `NullReferenceException` in `GameController`
**Error Message**: `NullReferenceException: Object reference not set to an instance of an object`

**Solution**: This almost always means a component reference is missing in the `GameController` inspector. Drag and drop each manager component (`SimulationManager`, `UIManager`, etc.) from the scene hierarchy into its corresponding field on the `GameController`.

### ❌ Problem: Game doesn't start; no event appears.
**Solution**:
1.  **Check `Starting Event`**: Ensure you have assigned a `SimEventSO` to the `Starting Event` field in your `GameData.asset`.
2.  **Check Event Conditions**: Make sure the starting event's conditions (Min/Max Day, Required Flags) allow it to be triggered on Day 1.
3.  **Check Console**: Look for any errors during initialization.

## Content & Logic Problems

### ❌ Problem: An event choice leads to a dead end.
**Solution**: Use the **Endings** tab in the Narrative Linker. Select your starting event (or any event) as the `Root Event`. The visualizer will show you all narrative paths. Any path that doesn't terminate in an `Ending` event or a `GoToNextDay` choice is a potential dead end.

### ❌ Problem: An outcome isn't working as expected.
**Solution**:
1.  **Check the Outcome Type**: Ensure you've selected the correct `OutcomeType`.
2.  **Check the Parameters**: Verify that the `Target Resource`, `Target Variable`, or other fields are assigned correctly.
3.  **Multi-Action vs. Legacy**: If the `_actions` list in an `Outcome` has any entries, it will be used instead of the legacy single-outcome fields. Make sure you are editing the correct section.
4.  **Check Probability**: Ensure the probability of the `Outcome` or `OutcomeAction` is greater than 0.

### ❌ Problem: A Story Arc doesn't trigger.
**Solution**: In the **Story Arcs** tab, select your arc and carefully check the **Conditions** foldout. 
-   Are the `Required Flags` being set correctly in the game?
-   Are any `Forbidden Flags` present?
-   Has a `Required Event` been completed?

## AI Content Generation

### ❌ Problem: AI buttons are disabled or throw errors.
**Solution**:
1.  **Check API Key**: Select `Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset` and check that your OpenAI API key is entered correctly and has no leading/trailing spaces.
2.  **Check Internet Connection**: The tool requires an active internet connection to reach the OpenAI API.
3.  **Check OpenAI API Status**: Visit the [OpenAI status page](https://status.openai.com/) to see if there are any ongoing incidents.
4.  **Check Billing**: Ensure your OpenAI account has available credits or a valid payment method.

### ❌ Problem: Generated images don't load.
**Solution**:
1.  **Check Console for Errors**: Look for any errors related to downloading or importing the image.
2.  **Permissions**: Ensure your project has permission to write files to the `Assets/SimulationGame/Data/Illustrations` folder.

## Save/Load Issues

### ❌ Problem: Game state is not saved or loaded correctly.
**Solution**:
1.  **Check `ISaveDataProvider`**: Ensure your `GameController` (or equivalent) correctly implements `ISaveDataProvider` and provides all necessary data.
2.  **Check `ISaveDataReceiver`**: Ensure the `ApplySaveData` method correctly restores the game state.
3.  **Serialization Issues**: Make sure any custom data you want to save is marked with `[System.Serializable]` and that dictionaries are handled correctly (Unity's default JSON utility doesn't serialize dictionaries directly).
4.  **File Permissions**: Check that your game has permission to write save files to the persistent data path.
