// Assets/SimulationGame/Editor/Views/NarrativeLinkerOutcomeView.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal; // RepaintAllViews
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Outcome browser/editor as a *panel* used by NarrativeLinkerView.
    /// Minimal changes:
    /// - Uses NarrativeLinkerContext for active GameData.
    /// - Adds "Only referenced" filter (shows Outcomes referenced by Events of active GameData).
    /// - Adds simple Search.
    /// - Right pane draws default inspector for the selected Outcome (compat w/ your fields).
    /// </summary>
    public class NarrativeLinkerOutcomeView
    {
        // Active GameData (from context)
        private GameData _gameData;
        private bool _initialized;

        // UI data
        private readonly List<Outcome> _outcomes = new List<Outcome>();
        private Outcome _selected;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // Filters
        private bool _filterReferencedOnly = false;
        private string _search = "";

        // Folders (for new assets)
        private const string kRoot = "Assets/SimulationGame";
        private const string kData = kRoot + "/Data";
        private const string kOutcomesFolder = kData + "/Outcomes";

        // ─────────────────────────────────────────────────────────────
        // Lifecycle (called by NarrativeLinkerView)
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            EnsureFolders();

            if (!_initialized)
            {
                _gameData = NarrativeLinkerContext.GetOrResolve();
                NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
                _initialized = true;
            }

            RefreshList();
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gameData, gd)) return;
            _gameData = gd;
            RefreshList();
            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────────
        // Main panel draw (called by NarrativeLinkerView)
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            DrawToolbar();

            // keep in sync with context even if tab stayed open
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
            {
                _gameData = current;
                RefreshList();
            }

            // Status header: Active GameData
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Active GameData", EditorStyles.boldLabel);
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    EditorGUILayout.ObjectField(_gameData, typeof(GameData), false);
                    GUILayout.FlexibleSpace();
                    if (_gameData && GUILayout.Button("Ping", GUILayout.Width(60)))
                        EditorGUIUtility.PingObject(_gameData);
                    if (_gameData && GUILayout.Button("Select", GUILayout.Width(70)))
                        Selection.activeObject = _gameData;
                });
            });

            EditorGUILayout.Space(4);

            NarrativeLinkerGUI.Horizontal(() =>
            {
                // LEFT: list (fixed width). Zewnętrzny Begin/End dla szerokości, box ma własne guardy.
                GUILayout.BeginVertical(GUILayout.Width(330));
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    DrawLeftPane();
                });
                GUILayout.EndVertical();

                // RIGHT: inspector (fills remaining space)
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    DrawRightPane();
                });
            });
        }

        private void DrawToolbar()
        {
            NarrativeLinkerGUI.Toolbar(
                ("Refresh", RefreshList),
                ("New Outcome", CreateNewOutcome),
                ("Only referenced", () =>
                {
                    _filterReferencedOnly = !_filterReferencedOnly;
                    RefreshList();
                }),
                ("Clear Search", () =>
                {
                    _search = "";
                    RefreshList();
                })
            );
        }

        private void DrawLeftPane()
        {
            // Filter row
            NarrativeLinkerGUI.Horizontal(() =>
            {
                GUILayout.Label("Search", GUILayout.Width(50));
                string newSearch = EditorGUILayout.TextField(_search ?? "");
                if (newSearch != _search)
                {
                    _search = newSearch;
                    RefreshList();
                }

                GUILayout.FlexibleSpace();

                bool newToggle = GUILayout.Toggle(_filterReferencedOnly, "Only referenced", EditorStyles.miniButton, GUILayout.Width(120));
                if (newToggle != _filterReferencedOnly)
                {
                    _filterReferencedOnly = newToggle;
                    RefreshList();
                }
            });

            EditorGUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            try
            {
                for (int i = 0; i < _outcomes.Count; i++)
                {
                    var o = _outcomes[i];
                    if (!o) continue;

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(o.name, (_selected == o) ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        _selected = o;
                        EditorGUIUtility.PingObject(o);
                    }

                    if (GUILayout.Button("🗑", EditorStyles.miniButtonRight, GUILayout.Width(26)))
                    {
                        if (NarrativeLinkerGUI.ConfirmDelete(o, "Delete Outcome asset?"))
                        {
                            DeleteOutcome(o);
                            if (_selected == o) _selected = null;
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (_outcomes.Count == 0)
                {
                    EditorGUILayout.HelpBox(_filterReferencedOnly
                            ? "No referenced Outcomes found for the active GameData."
                            : "No Outcomes found in project.",
                        MessageType.Info);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRightPane()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an Outcome on the left or create a new one.", MessageType.Info);
                return;
            }

            // Header actions
            NarrativeLinkerGUI.Horizontal(() =>
            {
                GUILayout.Label($"Outcome — {_selected.name}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping", GUILayout.Width(60)))   EditorGUIUtility.PingObject(_selected);
                if (GUILayout.Button("Select", GUILayout.Width(70))) Selection.activeObject = _selected;
            });

            EditorGUILayout.Space(4);

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            try
            {
                // Draw default inspector (keeps compatibility with your Outcome fields)
                var ed = UnityEditor.Editor.CreateEditor(_selected);
                if (ed != null)
                {
                    ed.OnInspectorGUI();
                    Object.DestroyImmediate(ed);
                }

                EditorGUILayout.Space(6);
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Delete Outcome", GUILayout.Width(140)))
                    {
                        if (NarrativeLinkerGUI.ConfirmDelete(_selected, "Delete Outcome asset?"))
                        {
                            var toDelete = _selected;
                            _selected = null;
                            DeleteOutcome(toDelete);
                            GUIUtility.ExitGUI();
                        }
                    }
                });
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Data ops
        // ─────────────────────────────────────────────────────────────
        private void RefreshList()
        {
            var all = new List<Outcome>();

            foreach (var guid in AssetDatabase.FindAssets("t:Outcome"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var o = AssetDatabase.LoadAssetAtPath<Outcome>(path);
                if (o) all.Add(o);
            }

            if (_filterReferencedOnly && _gameData != null)
            {
                var referenced = BuildReferencedOutcomeSet(_gameData);
                all.RemoveAll(o => !referenced.Contains(o));
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.ToLowerInvariant();
                all.RemoveAll(o => o == null || !o.name.ToLowerInvariant().Contains(q));
            }

            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _outcomes.Clear();
            _outcomes.AddRange(all);

            if (_selected != null && !_outcomes.Contains(_selected))
                _selected = null;
        }

        private static HashSet<Outcome> BuildReferencedOutcomeSet(GameData gd)
        {
            var set = new HashSet<Outcome>();
            if (gd == null || gd.Events == null) return set;

            foreach (var ev in gd.Events)
            {
                if (!ev) continue;

                // Collect asset dependencies of this event (includes sub-assets referenced as assets)
                var deps = EditorUtility.CollectDependencies(new Object[] { ev });
                foreach (var d in deps)
                {
                    if (d is Outcome o && o) set.Add(o);
                }
            }
            return set;
        }

        private void CreateNewOutcome()
        {
            EnsureFolders();

            var asset = ScriptableObject.CreateInstance<Outcome>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{kOutcomesFolder}/Outcome_New.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _selected = asset;
            RefreshList();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private void DeleteOutcome(Outcome asset)
        {
            if (asset == null) return;

            // Remove from our in-memory list
            _outcomes.Remove(asset);

            // Delete the asset
            string path = AssetDatabase.GetAssetPath(asset);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(kRoot)) AssetDatabase.CreateFolder("Assets", "SimulationGame");
            if (!AssetDatabase.IsValidFolder(kData)) AssetDatabase.CreateFolder(kRoot, "Data");
            if (!AssetDatabase.IsValidFolder(kOutcomesFolder)) AssetDatabase.CreateFolder(kData, "Outcomes");
        }
    }
}
#endif
