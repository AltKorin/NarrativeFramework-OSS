// Assets/SimulationGame/Editor/Views/NarrativeLinkerEndingView.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// 'Endings' tab for Narrative Linker.
    /// Follows the active GameData via NarrativeLinkerContext.
    /// Uses NarrativeLinkerGUI helpers for tolerant property lookup and safe UI scopes.
    /// - Read-only status of DefaultGameOverEvent and ResourceFailRules
    /// - Ending library (create, categorize, find references)
    /// - Path visualization (Event → Choice/Outcome → Event) with quick creation for missing links
    /// </summary>
    public class NarrativeLinkerEndingView
    {
        // ───────────────────────── Consts & UI State ─────────────────────────
        private const string EventsDir = "Assets/SimulationGame/Data/Events";
        private const string LabelPrefixCategory = "Ending/Category:";

        private Vector2 _scroll;
        private string _search = "";
        private string _categoryFilter = "All";
        private readonly string[] _categoryPresets = { "All", "Victory", "Defeat", "Neutral", "Special" };

        // Active GameData (provided by NarrativeLinkerContext)
        private GameData _gd;
        private SerializedObject _gdSO;

        // Paths (Tree)
        private bool _foldPaths = true;
        private Vector2 _pathsScroll;
        private SimEventSO _rootEvent; // defaults to GameData.StartingEvent
        private bool _showOnlyMissing = false;
        private bool _showChoices = true;
        private bool _showOutcomes = true;

        // Safety
        private readonly HashSet<SimEventSO> _renderStack = new HashSet<SimEventSO>();

        // Cached props (read-only usage in this tab)
        private SerializedProperty _pStartingEvent;
        private SerializedProperty _pDefaultGameOver;      // read-only display
        private SerializedProperty _pResourceFailRules;    // read-only display

        // ───────────────────────── Lifecycle ─────────────────────────
        public void OnEnable()
        {
            _gd = NarrativeLinkerContext.GetOrResolve();
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

            _gd = gd;
            _gdSO = (_gd != null) ? new SerializedObject(_gd) : null;

            // If root was unset, prefer new GameData.StartingEvent as default
            if (_rootEvent == null && _gdSO != null)
            {
                _gdSO.Update();
                ResolveProps();
                if (_pStartingEvent != null)
                    _rootEvent = _pStartingEvent.objectReferenceValue as SimEventSO;
                _gdSO.ApplyModifiedProperties();
            }
        }

        // ───────────────────────── Entry ─────────────────────────
        public void Draw()
        {
            // Safety sync in case this view opened after a context change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gd))
                OnActiveGameDataChanged(current);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Endings", EditorStyles.boldLabel);

                // Read-only status of game-over ending & rules
                DrawGameOverStatus();

                if (_gd == null) return;

                DrawToolbar();

                var endings = LoadAllEndings();
                endings = ApplySearchAndFilter(endings);

                NarrativeLinkerGUI.Scroll(ref _scroll, () =>
                {
                    if (endings.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No endings found. Use the 'Create Ending' button to add one.", MessageType.Info);
                    }
                    else
                    {
                        foreach (var e in endings) DrawEndingRow(e);
                    }
                });

                EditorGUILayout.Space(6);
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    if (GUILayout.Button("Validate Endings", GUILayout.Width(160)))
                        ValidateEndings(endings);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Create Ending", GUILayout.Width(140)))
                    {
                        var created = CreateEndingAsset("New Ending");
                        if (created != null)
                        {
                            Selection.activeObject = created;
                            EditorGUIUtility.PingObject(created);
                        }
                    }
                });

                // Paths (Tree)
                EditorGUILayout.Space(8);
                _foldPaths = EditorGUILayout.Foldout(_foldPaths, "Paths (Tree)", true);
                if (_foldPaths)
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        DrawPathsToolbar();

                        if (_rootEvent == null)
                        {
                            EditorGUILayout.HelpBox(
                                "Select a Root Event to begin path visualization, or set the 'Starting Event' in the Game Data tab.",
                                MessageType.Info
                            );
                        }
                        else
                        {
                            // viewport with minimum height
                            GUILayout.BeginVertical(GUILayout.MinHeight(220f));
                            NarrativeLinkerGUI.Scroll(ref _pathsScroll, () =>
                            {
                                _renderStack.Clear();
                                DrawEventTree(_rootEvent, 0);
                            });
                            GUILayout.EndVertical();
                        }
                    });
                }
            }
        }

        // ───────────────────────── Read-only Game Over Status ─────────────────────────
        private void DrawGameOverStatus()
        {
            if (_gd == null)
            {
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    EditorGUILayout.HelpBox("No active GameData selected. Choose one in the 'Game Data' tab.", MessageType.Warning);
                    if (GUILayout.Button("Open Game Data tab"))
                        NarrativeLinkerWindow.ShowWindow();
                });
                EditorGUILayout.Space(8);
                return;
            }

            if (_gdSO == null) _gdSO = new SerializedObject(_gd);
            _gdSO.Update();
            ResolveProps();

            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Game Over / Endings — Status (read-only)", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                // Default game over & rules (read-only glance)
                SimEventSO defaultGameOver = _pDefaultGameOver != null ? _pDefaultGameOver.objectReferenceValue as SimEventSO : null;
                int rulesCount = _pResourceFailRules != null ? _pResourceFailRules.arraySize : 0;

                NarrativeLinkerGUI.Horizontal(() =>
                {
                    EditorGUILayout.LabelField("Default Game Over Ending:", GUILayout.Width(180));
                    EditorGUILayout.ObjectField(defaultGameOver, typeof(SimEventSO), false);
                    GUILayout.FlexibleSpace();
                    if (defaultGameOver != null)
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(60)))
                            EditorGUIUtility.PingObject(defaultGameOver);
                        if (GUILayout.Button("Select", GUILayout.Width(70)))
                            Selection.activeObject = defaultGameOver;
                    }
                });

                NarrativeLinkerGUI.Horizontal(() =>
                {
                    EditorGUILayout.LabelField("Resource Fail Rules:", GUILayout.Width(180));
                    EditorGUILayout.LabelField(rulesCount.ToString(), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                });

                EditorGUILayout.Space(4);
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    EditorGUILayout.HelpBox("Editing of game-over ending and resource-fail rules lives in the Game Data tab (single source of truth).", MessageType.Info);
                    if (GUILayout.Button("Open in Game Data", GUILayout.Width(160)))
                    {
                        Selection.activeObject = _gd;
                        EditorGUIUtility.PingObject(_gd);
                    }
                });
            });

            _gdSO.ApplyModifiedProperties();
        }

        // ───────────────────────── Toolbar & Ending list ─────────────────────────
        private void DrawToolbar()
        {
            NarrativeLinkerGUI.Horizontal(() =>
            {
                GUILayout.Label("Search", GUILayout.Width(50));
                _search = GUILayout.TextField(_search);

                GUILayout.Space(10);
                GUILayout.Label("Category", GUILayout.Width(60));
                int idx = Array.IndexOf(_categoryPresets, _categoryFilter);
                if (idx < 0) idx = 0;
                int newIdx = EditorGUILayout.Popup(idx, _categoryPresets, GUILayout.Width(120));
                _categoryFilter = _categoryPresets[newIdx];

                GUILayout.FlexibleSpace();
            }, EditorStyles.helpBox);
        }

        private void DrawEndingRow(SimEventSO e)
        {
            if (e == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    string title = string.IsNullOrWhiteSpace(e.simEventTitle) ? e.name : e.simEventTitle;
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                        EditorGUIUtility.PingObject(e);
                    if (GUILayout.Button("Select", GUILayout.Width(70)))
                        Selection.activeObject = e;
                });

                NarrativeLinkerGUI.Horizontal(() =>
                {
                    string cat = GetEndingCategory(e);
                    GUILayout.Label("Category", GUILayout.Width(60));

                    foreach (var preset in _categoryPresets.Skip(1)) // skip "All"
                    {
                        bool active = string.Equals(cat, preset, StringComparison.OrdinalIgnoreCase);
                        var prev = GUI.backgroundColor;
                        GUI.backgroundColor = active ? new Color(0.7f, 0.9f, 0.7f) : prev;

                        if (GUILayout.Button(preset, GUILayout.Width(70)))
                        {
                            SetEndingCategory(e, preset);
                            cat = preset;
                        }
                        GUI.backgroundColor = prev;
                    }

                    GUILayout.Space(8);
                    string newCat = GUILayout.TextField(cat ?? "", GUILayout.Width(140));
                    if (!string.IsNullOrWhiteSpace(newCat) && !string.Equals(newCat, cat, StringComparison.Ordinal))
                        SetEndingCategory(e, newCat);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Find References", GUILayout.Width(130)))
                        ShowReferencesPopup(e);
                });
            }
        }

        // ───────────────────────── Data helpers ─────────────────────────
        private List<SimEventSO> LoadAllEndings()
        {
            var result = new List<SimEventSO>();
            string[] guids = AssetDatabase.FindAssets("t:SimEventSO");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<SimEventSO>(path);
                if (asset != null && asset.Type == SimEventSO.EventType.Ending)
                    result.Add(asset);
            }
            return result.OrderBy(a => a.name).ToList();
        }

        private List<SimEventSO> ApplySearchAndFilter(List<SimEventSO> src)
        {
            IEnumerable<SimEventSO> q = src;

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string s = _search.Trim();
                q = q.Where(e =>
                {
                    string title = string.IsNullOrWhiteSpace(e.simEventTitle) ? e.name : e.simEventTitle;
                    return title.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (!string.IsNullOrWhiteSpace(e.simEventId) &&
                            e.simEventId.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
                });
            }

            if (!string.Equals(_categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(e => string.Equals(GetEndingCategory(e), _categoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            return q.ToList();
        }

        // ───────────────────────── Category / Asset labels ─────────────────────────
        private static string GetEndingCategory(SimEventSO e)
        {
            var labels = AssetDatabase.GetLabels(e);
            foreach (var l in labels)
                if (l.StartsWith(LabelPrefixCategory, StringComparison.Ordinal))
                    return l.Substring(LabelPrefixCategory.Length);
            return null;
        }

        private static void SetEndingCategory(UnityEngine.Object e, string category)
        {
            var labels = new List<string>(AssetDatabase.GetLabels(e));
            labels.RemoveAll(l => l.StartsWith(LabelPrefixCategory, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(category))
                labels.Add(LabelPrefixCategory + category.Trim());

            AssetDatabase.SetLabels(e, labels.ToArray());
            EditorUtility.SetDirty(e);
        }

        private SimEventSO CreateEndingAsset(string title)
        {
            // Helper creates folder if missing and registers asset safely.
            var ending = NarrativeLinkerGUI.CreateAsset<SimEventSO>(EventsDir, title);
            ending.Type = SimEventSO.EventType.Ending;
            ending.simEventTitle = title;
            ending.MinTimeUnit = 1;
            ending.MaxTimeUnit = 1;
            ending.Probability = 1f;

            if (string.IsNullOrWhiteSpace(ending.simEventId))
                ending.simEventId = "ENDING_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            EditorUtility.SetDirty(ending);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return ending;
        }

        // ───────────────────────── Validation & References ─────────────────────────
        private void ValidateEndings(List<SimEventSO> endings)
        {
            if (_gdSO == null)
            {
                EditorUtility.DisplayDialog("Validation Failed", "Active GameData not found.", "OK");
                return;
            }

            var issues = new List<string>();
            ResolveProps();

            // Check DefaultGameOverEvent presence instead of Campaign Default
            if (_pDefaultGameOver == null || _pDefaultGameOver.objectReferenceValue == null)
                issues.Add("- No Default Game Over Ending is set.");

            var refs = BuildEndingInboundMap(endings);
            foreach (var e in endings)
            {
                if (!refs.TryGetValue(e, out var list) || list.Count == 0)
                    issues.Add($"- Ending '{e.name}' appears to be unused (no choices or outcomes link to it).");
            }

            if (issues.Count == 0)
                EditorUtility.DisplayDialog("Endings Validation", "No issues found.", "OK");
            else
                EditorUtility.DisplayDialog("Endings Validation", "Issues Found:\n\n" + string.Join("\n", issues), "OK");
        }

        private void ShowReferencesPopup(SimEventSO ending)
        {
            var refs = BuildEndingInboundMap(new List<SimEventSO> { ending });
            refs.TryGetValue(ending, out var list);
            list ??= new List<UnityEngine.Object>();

            if (list.Count == 0)
            {
                EditorUtility.DisplayDialog("Find References", "No inbound references found for this ending.", "OK");
                return;
            }

            Debug.Log($"[Endings] References to '{ending.name}':\n" +
                      string.Join("\n", list.Select(o => "- " + o.name)));
            EditorGUIUtility.PingObject(list[0]);
            Selection.activeObject = list[0];
        }

        private Dictionary<SimEventSO, List<UnityEngine.Object>> BuildEndingInboundMap(List<SimEventSO> endings)
        {
            var set = new HashSet<SimEventSO>(endings.Where(e => e != null));
            var map = new Dictionary<SimEventSO, List<UnityEngine.Object>>();

            if (_gd == null || _gd.Events == null) return map;

            foreach (var evt in _gd.Events)
            {
                if (evt == null || evt.Choices == null) continue;

                foreach (var ch in evt.Choices)
                {
                    if (ch == null) continue;

                    if (ch.NextEvent != null && set.Contains(ch.NextEvent))
                        AddRef(ch.NextEvent, ch);

                    if (ch.Outcomes == null) continue;
                    foreach (var oc in ch.Outcomes)
                    {
                        if (oc == null) continue;
                        if (oc.Type != OutcomeType.TriggerEvent) continue;

                        var so = new SerializedObject(oc);
                        var p = NarrativeLinkerGUI.FindPropAny(so, "_targetEvent", "targetEvent", "m_TargetEvent");
                        var target = p != null ? p.objectReferenceValue as SimEventSO : null;

                        if (target != null && set.Contains(target))
                            AddRef(target, oc);
                    }
                }
            }

            return map;

            void AddRef(SimEventSO e, UnityEngine.Object who)
            {
                if (!map.TryGetValue(e, out var list))
                {
                    list = new List<UnityEngine.Object>();
                    map[e] = list;
                }
                list.Add(who);
            }
        }

        // ───────────────────────── Paths (Tree) ─────────────────────────
        private void DrawPathsToolbar()
        {
            if (_gdSO == null) _gdSO = new SerializedObject(_gd);
            _gdSO.Update();
            ResolveProps();

            SimEventSO defaultRoot = _pStartingEvent != null ? _pStartingEvent.objectReferenceValue as SimEventSO : null;

            NarrativeLinkerGUI.Horizontal(() =>
            {
                EditorGUILayout.LabelField("Root", GUILayout.Width(40));
                _rootEvent = (SimEventSO)EditorGUILayout.ObjectField(_rootEvent != null ? _rootEvent : defaultRoot,
                                                                     typeof(SimEventSO), false);

                if (GUILayout.Button("Use GameData.StartingEvent", GUILayout.Width(220)))
                    _rootEvent = defaultRoot;

                GUILayout.FlexibleSpace();

                _showOnlyMissing = GUILayout.Toggle(_showOnlyMissing, "Show only missing", EditorStyles.miniButton);
                _showChoices = GUILayout.Toggle(_showChoices, "Choices", EditorStyles.miniButton);
                _showOutcomes = GUILayout.Toggle(_showOutcomes, "Outcomes", EditorStyles.miniButton);
            }, EditorStyles.helpBox);

            _gdSO.ApplyModifiedProperties();
        }

        private void DrawEventTree(SimEventSO evt, int depth)
        {
            if (evt == null) return;

            // Cycle guard
            if (_renderStack.Contains(evt))
            {
                Indented(depth, () =>
                {
                    EditorGUILayout.LabelField($"↩ (cycle detected) {PrettyEventName(evt)}", EditorStyles.miniLabel);
                });
                return;
            }
            _renderStack.Add(evt);

            // Node header
            Indented(depth, () =>
            {
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    GUIStyle label = (evt.Type == SimEventSO.EventType.Ending)
                        ? new GUIStyle(EditorStyles.boldLabel)
                        : EditorStyles.boldLabel;

                    if (evt.Type == SimEventSO.EventType.Ending)
                        label.normal.textColor = new Color(0.1f, 0.6f, 0.1f);

                    EditorGUILayout.LabelField(PrettyEventName(evt), label);

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(evt);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = evt;
                });
            });

            if (evt.Type == SimEventSO.EventType.Ending)
                return;

            // Choices
            if (_showChoices && evt.Choices != null)
            {
                for (int ci = 0; ci < evt.Choices.Count; ci++)
                {
                    var choice = evt.Choices[ci];
                    if (choice == null) continue;

                    bool hasNext = (choice.NextEvent != null);
                    bool showThisChoice = !(_showOnlyMissing && hasNext);

                    if (showThisChoice)
                    {
                        Indented(depth + 1, () =>
                        {
                            NarrativeLinkerGUI.Horizontal(() =>
                            {
                                EditorGUILayout.LabelField($"Choice: \"{choice.ChoiceText}\"", EditorStyles.label);

                                if (choice.GoToNextDay)
                                    EditorGUILayout.LabelField("→ Ends Day", EditorStyles.miniLabel, GUILayout.Width(90));

                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                    EditorGUIUtility.PingObject(choice);
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                    Selection.activeObject = choice;
                            });

                            if (choice.NextEvent != null)
                            {
                                Indented(depth + 2, () =>
                                {
                                    EditorGUILayout.LabelField($"NextEvent → {PrettyEventName(choice.NextEvent)}", EditorStyles.miniLabel);
                                });
                                DrawEventTree(choice.NextEvent, depth + 2);
                            }
                            else
                            {
                                DrawCreateMissingLinkUI(depth + 2, choice, null, "NextEvent is missing");
                            }
                        });
                    }
                }
            }

            // Outcomes
            if (_showOutcomes && evt.Choices != null)
            {
                for (int ci = 0; ci < evt.Choices.Count; ci++)
                {
                    var choice = evt.Choices[ci];
                    if (choice == null || choice.Outcomes == null) continue;

                    bool hasAnyToShow = false;
                    foreach (var oc in choice.Outcomes)
                    {
                        if (oc == null) continue;
                        bool isTrig = oc.Type == OutcomeType.TriggerEvent;
                        bool missing = isTrig && GetOutcomeTargetEvent(oc) == null;
                        if (!_showOnlyMissing || missing) { hasAnyToShow = true; break; }
                    }
                    if (!hasAnyToShow) continue;

                    Indented(depth + 1, () =>
                    {
                        EditorGUILayout.LabelField("Outcomes:", EditorStyles.miniBoldLabel);
                    });

                    for (int oi = 0; oi < choice.Outcomes.Count; oi++)
                    {
                        var outcome = choice.Outcomes[oi];
                        if (outcome == null) continue;

                        bool isTrigger = (outcome.Type == OutcomeType.TriggerEvent);
                        var target = GetOutcomeTargetEvent(outcome);
                        bool showThisOutcome = !(_showOnlyMissing && isTrigger && target != null);
                        if (!showThisOutcome) continue;

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
                                {
                                    DrawEventTree(target, depth + 3);
                                }
                                else
                                {
                                    DrawCreateMissingLinkUI(depth + 3, choice, outcome, "Outcome target is missing");
                                }
                            }
                        });
                    }
                }
            }
        }

        // ───────────────────────── Helpers ─────────────────────────
        private static SimEventSO GetOutcomeTargetEvent(Outcome outcome)
        {
            if (outcome == null || outcome.Type != OutcomeType.TriggerEvent) return null;
            var so = new SerializedObject(outcome);
            var p = NarrativeLinkerGUI.FindPropAny(so, "_targetEvent", "targetEvent", "m_TargetEvent");
            return p != null ? p.objectReferenceValue as SimEventSO : null;
        }

        private void DrawCreateMissingLinkUI(int depth, Choice choice, Outcome outcome, string message)
        {
            Indented(depth, () =>
            {
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    EditorGUILayout.LabelField(message, EditorStyles.miniLabel);

                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        if (GUILayout.Button("Create Event", GUILayout.Width(110)))
                        {
                            var created = CreateEndingAsset("New Event");
                            if (created != null)
                            {
                                if (outcome == null)
                                {
                                    // Missing Choice.NextEvent → assign via SerializedProperty (property is read-only)
                                    var soChoice = new SerializedObject(choice);
                                    var pNext = NarrativeLinkerGUI.FindPropAny(soChoice, "_nextEvent", "nextEvent", "m_NextEvent");

                                    if (pNext != null)
                                    {
                                        Undo.RecordObject(choice, "Assign NextEvent");
                                        pNext.objectReferenceValue = created;
                                        soChoice.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(choice);
                                    }
                                    else
                                    {
                                        EditorUtility.DisplayDialog(
                                            "Assignment Required",
                                            "Could not locate the serialized backing field for Choice.NextEvent.\n" +
                                            "Please assign the newly created event in the Inspector.",
                                            "OK");
                                    }
                                }
                                else
                                {
                                    // Missing Outcome target → assign via SerializedProperty
                                    var so = new SerializedObject(outcome);
                                    var p = NarrativeLinkerGUI.FindPropAny(so, "_targetEvent", "targetEvent", "m_TargetEvent");
                                    if (p != null)
                                    {
                                        p.objectReferenceValue = created;
                                        so.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(outcome);
                                    }
                                    else
                                    {
                                        EditorUtility.DisplayDialog(
                                            "Assignment Required",
                                            "Could not locate the serialized backing field for Outcome target.\n" +
                                            "Please assign the newly created event in the Inspector.",
                                            "OK");
                                    }
                                }
                                Selection.activeObject = created;
                                EditorGUIUtility.PingObject(created);
                            }
                        }

                        if (GUILayout.Button("Pick Existing", GUILayout.Width(110)))
                        {
                            EditorGUIUtility.ShowObjectPicker<SimEventSO>(null, false, "", 0);
                            // Designers can assign in inspector after picking (ObjectPicker is async).
                        }

                        GUILayout.FlexibleSpace();
                    });
                });
            });
        }

        private static void Indented(int depth, Action draw)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(Mathf.Clamp(depth, 0, 64) * 16f);
                using (new EditorGUILayout.VerticalScope())
                {
                    draw?.Invoke();
                }
            }
        }

        private static string PrettyEventName(SimEventSO e)
        {
            if (e == null) return "(null)";
            string title = string.IsNullOrWhiteSpace(e.simEventTitle) ? e.name : e.simEventTitle;
            return $"{title}  <{e.simEventId}>";
        }

        private void ResolveProps()
        {
            if (_gdSO == null) return;

            _pStartingEvent     = NarrativeLinkerGUI.FindPropAny(_gdSO, "_startingEvent", "startingEvent");
            _pDefaultGameOver   = NarrativeLinkerGUI.FindPropAny(_gdSO, "_defaultGameOverEvent", "DefaultGameOverEvent", "defaultGameOverEvent");
            _pResourceFailRules = NarrativeLinkerGUI.FindPropAny(_gdSO, "_resourceFailRules", "ResourceFailRules", "resourceFailRules");
        }
    }
}
