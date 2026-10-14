using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Event Tree view that follows the active GameData via NarrativeLinkerContext.
    /// Shows Choices & Outcomes and allows creating/linking missing targets.
    /// </summary>
    public class NarrativeLinkerEventTreeView
    {
        // ─────────────────────────────────────────────────────────────
        // Context & UI state
        // ─────────────────────────────────────────────────────────────
        private GameData _gd;
        private SerializedObject _gdSO;

        private Vector2 _scroll;
        private bool _showOnlyMissing = false;
        private bool _showChoices     = true;
        private bool _showOutcomes    = true;

        private SimEventSO _rootEvent; // if null, defaults to GameData.StartingEvent
        private readonly HashSet<SimEventSO> _renderStack = new HashSet<SimEventSO>();

        // pamiętane tytuły nowych eventów per „dziura” (Choice / Outcome)
        private readonly Dictionary<UnityEngine.Object, string> _newEventTitles =
            new Dictionary<UnityEngine.Object, string>();

        // ─────────────────────────────────────────────────────────────
        // Lifecycle (hook into active GameData)
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            _gd   = NarrativeLinkerContext.GetOrResolve();
            _gdSO = (_gd != null) ? new SerializedObject(_gd) : null;
            NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gd, gd)) return;

            _gd   = gd;
            _gdSO = (_gd != null) ? new SerializedObject(_gd) : null;

            if (_gdSO != null)
            {
                _gdSO.Update();
                var pStart = NarrativeLinkerGUI.FindPropAny(_gdSO, "_startingEvent", "startingEvent");
                _rootEvent = pStart != null ? pStart.objectReferenceValue as SimEventSO : null;
                _gdSO.ApplyModifiedProperties();
            }
            else
            {
                _rootEvent = null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Entry point
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gd))
                OnActiveGameDataChanged(current);

            EditorGUILayout.LabelField("Event Tree", EditorStyles.boldLabel);

            NarrativeLinkerGUI.VerticalBox(() =>
            {
                DrawToolbar();

                if (_rootEvent == null)
                {
                    EditorGUILayout.HelpBox(
                        "Select a Root Event (or set GameData.StartingEvent).",
                        MessageType.Info
                    );
                }
                else
                {
                    GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
                    NarrativeLinkerGUI.Scroll(ref _scroll, () =>
                    {
                        _renderStack.Clear();
                        DrawEventTree(_rootEvent, 0);
                    });
                    GUILayout.EndVertical();
                }
            });

            if (_gd == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "No active GameData selected. Pick one in the Game Data tab.",
                    MessageType.Warning
                );
            }
        }

        private void DrawToolbar()
        {
            NarrativeLinkerGUI.Horizontal(() =>
            {
                if (_gd != null && _gdSO == null)
                    _gdSO = new SerializedObject(_gd);
                if (_gdSO != null)
                    _gdSO.Update();

                SimEventSO defaultRoot = null;
                if (_gdSO != null)
                {
                    var pStart = NarrativeLinkerGUI.FindPropAny(_gdSO, "_startingEvent", "startingEvent");
                    if (pStart != null)
                        defaultRoot = pStart.objectReferenceValue as SimEventSO;
                }

                EditorGUILayout.LabelField("Root", GUILayout.Width(40));
                _rootEvent = (SimEventSO)EditorGUILayout.ObjectField(
                    _rootEvent != null ? _rootEvent : defaultRoot,
                    typeof(SimEventSO),
                    false
                );

                using (new EditorGUI.DisabledScope(defaultRoot == null))
                {
                    if (GUILayout.Button("Use GameData.StartingEvent", GUILayout.Width(220)))
                        _rootEvent = defaultRoot;
                }

                GUILayout.FlexibleSpace();

                _showOnlyMissing = GUILayout.Toggle(
                    _showOnlyMissing,
                    "Show only missing",
                    EditorStyles.miniButton
                );
                _showChoices = GUILayout.Toggle(
                    _showChoices,
                    "Choices",
                    EditorStyles.miniButton
                );
                _showOutcomes = GUILayout.Toggle(
                    _showOutcomes,
                    "Outcomes",
                    EditorStyles.miniButton
                );

                if (_gdSO != null)
                    _gdSO.ApplyModifiedProperties();
            }, EditorStyles.helpBox);
        }

        // ─────────────────────────────────────────────────────────────
        // Tree rendering
        // ─────────────────────────────────────────────────────────────
        private void DrawEventTree(SimEventSO evt, int depth)
        {
            if (evt == null) return;

            if (_renderStack.Contains(evt))
            {
                Indented(depth, () =>
                    EditorGUILayout.LabelField(
                        $"↩ (cycle) {PrettyEventName(evt)}",
                        EditorStyles.miniLabel
                    ));
                return;
            }
            _renderStack.Add(evt);

            // header z kolorem statusu (QoL)
            Indented(depth, () =>
            {
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel);

                    Color prev = GUI.color;
                    GUI.color = GetStatusColor(evt);

                    EditorGUILayout.LabelField(PrettyEventName(evt), labelStyle);

                    GUI.color = prev;

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(evt);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = evt;
                });
            });

            if (evt.Type == SimEventSO.EventType.Ending)
                return;

            // choices
            if (_showChoices && evt.Choices != null)
            {
                for (int ci = 0; ci < evt.Choices.Count; ci++)
                {
                    var choice = evt.Choices[ci];
                    if (choice == null) continue;

                    bool hasNext = (choice.NextEvent != null);
                    bool show    = !(_showOnlyMissing && hasNext);
                    if (!show) continue;

                    Indented(depth + 1, () =>
                    {
                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            EditorGUILayout.LabelField($"Choice: \"{choice.ChoiceText}\"", EditorStyles.label);
                            if (choice.GoToNextDay)
                                EditorGUILayout.LabelField("→ Ends Day", EditorStyles.miniLabel, GUILayout.Width(90));

                            GUILayout.FlexibleSpace();

                            // NOWE: szybkie dodanie Outcome
                            if (GUILayout.Button("+Outcome", GUILayout.Width(80)))
                            {
                                var created = CreateOutcomeForChoice(evt, choice);
                                if (created != null)
                                {
                                    Selection.activeObject = created;
                                    EditorGUIUtility.PingObject(created);
                                    GUIUtility.ExitGUI();
                                }
                            }

                            if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                EditorGUIUtility.PingObject(choice);
                            if (GUILayout.Button("Select", GUILayout.Width(60)))
                                Selection.activeObject = choice;
                        });

                        if (choice.NextEvent != null)
                        {
                            Indented(depth + 2, () =>
                                EditorGUILayout.LabelField(
                                    $"NextEvent → {PrettyEventName(choice.NextEvent)}",
                                    EditorStyles.miniLabel
                                ));
                            DrawEventTree(choice.NextEvent, depth + 2);
                        }
                        else
                        {
                            DrawCreateMissingLinkUI(
                                depth + 2,
                                choice,
                                null,
                                "NextEvent is missing"
                            );
                        }
                    });
                }
            }

            // outcomes
            if (_showOutcomes && evt.Choices != null)
            {
                for (int ci = 0; ci < evt.Choices.Count; ci++)
                {
                    var choice = evt.Choices[ci];
                    if (choice == null || choice.Outcomes == null) continue;

                    bool anyToShow = false;
                    foreach (var oc in choice.Outcomes)
                    {
                        if (oc == null) continue;
                        bool isTrigger = oc.Type == OutcomeType.TriggerEvent;
                        bool missing   = isTrigger && GetOutcomeTargetEvent(oc) == null;
                        if (!_showOnlyMissing || missing)
                        {
                            anyToShow = true;
                            break;
                        }
                    }
                    if (!anyToShow) continue;

                    Indented(depth + 1, () =>
                        EditorGUILayout.LabelField("Outcomes:", EditorStyles.miniBoldLabel));

                    for (int oi = 0; oi < choice.Outcomes.Count; oi++)
                    {
                        var outcome = choice.Outcomes[oi];
                        if (outcome == null) continue;

                        bool isTrigger = (outcome.Type == OutcomeType.TriggerEvent);
                        var target     = GetOutcomeTargetEvent(outcome);
                        bool show      = !(_showOnlyMissing && isTrigger && target != null);
                        if (!show) continue;

                        Indented(depth + 2, () =>
                        {
                            NarrativeLinkerGUI.Horizontal(() =>
                            {
                                string line = isTrigger
                                    ? $"Outcome[{oi}] TriggerEvent → {(target ? PrettyEventName(target) : "(missing)")}"
                                    : $"Outcome[{oi}] {outcome.Type}";
                                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);

                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                    EditorGUIUtility.PingObject(outcome);
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                    Selection.activeObject = outcome;
                            });

                            if (isTrigger)
                            {
                                if (target != null)
                                    DrawEventTree(target, depth + 3);
                                else
                                    DrawCreateMissingLinkUI(
                                        depth + 3,
                                        null,
                                        outcome,
                                        "Target Event is missing"
                                    );
                            }
                        });
                    }
                }
            }
        }

        private void DrawCreateMissingLinkUI(int depth, Choice choice, Outcome outcome, string hint)
{
    Indented(depth, () =>
    {
        NarrativeLinkerGUI.VerticalBox(() =>
        {
            NarrativeLinkerGUI.Horizontal(() =>
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(hint, EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
            });

            // klucz – Choice albo Outcome, żeby pamiętać tytuł per „dziura”
            UnityEngine.Object key = (UnityEngine.Object)choice ?? outcome;

            // ZAWSZE zainicjalizowane
            string newTitle = "New Event";
            if (key != null && _newEventTitles.TryGetValue(key, out var storedTitle))
                newTitle = storedTitle;

            EditorGUI.BeginChangeCheck();
            newTitle = EditorGUILayout.TextField("New Event Title", newTitle);
            if (EditorGUI.EndChangeCheck() && key != null)
                _newEventTitles[key] = newTitle;

            NarrativeLinkerGUI.Horizontal(() =>
            {
                if (GUILayout.Button("Create & Link", GUILayout.Width(140)))
                {
                    if (string.IsNullOrWhiteSpace(newTitle))
                        newTitle = "New Event";

                    CreateAndLinkEvent(newTitle, choice, outcome);

                    if (key != null)
                        _newEventTitles.Remove(key);

                    InternalEditorUtility.RepaintAllViews();
                }

                GUILayout.FlexibleSpace();
            });
        });
    });
}


        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Creates a new SimEventSO, assigns ID via active GameData (if any),
        /// and links it to Choice.NextEvent or Outcome.target.
        /// </summary>
        private void CreateAndLinkEvent(string title, Choice choice, Outcome outcome)
        {
            const string dir = "Assets/NarrativeFrameworkForUnity/Data/Events";

            var newEvent = NarrativeLinkerGUI.CreateAsset<SimEventSO>(dir, title);
            newEvent.simEventTitle       = title;
            newEvent.simEventDescription = "";
            newEvent.MinTimeUnit         = 1;
            newEvent.MaxTimeUnit         = 30;
            newEvent.Probability         = 1f;
            newEvent.Type                = SimEventSO.EventType.Main;

            if (_gd != null)
            {
                int next = _gd.IncrementEventId();
                newEvent.simEventId = $"EVT_{next}";
                EditorUtility.SetDirty(_gd);
                _gd.AddEvent(newEvent);
            }
            else
            {
                newEvent.simEventId = Guid.NewGuid().ToString("N");
            }

            EditorUtility.SetDirty(newEvent);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (choice != null)
            {
                var soChoice = new SerializedObject(choice);
                soChoice.Update();
                var pNext = NarrativeLinkerGUI.FindPropAny(
                    soChoice,
                    "_nextEvent",
                    "nextEvent",
                    "m_NextEvent"
                );
                if (pNext != null)
                {
                    Undo.RecordObject(choice, "Assign NextEvent");
                    pNext.objectReferenceValue = newEvent;
                    soChoice.ApplyModifiedProperties();
                    EditorUtility.SetDirty(choice);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Assignment Required",
                        "Could not locate the serialized backing field for Choice.NextEvent.\nPlease assign the newly created event in the Inspector.",
                        "OK"
                    );
                }
            }
            else if (outcome != null)
            {
                var soOutcome = new SerializedObject(outcome);
                soOutcome.Update();
                var p = NarrativeLinkerGUI.FindPropAny(
                    soOutcome,
                    "_targetEvent",
                    "targetEvent",
                    "m_TargetEvent"
                );
                if (p != null)
                {
                    p.objectReferenceValue = newEvent;
                    soOutcome.ApplyModifiedProperties();
                    EditorUtility.SetDirty(outcome);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Assignment Required",
                        "Could not locate the serialized backing field for Outcome target.\nPlease assign the newly created event in the Inspector.",
                        "OK"
                    );
                }
            }

            Selection.activeObject = newEvent;
            EditorGUIUtility.PingObject(newEvent);
            GUIUtility.ExitGUI();
        }

        private static SimEventSO GetOutcomeTargetEvent(Outcome outcome)
        {
            if (outcome == null) return null;
            var so = new SerializedObject(outcome);
            var p  = NarrativeLinkerGUI.FindPropAny(
                so,
                "_targetEvent",
                "targetEvent",
                "m_TargetEvent"
            );
            return p != null ? p.objectReferenceValue as SimEventSO : null;
        }

        private static void Indented(int depth, Action draw)
        {
            int prev = EditorGUI.indentLevel;
            EditorGUI.indentLevel = Mathf.Max(0, depth);
            try
            {
                draw?.Invoke();
            }
            finally
            {
                EditorGUI.indentLevel = prev;
            }
        }

        private static string PrettyEventName(SimEventSO e)
        {
            if (e == null) return "<null>";
            string baseName = !string.IsNullOrEmpty(e.simEventTitle)
                ? e.simEventTitle
                : e.name;
            string suffix = e.Type == SimEventSO.EventType.Ending ? " [Ending]" : "";
            return $"{baseName}{suffix}";
        }

        // prosty status kolorów dla eventu (QoL)
        private static Color GetStatusColor(SimEventSO e)
        {
            if (e == null)
                return Color.white;

            if (e.Type == SimEventSO.EventType.Ending)
                return new Color(0.4f, 0.9f, 0.4f); // zielony

            if (e.Choices == null || e.Choices.Count == 0)
                return new Color(1f, 0.4f, 0.4f);   // czerwony

            bool hasMissingLink = false;
            foreach (var c in e.Choices)
            {
                if (c == null) continue;

                // brak NextEvent i nie kończy dnia → „dziura”
                if (c.NextEvent == null && !c.GoToNextDay)
                {
                    hasMissingLink = true;
                    break;
                }
            }

            if (hasMissingLink)
                return new Color(1f, 0.8f, 0.4f);   // żółty

            return Color.white;
        }
        private static Outcome CreateOutcomeForChoice(SimEventSO evt, Choice choice)
{
    if (evt == null || choice == null) return null;

    // ── 1. Upewnij się, że istnieje folder na Outcomes ─────────────────────
    const string root   = "Assets/NarrativeFrameworkForUnity";
    const string data   = root + "/Data";
    const string folder = data + "/Outcomes";

    if (!AssetDatabase.IsValidFolder(root))
        AssetDatabase.CreateFolder("Assets", "NarrativeFrameworkForUnity");
    if (!AssetDatabase.IsValidFolder(data))
        AssetDatabase.CreateFolder(root, "Data");
    if (!AssetDatabase.IsValidFolder(folder))
        AssetDatabase.CreateFolder(data, "Outcomes");

    // ── 2. Utwórz asset Outcome ────────────────────────────────────────────
    string baseName = !string.IsNullOrEmpty(evt.simEventTitle) ? evt.simEventTitle : evt.name;
    string safeChoice = string.IsNullOrWhiteSpace(choice.ChoiceText) ? "Choice" : choice.ChoiceText;
    safeChoice = safeChoice.Replace(' ', '_');

    string assetName = $"Outcome_{baseName}_{safeChoice}";
    var outcome = ScriptableObject.CreateInstance<Outcome>();
    string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");

    AssetDatabase.CreateAsset(outcome, path);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    // ── 3. Dopnij do listy Outcomes w Choice (serialized, bez grzebania w runtime API) ─────
    Undo.RecordObject(choice, "Add Outcome");

    var soChoice = new SerializedObject(choice);
    var pOutcomes = NarrativeLinkerGUI.FindPropAny(
        soChoice,
        "_outcomes", "outcomes", "Outcomes"
    );

    if (pOutcomes != null && pOutcomes.isArray)
    {
        int index = pOutcomes.arraySize;
        pOutcomes.InsertArrayElementAtIndex(index);
        var elem = pOutcomes.GetArrayElementAtIndex(index);
        elem.objectReferenceValue = outcome;
        soChoice.ApplyModifiedProperties();
    }
    else
    {
        EditorUtility.DisplayDialog(
            "Assignment Required",
            "Could not locate the serialized 'Outcomes' list property on Choice.\nPlease add the outcome manually in the Inspector.",
            "OK"
        );
    }

    return outcome;
}
    }
}
