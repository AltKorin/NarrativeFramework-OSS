# Unity Narrative System - Quick Start Guide

## 📋 Pre-Integration Checklist

### ✅ System Requirements
- [ ] Unity 2022.3 LTS or newer
- [ ] Universal Render Pipeline (URP)
- [ ] Input System
- [ ] TextMeshPro
- [ ] Unity UI (uGUI)

### ✅ Project Preparation
- [ ] Backup your project.
- [ ] Create a new scene for testing.
- [ ] Ensure a `Resources` folder exists in `Assets`.

### ✅ OpenAI API Key (Optional)
- [ ] If you plan to use the AI content generation features, have your OpenAI API key ready.

## 🚀 5-Minute Integration

### Step 1: Copy Files (1 minute)
Copy the entire `SimulationGame` folder into your project's `Assets` directory.

### Step 2: Scene Setup (2 minutes)
Create the following GameObject hierarchy in your test scene:
```
NarrativeSystem (Empty GameObject)
├── GameController          (GameController component)
├── SimulationManager       (SimulationManager component)
├── EventSystem             (EventSystem component)
├── ChoiceEngine            (ChoiceEngine component)
```

### Step 3: Wire Components (1 minute)
In the `GameController` inspector, assign the corresponding components to their respective fields (e.g., drag the `SimulationManager` GameObject to the `Simulation Source` field).

### Step 4: Create and Configure GameData (1 minute)
1.  **Open the Narrative Linker**: Go to **`Tools > Narrative Framework > Narrative Linker`**.
2.  **Select the "Game Data" Tab**: If a `GameData.asset` file is not found in `Assets/Resources/`, a button will appear.
3.  **Click "Create"**: A new `GameData.asset` will be created for you at the required path.
4.  **Configure**: Select the `GameData.asset` and set the `Starting Event` using the sample event provided in `Assets/SimulationGame/Sample/`.

### Step 5: Configure OpenAI Key (Optional)
1.  Create the settings asset via **`Assets > Create > Tools > NarrativeFramework > OpenAI Settings`** (or skip: it is created automatically at `Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset` on first AI use).
2.  Select the `OpenAISettings.asset` file.
3.  Enter your OpenAI API key in the inspector.

### Step 6: Test
- Press Play.
- Check the Console for "GameData.asset loaded successfully".
- The sample event should appear.

## 📊 Integration Validation Checklist

- [ ] `GameController` initializes without errors.
- [ ] `GameData.asset` loads from the `Resources` folder.
- [ ] The sample starting event displays correctly.
- [ ] Clicking a choice triggers an outcome and advances the game.
- [ ] The `Narrative Linker` window opens from `Tools > Narrative Framework > Narrative Linker`.

## 🔧 Key File Locations

-   **Core System**: `Assets/SimulationGame/Runtime/Scripts/`
-   **Editor Tools**: `Assets/SimulationGame/Editor/`
-   **Main Configuration**: `Assets/Resources/GameData.asset` (You must create this)
-   **Sample Content**: `Assets/SimulationGame/Sample/`

## 🎯 Quick Content Creation with the Narrative Linker

1.  **Open the Linker**: `Tools > Narrative Framework > Narrative Linker`.
2.  **Create an Actor**: Go to the `Actors` tab and create a new Actor Type and an Actor.
3.  **Create a Variable**: Go to the `Variables` tab and create a new Narrative Variable.
4.  **Create an Event**: Go to the `New Event` tab. Use the AI to generate a title and description, or write your own.
5.  **Add Choices**: In the `Event's Choices` tab, select your new event, add choices, and link outcomes.
6.  **Set Starting Event**: Drag your new event into the `Starting Event` field in the `GameData` tab.
7.  **Press Play!**

---

**✨ From zero to a working narrative system in under 5 minutes!**
