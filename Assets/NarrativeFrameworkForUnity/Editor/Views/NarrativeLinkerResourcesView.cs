#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditorInternal; // RepaintAllViews
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Resources view for the Narrative Linker tool.
    /// Designer-first helper to create and manage Resource assets
    /// without touching ScriptableObjects manually.
    ///
    /// Non-breaking:
    /// - Uses SerializedObject/SerializedProperty and tolerant lookups.
    /// - Supports both "_resources" and "resources" field names in GameData.
    /// - Uses NarrativeLinkerGUI safe wrappers to avoid IMGUI mismatches.
    /// - Now follows the active GameData via NarrativeLinkerContext.
    /// </summary>
    public sealed class NarrativeLinkerResourcesView : EditorWindow
    {
        // Menu entry (sits next to the main Linker)
        [MenuItem("Tools/Narrative Framework/Narrative Linker • Resources", priority = 203)]
        public static void Open()
        {
            var w = GetWindow<NarrativeLinkerResourcesView>("Narrative Linker • Resources");
            w.minSize = new Vector2(720, 420);
            w.Show();
        }

        // Cached editor data
        private GameData _gameData;
        private SerializedObject _gdSO;
        private SerializedProperty _pResources;

        // UI state
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private int _selectedIndex = -1;

        // Reorderable list instance for resources
        private ReorderableList _list;

        // Lifecycle guard
        private bool _initialized;

        private const string kRoot = "Assets/SimulationGame";
        private const string kData = kRoot + "/Data";
        private const string kResFolder = kData + "/Resources";

        private void OnEnable()
        {
            EnsureFolders();

            if (!_initialized)
            {
                NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
                _initialized = true;
            }

            // Prefer active context, fallback to any/canonical
            var ctx = NarrativeLinkerContext.GetOrResolve();
            RebindFromGameData(ctx);
            SetupList();
        }

        private void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnFocus()
        {
            // Refresh bindings when the window regains focus.
            var ctx = NarrativeLinkerContext.GetOrResolve();
            RebindFromGameData(ctx);
            SetupList();
            Repaint();
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            RebindFromGameData(gd);
            SetupList();
            InternalEditorUtility.RepaintAllViews();
        }

        private void OnGUI()
        {
            DrawToolbar();

            // Keep in sync with context even if window stayed open
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
            {
                RebindFromGameData(current);
                SetupList();
            }

            // Status / fallback
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                if (_gameData == null)
                {
                    EditorGUILayout.HelpBox(
                        "No active GameData selected.\nPick one in the main Narrative Linker (Game Data tab) or click 'Locate Any'.",
                        MessageType.Warning);
                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        if (GUILayout.Button("Use Active (Context)", GUILayout.Width(160)))
                        {
                            RebindFromGameData(NarrativeLinkerContext.GetOrResolve());
                            SetupList();
                        }
                        if (GUILayout.Button("Locate Any", GUILayout.Width(120)))
                        {
                            TryLocateAnyGameData();
                        }
                        GUILayout.FlexibleSpace();
                    });
                }
                else
                {
                    EditorGUILayout.LabelField("Active GameData", EditorStyles.boldLabel);
                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        EditorGUILayout.ObjectField(_gameData, typeof(GameData), false);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Ping", GUILayout.Width(60)))   EditorGUIUtility.PingObject(_gameData);
                        if (GUILayout.Button("Select", GUILayout.Width(70))) Selection.activeObject = _gameData;
                    });
                }
            });

            if (_gameData == null) return;

            if (_gdSO == null) _gdSO = new SerializedObject(_gameData);
            _gdSO.Update();

            NarrativeLinkerGUI.Horizontal(() =>
            {
                // Left: list (resources in GameData)
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    EditorGUILayout.LabelField("Resources in GameData", EditorStyles.boldLabel);
                    _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                    try
                    {
                        _list?.DoLayoutList();
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                });

                // Right: inspector of selected resource
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

                    var obj = GetSelectedResource();
                    if (obj == null)
                    {
                        EditorGUILayout.HelpBox("Select a resource on the left or create a new one.", MessageType.Info);
                        return;
                    }

                    _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                    try
                    {
                        EditorGUILayout.ObjectField("Asset", obj, typeof(Resource), false);

                        // Draw default inspector of the Resource (no assumptions about fields).
                        var ed = UnityEditor.Editor.CreateEditor(obj);
                        if (ed != null)
                        {
                            ed.OnInspectorGUI();
                            DestroyImmediate(ed);
                        }

                        EditorGUILayout.Space(6);

                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            if (GUILayout.Button("Ping", GUILayout.Width(80)))
                                EditorGUIUtility.PingObject(obj);

                            if (GUILayout.Button("Select", GUILayout.Width(80)))
                                Selection.activeObject = obj;

                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Delete", GUILayout.Width(100)))
                            {
                                if (NarrativeLinkerGUI.ConfirmDelete(obj, "Delete Resource?"))
                                {
                                    RemoveResource(obj);
                                    _selectedIndex = -1;
                                    GUIUtility.ExitGUI();
                                }
                            }
                        });
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                });
            });

            _gdSO.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────
        // Toolbar
        // ─────────────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            NarrativeLinkerGUI.Toolbar(
                ("Create New Resource", CreateNewResource),
                ("Ping GameData", () => { if (_gameData) EditorGUIUtility.PingObject(_gameData); }),
                ("Use Active (Context)", () =>
                {
                    RebindFromGameData(NarrativeLinkerContext.GetOrResolve());
                    SetupList();
                    Repaint();
                }),
                ("Locate Any", TryLocateAnyGameData)
            );
        }

        // ─────────────────────────────────────────────────────────────
        // Init / binding helpers
        // ─────────────────────────────────────────────────────────────
        private void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(kRoot)) AssetDatabase.CreateFolder("Assets", "SimulationGame");
            if (!AssetDatabase.IsValidFolder(kData)) AssetDatabase.CreateFolder(kRoot, "Data");
            if (!AssetDatabase.IsValidFolder(kResFolder)) AssetDatabase.CreateFolder(kData, "Resources");
        }

        /// <summary>
        /// Bind the view to a specific GameData (prefer context). If null, resolve canonical/any.
        /// </summary>
        private void RebindFromGameData(GameData gd)
        {
            if (gd == null)
                gd = ResolveAnyGameData();

            _gameData = gd;
            _gdSO = _gameData ? new SerializedObject(_gameData) : null;
            _pResources = _gdSO != null
                ? NarrativeLinkerGUI.FindPropAny(_gdSO, GameData.PROP_RESOURCES, "_resources", "resources")
                : null;
        }

        /// <summary>
        /// Resolve a GameData when context is not available.
        /// </summary>
        private static GameData ResolveAnyGameData()
        {
            // 1) canonical
            var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/SimulationGame/Resources/GameData.asset");
            if (canonical) return canonical;

            // 2) any in project
            var guid = AssetDatabase.FindAssets("t:GameData").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var gd = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (gd) return gd;
            }
            return null;
        }

        private void SetupList()
        {
            if (_gdSO == null || _pResources == null) { _list = null; return; }

            _list = new ReorderableList(_gdSO, _pResources, true, true, true, true);
            _list.drawHeaderCallback = r =>
            {
                EditorGUI.LabelField(r, $"Resources ({_pResources.arraySize})");
            };
            _list.onAddCallback = list => CreateNewResource();
            _list.onRemoveCallback = list =>
            {
                var obj = GetSelectedResource();
                if (obj == null) return;
                if (NarrativeLinkerGUI.ConfirmDelete(obj, "Remove Resource from GameData?"))
                {
                    RemoveResource(obj);
                }
            };
            _list.onSelectCallback = list => { _selectedIndex = list.index; };

            _list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= _pResources.arraySize) return;
                var el = _pResources.GetArrayElementAtIndex(index);
                var obj = el.objectReferenceValue as Resource;

                // Left part: object field
                var left = new Rect(rect.x, rect.y + 2, rect.width - 170, EditorGUIUtility.singleLineHeight);
                el.objectReferenceValue = EditorGUI.ObjectField(left, obj, typeof(Resource), false);

                // Right part: mini-buttons
                var rPing   = new Rect(rect.xMax - 165, rect.y + 2, 50, EditorGUIUtility.singleLineHeight);
                var rSel    = new Rect(rect.xMax - 110, rect.y + 2, 50, EditorGUIUtility.singleLineHeight);
                var rDelete = new Rect(rect.xMax - 55,  rect.y + 2, 50, EditorGUIUtility.singleLineHeight);

                using (new EditorGUI.DisabledScope(obj == null))
                {
                    if (GUI.Button(rPing, "Ping")) EditorGUIUtility.PingObject(obj);
                    if (GUI.Button(rSel,  "Select")) Selection.activeObject = obj;
                    if (GUI.Button(rDelete, "Del"))
                    {
                        if (NarrativeLinkerGUI.ConfirmDelete(obj, "Delete Resource asset?"))
                        {
                            RemoveResource(obj, alsoDeleteAsset: true);
                            if (_selectedIndex == index) _selectedIndex = -1;
                        }
                    }
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // Actions
        // ─────────────────────────────────────────────────────────────
        private void CreateNewResource()
        {
            // Create asset on disk
            var asset = NarrativeLinkerGUI.CreateAsset<Resource>(kResFolder, "New_Resource");

            // Best-effort initialize common fields via SerializedObject (no hard dependency on field names)
            var so = new SerializedObject(asset);
            var pId   = so.FindProperty("Id")           ?? so.FindProperty("_id");
            var pName = so.FindProperty("DisplayName")  ?? so.FindProperty("_displayName") ?? so.FindProperty("_name");
            var pInit = so.FindProperty("InitialValue") ?? so.FindProperty("_initialValue");
            var pMin  = so.FindProperty("MinValue")     ?? so.FindProperty("_minValue");
            var pMax  = so.FindProperty("MaxValue")     ?? so.FindProperty("_maxValue");

            if (pName != null) pName.stringValue = asset.name.Replace('_', ' ');
            if (pId   != null) pId.stringValue   = SanitizeId(asset.name);
            if (pInit != null) pInit.floatValue  = 0f;
            if (pMin  != null) pMin.floatValue   = 0f;
            if (pMax  != null) pMax.floatValue   = 100f;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            // Add to GameData list
            if (_pResources == null)
            {
                // Rebind from context/fallback just in case
                RebindFromGameData(NarrativeLinkerContext.GetOrResolve());
                if (_pResources == null)
                {
                    Debug.LogError("[ResourcesView] Could not find 'resources' field on GameData.");
                    return;
                }
            }

            _gdSO.Update();
            int i = _pResources.arraySize;
            _pResources.InsertArrayElementAtIndex(i);
            _pResources.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            _gdSO.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the newly created asset
            _selectedIndex = i;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void RemoveResource(Resource res, bool alsoDeleteAsset = false)
        {
            if (_gdSO == null || _pResources == null || res == null) return;

            _gdSO.Update();
            // Remove all occurrences (defensive)
            for (int i = _pResources.arraySize - 1; i >= 0; i--)
            {
                var el = _pResources.GetArrayElementAtIndex(i);
                if (el.objectReferenceValue == res)
                {
                    el.objectReferenceValue = null;
                    _pResources.DeleteArrayElementAtIndex(i);
                }
            }
            _gdSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_gameData);

            if (alsoDeleteAsset)
            {
                NarrativeLinkerGUI.DeleteAsset(res);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private void TryLocateAnyGameData()
        {
            var guid = AssetDatabase.FindAssets("t:GameData").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var loaded = AssetDatabase.LoadAssetAtPath<GameData>(path);
                RebindFromGameData(loaded);
                SetupList();
                if (_gameData) EditorGUIUtility.PingObject(_gameData);
            }
        }

        private Resource GetSelectedResource()
        {
            if (_pResources == null || _pResources.arraySize == 0) return null;
            if (_selectedIndex < 0 || _selectedIndex >= _pResources.arraySize) return null;
            var el = _pResources.GetArrayElementAtIndex(_selectedIndex);
            return el.objectReferenceValue as Resource;
        }

        private static string SanitizeId(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "resource";
            s = s.Trim().ToLowerInvariant();
            // Replace non-alphanumerics with underscores
            var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            var t = new string(chars);
            while (t.Contains("__")) t = t.Replace("__", "_");
            return t.Trim('_');
        }
    }
}
#endif
