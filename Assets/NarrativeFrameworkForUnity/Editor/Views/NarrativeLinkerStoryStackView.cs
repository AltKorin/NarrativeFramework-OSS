// Assets/SimulationGame/Editor/Views/NarrativeLinkerStoryStackView.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Story Stack editor for the Narrative Linker window.
    /// - Follows the active GameData via NarrativeLinkerContext
    /// - Lets you browse/create stacks and assign one to GameData
    /// - Uses SerializedObject + tolerant property lookup (NarrativeLinkerGUI.FindPropAny)
    /// - Safe IMGUI scopes via NarrativeLinkerGUI helpers
    /// </summary>
    public class NarrativeLinkerStoryStackView
    {
        private const string ModuleRoot = "Assets/SimulationGame";
        private const string DataDir    = ModuleRoot + "/Data";
        private const string StacksDir  = DataDir + "/StoryStacks";

        // Context: active GameData and its SerializedObject
        private GameData _gd;
        private SerializedObject _gdSO;

        // Cached prop to the stack reference inside GameData
        private SerializedProperty _pStoryStack;

        // UI state
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // Project stacks list + current selection
        private readonly List<StoryStackSO> _found = new();
        private StoryStackSO _selected;
        private SerializedObject _so; // editor for selected stack

        // ─────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            _gd   = NarrativeLinkerContext.GetOrResolve();
            _gdSO = (_gd != null) ? new SerializedObject(_gd) : null;
            ResolveGameDataProps();

            RefreshList();
            // If GameData has an assigned stack, prefer selecting it
            var gdStack = GetGameDataStack();
            if (gdStack != null) Select(gdStack);

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
            ResolveGameDataProps();

            // On GD switch: reselect its stack if present
            var gdStack = GetGameDataStack();
            if (gdStack != null) Select(gdStack);

            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────────
        // Entry
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            // Safety sync if view opened after a context change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gd))
                OnActiveGameDataChanged(current);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeft();
                DrawRight();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Left panel: list & actions
        // ─────────────────────────────────────────────────────────────
        private void DrawLeft()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField("Story Stacks", EditorStyles.boldLabel);

                // GameData selector row (read-only info + quick actions)
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    if (_gd == null)
                    {
                        EditorGUILayout.HelpBox("No active GameData selected. Choose one in the Game Data tab.", MessageType.Warning);
                    }
                    else
                    {
                        _gdSO ??= new SerializedObject(_gd);
                        _gdSO.Update();
                        ResolveGameDataProps();

                        var gdStack = GetGameDataStack();

                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            EditorGUILayout.LabelField("Active GameData:", GUILayout.Width(120));
                            EditorGUILayout.ObjectField(_gd, typeof(GameData), false);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Select", GUILayout.Width(60)))
                                Selection.activeObject = _gd;
                        });

                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            EditorGUILayout.LabelField("GameData StoryStack:", GUILayout.Width(120));
                            var newRef = (StoryStackSO)EditorGUILayout.ObjectField(gdStack, typeof(StoryStackSO), false);

                            if (newRef != gdStack)
                            {
                                SetGameDataStack(newRef);
                            }

                            GUILayout.FlexibleSpace();
                            if (gdStack != null)
                            {
                                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                    EditorGUIUtility.PingObject(gdStack);
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                    Selection.activeObject = gdStack;
                            }
                        });

                        // Quick assign from current selection
                        using (new EditorGUI.DisabledScope(_selected == null || _selected == gdStack))
                        {
                            if (GUILayout.Button("Set Selected as GameData StoryStack"))
                            {
                                SetGameDataStack(_selected);
                            }
                        }

                        _gdSO.ApplyModifiedProperties();
                    }
                });

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                        RefreshList();

                    if (GUILayout.Button("Create", GUILayout.Width(80)))
                    {
                        var created = CreateNew();
                        if (created != null && _gd != null)
                        {
                            // convenience: assign freshly created as GD stack
                            SetGameDataStack(created);
                        }
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                        EditorUtility.RevealInFinder(StacksDir);
                }

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                foreach (var s in _found)
                {
                    if (s == null) continue;

                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        bool isSelected = _selected == s;
                        if (GUILayout.Toggle(isSelected, s.name, "Button"))
                        {
                            if (!isSelected)
                                Select(s);
                        }

                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                            EditorGUIUtility.PingObject(s);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Right panel: inspector of selected StoryStackSO
        // ─────────────────────────────────────────────────────────────
        private void DrawRight()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("Select or create a StoryStackSO to edit.", MessageType.Info);
                    return;
                }

                _so ??= new SerializedObject(_selected);
                _so.Update();

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                // Basic (id + mode)
                EditorGUILayout.PropertyField(
                    NarrativeLinkerGUI.FindPropAny(_so, "_stackId", "stackId", "m_StackId"),
                    new GUIContent("Stack Id"));
                EditorGUILayout.PropertyField(
                    NarrativeLinkerGUI.FindPropAny(_so, "_mode", "mode", "m_Mode"),
                    new GUIContent("Mode"));

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("General Options", EditorStyles.miniBoldLabel);
                    SafePropToggle(_so, "avoidLastN", "Avoid Last N");
                    SafePropToggle(_so, "loopSequence", "Loop Sequence");
                    SafePropToggle(_so, "allowRepeatIfNoEligible", "Allow Repeat If No Eligible");
                    SafePropToggle(_so, "skipNullEntries", "Skip Null Entries");
                }

                EditorGUILayout.Space(6);

                // Fixed sequence (array)
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Fixed Sequence", EditorStyles.miniBoldLabel);
                    var pSeq = NarrativeLinkerGUI.FindPropAny(_so, "_sequence", "sequence", "m_Sequence");
                    if (pSeq != null) EditorGUILayout.PropertyField(pSeq, true);
                    else EditorGUILayout.HelpBox("Property '_sequence' not found.", MessageType.None);
                }

                EditorGUILayout.Space(6);

                // Random pool (array)
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Random Weighted Pool", EditorStyles.miniBoldLabel);
                    var pPool = NarrativeLinkerGUI.FindPropAny(_so, "_pool", "pool", "m_Pool");
                    if (pPool != null) EditorGUILayout.PropertyField(pPool, true);
                    else EditorGUILayout.HelpBox("Property '_pool' not found.", MessageType.None);
                }

                EditorGUILayout.EndScrollView();

                // Footer actions
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button("Save"))
                    {
                        _so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_selected);
                        AssetDatabase.SaveAssets();
                    }

                    if (GUILayout.Button("Revert"))
                    {
                        _so.Update();
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Select in Project", GUILayout.Width(150)))
                        Selection.activeObject = _selected;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private void Select(StoryStackSO s)
        {
            _selected = s;
            _so = (_selected != null) ? new SerializedObject(_selected) : null;
        }

        private void RefreshList()
        {
            _found.Clear();
            var guids = AssetDatabase.FindAssets("t:StoryStackSO");
            foreach (var g in guids)
            {
                var path  = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<StoryStackSO>(path);
                if (asset != null) _found.Add(asset);
            }

            // Keep current selection if still present; otherwise prefer GameData's stack
            var gdStack = GetGameDataStack();
            if (_selected == null && gdStack != null) Select(gdStack);
            else if (_selected == null && _found.Count > 0) Select(_found[0]);
        }

        private StoryStackSO CreateNew()
        {
            // Ensure folder structure exists
            if (!AssetDatabase.IsValidFolder(ModuleRoot))
                AssetDatabase.CreateFolder("Assets", "SimulationGame");
            if (!AssetDatabase.IsValidFolder(DataDir))
                AssetDatabase.CreateFolder(ModuleRoot, "Data");
            if (!AssetDatabase.IsValidFolder(StacksDir))
                AssetDatabase.CreateFolder(DataDir, "StoryStacks");

            var asset = ScriptableObject.CreateInstance<StoryStackSO>();
            var path  = AssetDatabase.GenerateUniqueAssetPath($"{StacksDir}/StoryStack.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(asset);
            Select(asset);
            RefreshList();
            return asset;
        }

        private void ResolveGameDataProps()
        {
            _pStoryStack = null;
            if (_gdSO == null) return;

            _gdSO.Update();
            _pStoryStack = NarrativeLinkerGUI.FindPropAny(
            _gdSO,
            "DefaultStoryStack",              // ← public field in GameData.cs
            "_storyStack", "storyStack", "m_StoryStack",
            "mainStoryStack", "defaultStoryStack"
            );
            _gdSO.ApplyModifiedProperties();
        }

        private StoryStackSO GetGameDataStack()
        {
            if (_gdSO == null || _pStoryStack == null) return null;
            _gdSO.Update();
            var v = _pStoryStack.objectReferenceValue as StoryStackSO;
            _gdSO.ApplyModifiedProperties();
            return v;
        }

        private void SetGameDataStack(StoryStackSO stack)
        {
            if (_gdSO == null || _pStoryStack == null) return;
            _gdSO.Update();
            _pStoryStack.objectReferenceValue = stack;
            _gdSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_gd);
            AssetDatabase.SaveAssets();
            if (stack != null) Select(stack);
        }

        private static void SafePropToggle(SerializedObject so, string nameOrAlt, string label)
        {
            var p = NarrativeLinkerGUI.FindPropAny(so, $"_{nameOrAlt}", nameOrAlt, $"m_{nameOrAlt}");
            if (p != null) p.boolValue = EditorGUILayout.Toggle(label, p.boolValue);
            else EditorGUILayout.HelpBox($"Property '{nameOrAlt}' not found.", MessageType.None);
        }
    }
}
