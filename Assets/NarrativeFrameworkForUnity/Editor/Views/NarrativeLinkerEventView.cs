// Assets/SimulationGame/Editor/Views/NarrativeLinkerEventView.cs
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Threading.Tasks;
using System.IO;
using SimulationGame.OpenAI;
using System.Collections.Generic;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Editor-side helper for browsing & wiring narrative data (events ⇄ choices ⇄ outcomes).
    /// Supports assigning sequential simEventId from GameData and duplicating events with a fresh ID.
    /// Hardened with NarrativeLinkerGUI helpers to avoid IMGUI layout errors.
    /// </summary>
    public class NarrativeLinkerEventView
    {
        /* ────────────────────────────────────────────
         * Runtime-state fields
         * ──────────────────────────────────────────── */
        private SimEventSO selectedEvent;
        private Choice     selectedChoice;

        private Vector2    _scroll;

        /* Prompts used by AI helpers */
        private string generationPrompt      = "Describe a cold and mysterious narrative event in a survival game.";
        private string generationResult      = "";
        private string illustrationPrompt    = "A lonely hut in a snowy forest";
        private string backgroundImagePrompt = "A snowy forest panorama, subtle blue tones";

        // Canonical paths (kept consistent with NewEventView)
        private const string ModuleRoot        = "Assets/SimulationGame";
        private const string ResourcesDir      = ModuleRoot + "/Resources";
        private const string GameDataAssetPath = ResourcesDir + "/GameData.asset";
        private const string EventsDir         = ModuleRoot + "/Data/Events";
        private const string IllustrationsDir  = ModuleRoot + "/Data/Illustrations";

        private GameData _gameData; // resolved lazily so domain reloads are safe
        private bool _initialized; 

        // ────────────────────────────────────────────
        // Popup state for event list from GameData
        // ────────────────────────────────────────────
        private int _eventPopupIndex = -1;

// GameData Manipulation
        public void OnEnable()
        {
            if (_initialized) return;
            _gameData = NarrativeLinkerContext.GetOrResolve();
            NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
            _initialized = true;
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gameData, gd)) return;
            _gameData = gd;

            // (opcjonalnie) jeśli trzymasz indeks popupu z listy eventów,
            // możesz go tu przeliczyć względem _gameData.Events.

            InternalEditorUtility.RepaintAllViews();
        }

        /* ────────────────────────────────────────────
         * Main GUI
         * ──────────────────────────────────────────── */
        public void Draw()
        {
            // keep GameData hot
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
                OnActiveGameDataChanged(current);
            if (_gameData == null)
                _gameData = EnsureGameDataAsset(_gameData); 


            NarrativeLinkerGUI.Scroll(ref _scroll, () =>
            {
                GUILayout.Label("📜  Narrative Linker", NarrativeLinkerGUI.Header);

                DrawEventSelector();

                if (selectedEvent != null)
                {
                    DrawEventToolbarAndIdRow();   // ID + Duplicate with fresh ID + Ping
                    DrawEventDetails();
                    DrawChoiceList();

                    if (selectedChoice != null)
                        DrawOutcomeList();
                }
            });
        }

        /* ────────────────────────────────────────────
         * Event selection
         * ──────────────────────────────────────────── */
        private void DrawEventSelector()
        {
            EditorGUILayout.Space();

            // Optional popup of events from GameData
            if (_gameData != null && _gameData.Events != null && _gameData.Events.Count > 0)
            {
                // Build names once per draw
                var names = new string[_gameData.Events.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    var ev = _gameData.Events[i];
                    names[i] = string.IsNullOrEmpty(ev.simEventTitle) ? ev.name : ev.simEventTitle;
                }

                int newIndex = EditorGUILayout.Popup("Event List", _eventPopupIndex, names);
                if (newIndex != _eventPopupIndex)
                {
                    _eventPopupIndex = newIndex;
                    if (_eventPopupIndex >= 0 && _eventPopupIndex < _gameData.Events.Count)
                        selectedEvent = _gameData.Events[_eventPopupIndex];
                }
            }

            // Label is "Event" when nothing selected, otherwise keep "Start Event"
            string objectFieldLabel = (selectedEvent == null) ? "Event" : "Start Event";
            selectedEvent = (SimEventSO)EditorGUILayout.ObjectField(
                objectFieldLabel,
                selectedEvent,
                typeof(SimEventSO),
                false);

            // Keep popup in sync when user picks via ObjectField
            if (selectedEvent != null && _gameData != null && _gameData.Events != null)
            {
                int idx = -1;
                for (int i = 0; i < _gameData.Events.Count; i++)
                    if (_gameData.Events[i] == selectedEvent) { idx = i; break; }
                _eventPopupIndex = idx;
            }
            else if (selectedEvent == null)
            {
                _eventPopupIndex = -1;
            }
        }

        /// <summary>
        /// Small header toolbar to manage ID & quick actions.
        /// </summary>
        private void DrawEventToolbarAndIdRow()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // ID row (read-only) + reassign
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(new GUIContent("simEventId"), selectedEvent.simEventId ?? "<null>");
                    }

                    if (GUILayout.Button("Reassign ID", GUILayout.Width(110)))
                    {
                        AssignNewIdFromGameData(selectedEvent);
                    }
                }

                // Actions row
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Duplicate Event"))
                    {
                        DuplicateSelectedEventWithFreshId();
                    }

                    if (GUILayout.Button("Ping Asset", GUILayout.Width(100)))
                    {
                        EditorGUIUtility.PingObject(selectedEvent);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawEventDetails()
        {
            EditorGUILayout.Space();
            var soEvent = new SerializedObject(selectedEvent);
            soEvent.Update();

            // Basic fields (tolerant to name changes if needed -> keep canonical first)
            EditorGUILayout.PropertyField(soEvent.FindProperty("Title")               ?? soEvent.FindProperty("simEventTitle"),       new GUIContent("Title"));
            EditorGUILayout.PropertyField(soEvent.FindProperty("Description")         ?? soEvent.FindProperty("simEventDescription"), new GUIContent("Description"));
            EditorGUILayout.PropertyField(soEvent.FindProperty("simEventTitle"),       new GUIContent("Event Title"));
            EditorGUILayout.PropertyField(soEvent.FindProperty("simEventDescription"), new GUIContent("Event Description"));

            // Visual assets
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visual Elements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(soEvent.FindProperty("mainIllustration"), new GUIContent("Main Illustration"));
            EditorGUILayout.PropertyField(soEvent.FindProperty("backgroundImage"),  new GUIContent("Background Image"));
            EditorGUILayout.PropertyField(soEvent.FindProperty("ambientSound"),     new GUIContent("Ambient Sound"));

            // AI helpers
            EditorGUILayout.Space();
            DrawAIGenerators();

            soEvent.ApplyModifiedProperties();
        }

        /* ────────────────────────────────────────────
         * AI helper section
         * ──────────────────────────────────────────── */
        private void DrawAIGenerators()
        {
            /* --- Event text */
            EditorGUILayout.LabelField("AI Event Generator", EditorStyles.boldLabel);
            generationPrompt = EditorGUILayout.TextField("Prompt", generationPrompt);

            if (GUILayout.Button("🔮 Generate Description"))
                _ = GenerateDescriptionAsync(generationPrompt);

            EditorGUILayout.HelpBox(generationResult, MessageType.None);

            /* --- Main illustration */
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI Illustration Generator", EditorStyles.boldLabel);
            illustrationPrompt = EditorGUILayout.TextField("Main Illustration Prompt", illustrationPrompt);

            if (GUILayout.Button("🎨 Generate Illustration"))
                _ = GenerateImageAsync(illustrationPrompt, false);

            /* --- Background sprite */
            EditorGUILayout.Space(3);
            backgroundImagePrompt = EditorGUILayout.TextField("Background Image Prompt", backgroundImagePrompt);

            if (GUILayout.Button("🌄 Generate Background Image"))
                _ = GenerateImageAsync(backgroundImagePrompt, true);
        }

        /* ────────────────────────────────────────────
         * Choice list
         * ──────────────────────────────────────────── */
        private void DrawChoiceList()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

            // rich-text-enabled button style (once per call)
            GUIStyle richBtn = new GUIStyle(EditorStyles.miniButton)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 6, 2, 2)
            };

            for (int i = 0; i < selectedEvent.Choices.Count; i++)
            {
                var choice = selectedEvent.Choices[i];
                if (choice == null) continue;

                EditorGUILayout.BeginHorizontal();

                // caption
                string caption = choice.ChoiceText;
                int nOut = choice.Outcomes != null ? choice.Outcomes.Count : 0;

                if (nOut == 0) caption += $"  <color=#FF5555>(0)</color>";
                else caption += $"  <color=#AAAAAA>({nOut})</color>";

                if (choice.GoToNextDay)
                    caption += "  <color=#FFA500><b>Ends Day</b></color>";

                if (GUILayout.Button(caption, richBtn, GUILayout.MinWidth(140)))
                    selectedChoice = choice;

                GUILayout.Label(GetChoiceTargetLabel(choice), EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                // delete
                if (GUILayout.Button("🗑", GUILayout.Width(30)))
                {
                    DeleteChoiceAt(i);
                    EditorGUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();
                    return;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("➕ Add New Choice"))
            {
                CreateNewChoice();
            }
        }

        private void CreateNewChoice()
        {
            var newChoice = ScriptableObject.CreateInstance<Choice>();
            newChoice.name       = $"Choice_{selectedEvent.Choices.Count}";
            newChoice.ChoiceText = "New Choice Text";

            Undo.RegisterCreatedObjectUndo(newChoice, "Create Choice");
            Undo.RecordObject(selectedEvent, "Add Choice");

            AssetDatabase.AddObjectToAsset(newChoice, selectedEvent);
            selectedEvent.Choices.Add(newChoice);

            EditorUtility.SetDirty(selectedEvent);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DeleteChoiceAt(int index)
        {
            if (index < 0 || index >= selectedEvent.Choices.Count) return;

            Choice choiceToRemove = selectedEvent.Choices[index];

            Undo.RecordObject(selectedEvent, "Remove Choice");
            selectedEvent.Choices.RemoveAt(index);
            if (selectedChoice == choiceToRemove) selectedChoice = null;

            // Use Undo so designer can Cmd+Z the deletion
            Undo.DestroyObjectImmediate(choiceToRemove);

            EditorUtility.SetDirty(selectedEvent);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private string GetChoiceTargetLabel(Choice choice)
        {
            if (choice == null) return "";
            if (choice.NextEvent != null) {
                var t = string.IsNullOrEmpty(choice.NextEvent.simEventTitle)
                    ? choice.NextEvent.name
                    : choice.NextEvent.simEventTitle;
                return $"→ {t}";
            }
            if (choice.GoToNextDay)       return "→ Ends Day";
            return "→ Ends Event";
        }

        /* ────────────────────────────────────────────
         * Outcome editing
         * ──────────────────────────────────────────── */
        private void DrawOutcomeList()
        {
            if (selectedChoice == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editing Outcomes for Choice:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"\"{selectedChoice.ChoiceText}\"", EditorStyles.helpBox);

            // Core choice fields
            var soChoice = new SerializedObject(selectedChoice);
            soChoice.Update();

            EditorGUILayout.PropertyField(soChoice.FindProperty("_choiceText"),  new GUIContent("Choice Text"));
            EditorGUILayout.PropertyField(soChoice.FindProperty("_nextEvent"),   new GUIContent("Next Event"));
            EditorGUILayout.PropertyField(soChoice.FindProperty("_goToNextDay"), new GUIContent("Go To Next Day"));

            if (GUILayout.Button("✨ Auto-fill Choice Text"))
                _ = GenerateChoiceTextAsync(selectedChoice);
            soChoice.ApplyModifiedProperties();

            // Protect against null list
            if (selectedChoice.Outcomes == null)
            {
                var soFix = new SerializedObject(selectedChoice);
                soFix.Update();
                var f = soFix.FindProperty("_outcomes");
                if (f != null && f.isArray) f.arraySize = 0;
                soFix.ApplyModifiedProperties();
            }

            // Outcome cards
            for (int i = 0; i < selectedChoice.Outcomes.Count; i++)
            {
                Outcome outcome = selectedChoice.Outcomes[i];
                if (outcome == null) continue;

                bool deleteFlag = false;

                // styled header strip
                OutcomeStyle style = OutcomeStyleMap(outcome.Type);
                Rect vbox = EditorGUILayout.BeginVertical("box");
                EditorGUI.DrawRect(new Rect(vbox.x, vbox.y, vbox.width, 18), style.color * 1.3f);
                GUILayout.Space(2);
                GUILayout.Label($"{style.icon}  {outcome.Type}", EditorStyles.boldLabel);

                var soOut = new SerializedObject(outcome);
                soOut.Update();

                // Type
                EditorGUILayout.PropertyField(soOut.FindProperty("_type"), new GUIContent("Type"));
                OutcomeType type = (OutcomeType)soOut.FindProperty("_type").enumValueIndex;

                // Fields per outcome type
                switch (type)
                {
                    case OutcomeType.ModifyResource:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetResource"), new GUIContent("Target Resource"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_value"),          new GUIContent("Value"));
                        break;

                    case OutcomeType.AddFlag:
                    case OutcomeType.RemoveFlag:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_flagsToAdd"),    new GUIContent("Flags To Add"),    true);
                        EditorGUILayout.PropertyField(soOut.FindProperty("_flagsToRemove"), new GUIContent("Flags To Remove"), true);
                        break;

                    case OutcomeType.TriggerEvent:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEvent"),  new GUIContent("Target Event (SO)"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Target Event Id (fallback)"));
                        break;

                    case OutcomeType.SetRelationship:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_sourceActor"), new GUIContent("Source Actor"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetActor"), new GUIContent("Target Actor"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_value"),       new GUIContent("Delta"));
                        break;

                    case OutcomeType.SetPlayerActor:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetActor"), new GUIContent("Player Actor"));
                        break;

                    case OutcomeType.TriggerStoryArc:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_storyArc"),      new GUIContent("Story Arc"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Arc Id (fallback)"));
                        break;

                    // ── narrative variables ─────────────────────────────────────────
                    case OutcomeType.ModifyNarrativeVariable:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetVariable"), new GUIContent("Target Variable"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_value"),          new GUIContent("Delta / Value"));
                        break;

                    case OutcomeType.SetNarrativeBoolTrue:
                    case OutcomeType.SetNarrativeBoolFalse:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetVariable"), new GUIContent("Target Variable (bool)"));
                        break;

                    // ── achievements & features ─────────────────────────────────────
                    case OutcomeType.AwardAchievement:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Achievement Id"));
                        break;

                    case OutcomeType.UnlockFeature:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Feature Id"));
                        break;

                    // ── custom logic hook ───────────────────────────────────────────
                    case OutcomeType.CustomOutcome:
                        EditorGUILayout.PropertyField(soOut.FindProperty("_customLogic"),   new GUIContent("Custom Logic (BaseOutcome)"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Custom Outcome Id (fallback)"));
                        break;

                    default:
                        // Fallback
                        EditorGUILayout.PropertyField(soOut.FindProperty("_targetEventId"), new GUIContent("Target Id / Flag"));
                        EditorGUILayout.PropertyField(soOut.FindProperty("_value"),         new GUIContent("Value"));
                        break;
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Actions (optional)", EditorStyles.boldLabel);

                // Actions array (OutcomeAction list)
                SerializedProperty actions = soOut.FindProperty("_actions");
                if (actions != null)
                {
                    for (int ai = 0; ai < actions.arraySize; ai++)
                    {
                        var act = actions.GetArrayElementAtIndex(ai);
                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            var pType = act.FindPropertyRelative("_type");
                            EditorGUILayout.PropertyField(pType, new GUIContent("Type"));

                            // Per-action probability (0..100%)
                            var pProb = act.FindPropertyRelative("_probability");
                            if (pProb == null)
                            {
                                // initialize missing field (protection against old saves)
                                act.FindPropertyRelative("_probability").floatValue = 1f;
                                pProb = act.FindPropertyRelative("_probability");
                            }
                            float ap = pProb.floatValue * 100f;
                            ap = EditorGUILayout.Slider("Action Probability", ap, 0f, 100f);
                            pProb.floatValue = ap / 100f;

                            // Fields per action type
                            switch ((OutcomeType)pType.enumValueIndex)
                            {
                                case OutcomeType.ModifyResource:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetResource"), new GUIContent("Target Resource"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_value"),          new GUIContent("Value"));
                                    break;

                                case OutcomeType.AddFlag:
                                case OutcomeType.RemoveFlag:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_flagsToAdd"),    new GUIContent("Flags To Add"), true);
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_flagsToRemove"), new GUIContent("Flags To Remove"), true);
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),      new GUIContent("Flag Id (optional)"));
                                    break;

                                case OutcomeType.TriggerEvent:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetEvent"),   new GUIContent("Target Event (SO)"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),      new GUIContent("Target Event Id (fallback)"));
                                    break;

                                case OutcomeType.TriggerStoryArc:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_storyArc"),      new GUIContent("Story Arc"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),      new GUIContent("Arc Id (fallback)"));
                                    break;

                                case OutcomeType.SetRelationship:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_sourceActor"), new GUIContent("Source Actor"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetActor"), new GUIContent("Target Actor"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_value"),       new GUIContent("Delta"));
                                    break;

                                case OutcomeType.SetPlayerActor:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetActor"), new GUIContent("Player Actor"));
                                    break;

                                case OutcomeType.ModifyNarrativeVariable:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetVariable"), new GUIContent("Target Variable"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_value"),          new GUIContent("Delta / Value"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),       new GUIContent("Variable Id (fallback)"));
                                    break;

                                case OutcomeType.SetNarrativeBoolTrue:
                                case OutcomeType.SetNarrativeBoolFalse:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetVariable"), new GUIContent("Target Variable (bool)"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),       new GUIContent("Variable Id (fallback)"));
                                    break;

                                case OutcomeType.AwardAchievement:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"), new GUIContent("Achievement Id"));
                                    break;

                                case OutcomeType.UnlockFeature:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"), new GUIContent("Feature Id"));
                                    break;

                                case OutcomeType.CustomOutcome:
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_customLogic"),   new GUIContent("Custom Logic (BaseOutcome)"));
                                    EditorGUILayout.PropertyField(act.FindPropertyRelative("_targetId"),      new GUIContent("Custom Outcome Id (fallback)"));
                                    break;
                            }

                            EditorGUILayout.Space(2);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("🗑 Remove Action", GUILayout.Width(130)))
                                {
                                    actions.DeleteArrayElementAtIndex(ai);
                                    soOut.ApplyModifiedProperties();
                                    GUIUtility.ExitGUI();
                                }
                            }
                        }
                    }

                    if (GUILayout.Button("➕ Add Action"))
                    {
                        int idx = actions.arraySize;
                        actions.InsertArrayElementAtIndex(idx);
                        var act = actions.GetArrayElementAtIndex(idx);

                        // Reset brand-new element (Unity duplicates the previous one)
                        act.FindPropertyRelative("_type").enumValueIndex = (int)OutcomeType.ModifyResource;
                        act.FindPropertyRelative("_probability").floatValue = 1f;
                        act.FindPropertyRelative("_value").floatValue = 0f;
                        act.FindPropertyRelative("_targetId").stringValue = "";

                        act.FindPropertyRelative("_targetEvent").objectReferenceValue = null;
                        act.FindPropertyRelative("_targetResource").objectReferenceValue = null;
                        act.FindPropertyRelative("_targetVariable").objectReferenceValue = null;
                        act.FindPropertyRelative("_sourceActor").objectReferenceValue = null;
                        act.FindPropertyRelative("_targetActor").objectReferenceValue = null;
                        act.FindPropertyRelative("_storyArc").objectReferenceValue = null;
                        act.FindPropertyRelative("_customLogic").objectReferenceValue = null;

                        var fAdd = act.FindPropertyRelative("_flagsToAdd");
                        if (fAdd != null) fAdd.ClearArray();
                        var fRem = act.FindPropertyRelative("_flagsToRemove");
                        if (fRem != null) fRem.ClearArray();

                        soOut.ApplyModifiedProperties();
                    }
                }

                // description + AI helper
                EditorGUILayout.PropertyField(soOut.FindProperty("_outcomeDescription"), new GUIContent("Outcome Description"));
                if (GUILayout.Button("✨ Auto-fill Description"))
                    _ = GenerateOutcomeDescriptionAsync(outcome);

                // Probability slider (0-100 %)
                SerializedProperty p = soOut.FindProperty("_probability");
                if (p != null)
                {
                    float percent = p.floatValue * 100f;
                    percent = EditorGUILayout.Slider("Probability", percent, 0, 100);
                    p.floatValue = percent / 100f;
                }

                soOut.ApplyModifiedProperties();

                if (GUILayout.Button("🗑 Delete Outcome")) deleteFlag = true;
                EditorGUILayout.EndVertical();

                // deletion with Undo
                if (deleteFlag)
                {
                    Outcome outcomeToRemove = selectedChoice.Outcomes[i];

                    Undo.RecordObject(selectedChoice, "Remove Outcome");

                    List<Outcome> listCopy = new List<Outcome>(selectedChoice.Outcomes);
                    listCopy.RemoveAt(i);
                    var field = selectedChoice.GetType().GetField("_outcomes",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                        field.SetValue(selectedChoice, listCopy);

                    Undo.DestroyObjectImmediate(outcomeToRemove);

                    EditorUtility.SetDirty(selectedChoice);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            // Add outcome
            if (GUILayout.Button("Add Outcome"))
            {
                Outcome newOutcome = ScriptableObject.CreateInstance<Outcome>();
                newOutcome.name = $"Outcome_{(selectedChoice.Outcomes != null ? selectedChoice.Outcomes.Count : 0)}";
                newOutcome.Initialize(OutcomeType.ModifyResource, "", 0f, 1f, "");

                AssetDatabase.AddObjectToAsset(newOutcome, selectedEvent);

                List<Outcome> listCopy = new List<Outcome>(selectedChoice.Outcomes ?? new List<Outcome>()) { newOutcome };

                var field = selectedChoice.GetType().GetField("_outcomes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(selectedChoice, listCopy);

                EditorUtility.SetDirty(selectedChoice);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        /* ────────────────────────────────────────────
         * AI helper methods
         * ──────────────────────────────────────────── */
        private async Task GenerateDescriptionAsync(string prompt)
        {
            string sysPrompt = $"Generate JSON with keys 'title' and 'description'. " +
                               $"Example: {{\"title\":\"Snowfall\",\"description\":\"A blizzard hits the camp...\"}} " +
                               $"Prompt: {prompt}";

            var response = await OpenAIGenerator.GenerateEvent(sysPrompt);
            if (string.IsNullOrEmpty(response)) return;

            string json = ExtractJsonPayload(response);
            var parsed  = JsonUtility.FromJson<OpenAIEventResponse>(json);
            if (parsed == null) return;

            OpenAIDescriptionPopup.Show(
                selectedEvent,
                parsed.title,
                parsed.description,
                
                () =>
                {
                    EditorUtility.SetDirty(selectedEvent);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                });
        }

        /// <summary>Request an OpenAI image and assign it to mainIllustration or backgroundImage.</summary>
        private async Task GenerateImageAsync(string prompt, bool toBackground)
        {
            var imageUrl = await OpenAIGenerator.GenerateImage(prompt);
            if (string.IsNullOrEmpty(imageUrl))
            {
                Debug.LogError("❌ Failed to generate image.");
                return;
            }

            using var client = new System.Net.WebClient();
            byte[] data = client.DownloadData(imageUrl);

            Directory.CreateDirectory(IllustrationsDir);

            string assetPath = $"{IllustrationsDir}/generated_{System.Guid.NewGuid()}.png";
            File.WriteAllBytes(assetPath, data);
            AssetDatabase.ImportAsset(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogError("❌ Sprite failed to load.");
                return;
            }

            if (toBackground) selectedEvent.backgroundImage = sprite;
            else               selectedEvent.mainIllustration = sprite;

            EditorUtility.SetDirty(selectedEvent);
            AssetDatabase.SaveAssets();
            Debug.Log("🖼 Image sprite assigned.");
        }

        /// <summary>Generates a concise Outcome description and writes it to the asset.</summary>
        private async Task GenerateOutcomeDescriptionAsync(Outcome outcome)
        {
            string prompt =
                $"Write ONE short sentence that describes this outcome in a narrative game. " +
                $"Outcome type: {outcome.Type}. " +
                $"If applicable, target id: '{outcome.TargetId}'. " +
                $"Do NOT add any quotation marks.";

            string description = await OpenAIGenerator.GenerateEvent(prompt);
            if (string.IsNullOrWhiteSpace(description)) return;

            description = description.Trim().Replace("\n", " ").Replace("\r", " ");
            if (description.Length > 200) description = description.Substring(0, 200);

            var so = new SerializedObject(outcome);
            so.Update();
            so.FindProperty("_outcomeDescription").stringValue = description;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(outcome);
            AssetDatabase.SaveAssets();
            Debug.Log("💬 Outcome description filled.");
        }

        /// <summary>
        /// Generates a short, player-facing label for the given choice and stores it in ChoiceText.
        /// </summary>
        private async Task GenerateChoiceTextAsync(Choice choice)
        {
            string prompt =
                "Write ONE short line the player could click in a narrative game. " +
                "Return ONLY that line. Do NOT wrap it in JSON or any extra words.";

            string text = await OpenAIGenerator.GenerateEvent(prompt);
            if (string.IsNullOrWhiteSpace(text)) return;

            // unwrap common wrappers
            string Unwrap(string s)
            {
                int colon = s.IndexOf("\":\"");
                if (s.StartsWith("{") && colon > 0 && s.EndsWith("}"))
                {
                    int start = colon + 3;
                    int end = s.LastIndexOf('"');
                    return s.Substring(start, end - start);
                }
                if (colon > 0)
                {
                    int start = colon + 3;
                    int end = s.IndexOf('"', start);
                    if (end > start) return s.Substring(start, end - start);
                }
                return s;
            }

            text = Unwrap(text).Trim().Replace("\n", " ").Replace("\r", " ");
            if (text.Length > 60) text = text.Substring(0, 60);

            var so = new SerializedObject(choice);
            so.Update();
            so.FindProperty("_choiceText").stringValue = text;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(choice);
            AssetDatabase.SaveAssets();
            Debug.Log($"💬 Choice text set to: {text}");
        }

        private static string ExtractJsonPayload(string response)
        {
            int s = response.IndexOf('{');
            int e = response.LastIndexOf('}');
            return (s >= 0 && e >= s) ? response.Substring(s, e - s + 1) : "{}";
        }

        /* ────────────────────────────────────────────
         * VISUAL COSMETICS helper
         * ──────────────────────────────────────────── */
        private struct OutcomeStyle
        {
            public Color  color;
            public string icon;
        }

        private static OutcomeStyle OutcomeStyleMap(OutcomeType t) => t switch
        {
            OutcomeType.ModifyResource  => new OutcomeStyle{color=new Color(.25f,.45f,.25f), icon="🪙"},
            OutcomeType.AddFlag         => new OutcomeStyle{color=new Color(.20f,.40f,.60f), icon="🏳"},
            OutcomeType.RemoveFlag      => new OutcomeStyle{color=new Color(.35f,.15f,.15f), icon="🚫"},
            OutcomeType.TriggerEvent    => new OutcomeStyle{color=new Color(.55f,.35f,.05f), icon="🎭"},
            OutcomeType.SetRelationship => new OutcomeStyle{color=new Color(.40f,.20f,.50f), icon="🤝"},
            OutcomeType.SetPlayerActor  => new OutcomeStyle{color=new Color(.30f,.35f,.55f), icon="🧍"},
            OutcomeType.TriggerStoryArc => new OutcomeStyle{color=new Color(.35f,.45f,.20f), icon="🧵"},
            OutcomeType.ModifyNarrativeVariable => new OutcomeStyle{color=new Color(.30f,.30f,.50f), icon="∑"},
            OutcomeType.SetNarrativeBoolTrue    => new OutcomeStyle{color=new Color(.25f,.55f,.25f), icon="✔"},
            OutcomeType.SetNarrativeBoolFalse   => new OutcomeStyle{color=new Color(.55f,.25f,.25f), icon="✖"},
            OutcomeType.AwardAchievement        => new OutcomeStyle{color=new Color(.50f,.45f,.15f), icon="🏆"},
            OutcomeType.UnlockFeature           => new OutcomeStyle{color=new Color(.20f,.50f,.50f), icon="🔓"},
            OutcomeType.CustomOutcome           => new OutcomeStyle{color=new Color(.30f,.30f,.30f), icon="⚙"},
            _                                   => new OutcomeStyle{color=new Color(.30f,.30f,.30f), icon="❔"},
        };

        /* ────────────────────────────────────────────
         * ID + Duplicate helpers
         * ──────────────────────────────────────────── */

        private void AssignNewIdFromGameData(SimEventSO e)
        {
            var gd = _gameData ?? EnsureGameDataAsset(_gameData);
            if (gd != null)
            {
                int next = gd.IncrementEventId();
                e.simEventId = $"EVT_{next}";
                EditorUtility.SetDirty(gd);
                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
                Debug.Log($"🔧 simEventId reassigned to {e.simEventId}");
            }
            else
            {
                e.simEventId = System.Guid.NewGuid().ToString();
                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
                Debug.LogWarning($"GameData not found – assigned GUID id: {e.simEventId}");
            }
        }

        private void DuplicateSelectedEventWithFreshId()
        {
            if (selectedEvent == null) return;

            Directory.CreateDirectory(EventsDir);

            SimEventSO clone = Object.Instantiate(selectedEvent);
            clone.name = selectedEvent.name + "_Copy";

            var gd = _gameData ?? EnsureGameDataAsset(_gameData);
            if (gd != null)
            {
                int next = gd.IncrementEventId();
                clone.simEventId = $"EVT_{next}";
                EditorUtility.SetDirty(gd);
            }
            else
            {
                clone.simEventId = System.Guid.NewGuid().ToString();
            }

            string originalPath = AssetDatabase.GetAssetPath(selectedEvent);
            string targetFolder = string.IsNullOrEmpty(originalPath) ? EventsDir : Path.GetDirectoryName(originalPath);
            if (string.IsNullOrEmpty(targetFolder)) targetFolder = EventsDir;

            string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, clone.name + ".asset"));

            AssetDatabase.CreateAsset(clone, newPath);
            EditorUtility.SetDirty(clone);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = clone;
            EditorGUIUtility.PingObject(clone);

            Debug.Log($"✅ Duplicated event to {newPath} with simEventId = {clone.simEventId}");
        }

        private static GameData EnsureGameDataAsset(GameData current)
        {
            if (current != null && AssetDatabase.Contains(current))
                return current;

            var moduleAsset = AssetDatabase.LoadAssetAtPath<GameData>(GameDataAssetPath);
            if (moduleAsset != null)
                return moduleAsset;

            string[] guids = AssetDatabase.FindAssets("t:GameData");
            GameData any = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (asset == null) continue;
                if (path.Contains("/SimulationGame/")) return asset;
                any ??= asset;
            }
            if (any != null) return any;

            Directory.CreateDirectory(ResourcesDir);
            var created = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(created, GameDataAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"[Editor] Created new GameData at {GameDataAssetPath}");
            return created;
        }
    }
}
