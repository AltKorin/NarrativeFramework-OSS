// Assets/SimulationGame/Editor/Views/NarrativeLinkerVariableView.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Narrative Variables view for the Narrative Linker window.
    /// - Follows the active GameData from NarrativeLinkerContext (no GameData.Instance anymore).
    /// - Edits variables directly in the window (no need to open the Inspector).
    /// - Uses SerializedObject + tolerant property lookup to be resilient to refactors.
    /// - Lets you add/remove/rename variables in GameData and browse all variables in the project.
    /// </summary>
    public class NarrativeLinkerVariableView
    {
        // Active GameData (from context) + wrapper
        private GameData _gameData;
        private SerializedObject _gdSO;

        private Vector2 scrollPos;

        // Per-asset rename buffers
        private readonly Dictionary<Object, string> renameBuffer = new Dictionary<Object, string>();

        // Project-wide browser
        private List<NarrativeVariableSO> projectVariables = new List<NarrativeVariableSO>();
        private Vector2 projectScroll;
        private string searchQuery = "";
        private bool foldProjectBrowser = true;

        // ─────────────────────────────────────────────────────────
        // Lifecycle (hook into active GameData)
        // ─────────────────────────────────────────────────────────
        public void OnEnable()
        {
            _gameData = NarrativeLinkerContext.GetOrResolve();
            _gdSO     = (_gameData != null) ? new SerializedObject(_gameData) : null;
            NarrativeLinkerContext.Changed += OnActiveGameDataChanged;

            RefreshProjectVariables();
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gameData, gd)) return;

            _gameData = gd;
            _gdSO     = (_gameData != null) ? new SerializedObject(_gameData) : null;

            // No need to reload project variables — they are project-wide.
            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────
        // Main draw
        // ─────────────────────────────────────────────────────────
        public void Draw()
        {
            // Safety sync if view opened after context change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
                OnActiveGameDataChanged(current);

            // No GameData picked
            if (_gameData == null)
            {
                EditorGUILayout.HelpBox("No active GameData selected. Pick one in the Game Data tab.", MessageType.Warning);
                return;
            }

            if (_gdSO == null) _gdSO = new SerializedObject(_gameData);
            _gdSO.Update();
            // Active GameData status header (cosmetic, helpful)
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Active GameData", EditorStyles.boldLabel);
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    EditorGUILayout.ObjectField(_gameData, typeof(GameData), false);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                        EditorGUIUtility.PingObject(_gameData);
                    if (GUILayout.Button("Select", GUILayout.Width(70)))
                        Selection.activeObject = _gameData;
                });
            });
            EditorGUILayout.Space(6);


            // Tolerant lookup of the serialized list of variables in GameData
            // Try constant first (if present), then common fallbacks.
            SerializedProperty varList =
                NarrativeLinkerGUI.FindPropAny(
                    _gdSO,
                    GameData.PROP_VARS,          // if your GameData exposes this const
                    "_narrativeVariables",
                    "narrativeVariables",
                    "m_NarrativeVariables"
                );

            if (varList == null)
            {
                EditorGUILayout.HelpBox(
                    "Serialized property for narrative variables not found " +
                    "(expected GameData.PROP_VARS / _narrativeVariables).",
                    MessageType.Error);
                return;
            }

            // ─────────────────────────────────────────────────────────
            // Project browser (to add existing assets into GameData)
            // ─────────────────────────────────────────────────────────
            EditorGUILayout.Space();
            foldProjectBrowser = EditorGUILayout.Foldout(foldProjectBrowser, "Project Variables Browser", true);
            if (foldProjectBrowser)
            {
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    DrawProjectBrowserToolbar(_gdSO, varList);
                    DrawProjectBrowserList(_gdSO, varList);
                });
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(4);

            // ─────────────────────────────────────────────────────────
            // Variables currently in GameData (inline editable)
            // ─────────────────────────────────────────────────────────
            NarrativeLinkerGUI.Scroll(ref scrollPos, () =>
            {
                EditorGUILayout.LabelField("Narrative Variables (in GameData)", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // Iterate backwards to allow safe removal
                for (int i = varList.arraySize - 1; i >= 0; i--)
                {
                    var element     = varList.GetArrayElementAtIndex(i);
                    Object varAsset = element.objectReferenceValue;

                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        if (varAsset == null)
                        {
                            // Empty slot helper
                            NarrativeLinkerGUI.Horizontal(() =>
                            {
                                EditorGUILayout.LabelField($"Slot {i}: <empty>", EditorStyles.miniBoldLabel);
                                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                                {
                                    varList.DeleteArrayElementAtIndex(i);
                                    _gdSO.ApplyModifiedProperties();
                                    GUIUtility.ExitGUI();
                                }
                            });
                            return;
                        }

                        // Header actions (Ping / Select / Remove)
                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            EditorGUILayout.LabelField(varAsset.name, EditorStyles.boldLabel);
                            if (GUILayout.Button("Ping", GUILayout.Width(50)))  EditorGUIUtility.PingObject(varAsset);
                            if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = varAsset;
                            if (GUILayout.Button("Remove", GUILayout.Width(80)))
                            {
                                varList.DeleteArrayElementAtIndex(i); // only removes from GameData
                                _gdSO.ApplyModifiedProperties();
                                EditorUtility.SetDirty(_gameData);
                                AssetDatabase.SaveAssets();
                                GUIUtility.ExitGUI();
                            }
                        });

                        // Inline rename (asset name)
                        if (!renameBuffer.ContainsKey(varAsset))
                            renameBuffer[varAsset] = varAsset.name;

                        renameBuffer[varAsset] = EditorGUILayout.TextField("Asset Name", renameBuffer[varAsset]);

                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Apply Rename", GUILayout.Width(120)))
                            {
                                string newName = renameBuffer[varAsset];
                                if (!string.IsNullOrWhiteSpace(newName))
                                {
                                    NarrativeLinkerGUI.RenameAsset(varAsset, newName);
                                    renameBuffer[varAsset] = varAsset.name; // sync back to sanitized name
                                }
                            }
                        });

                        EditorGUILayout.Space(4);

                        // Inline full editing of the variable ScriptableObject
                        var varSO = new SerializedObject(varAsset);
                        varSO.Update();

                        // Draw all visible properties except m_Script (keeps custom drawers intact)
                        EditorGUI.indentLevel++;
                        SerializedProperty p = varSO.GetIterator();
                        p.NextVisible(true); // skip m_Script
                        while (p.NextVisible(false))
                            EditorGUILayout.PropertyField(p, true);
                        EditorGUI.indentLevel--;

                        if (varSO.ApplyModifiedProperties())
                        {
                            EditorUtility.SetDirty(varAsset);
                            AssetDatabase.SaveAssets();
                        }
                    });

                    EditorGUILayout.Space(6);
                }

                EditorGUILayout.Space(8);

                // Create a brand-new variable asset and add it to GameData
                if (GUILayout.Button("Create New Narrative Variable"))
                {
                    CreateNewVariableAndAddToList(_gdSO, varList);
                }

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Tips:\n• Edit any variable directly here (Id, Title, Description, Type, initial values, etc.).\n" +
                    "• 'Remove' only removes the reference from GameData (it does not delete the asset).\n" +
                    "• Use the Project Variables Browser to quickly add existing variables to GameData.",
                    MessageType.Info);
            });

            _gdSO.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────
        // Project browser — toolbar
        // ─────────────────────────────────────────────────────────
        private void DrawProjectBrowserToolbar(SerializedObject gameDataSO, SerializedProperty varList)
        {
            NarrativeLinkerGUI.Horizontal(() =>
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                    RefreshProjectVariables();

                GUILayout.Space(6);
                GUILayout.Label("Search:", GUILayout.Width(48));
                string newSearch = GUILayout.TextField(searchQuery);
                if (newSearch != searchQuery) searchQuery = newSearch;

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(projectVariables.Count == 0))
                {
                    if (GUILayout.Button("Add All Missing to GameData", GUILayout.Width(220)))
                    {
                        int added = 0;
                        foreach (var v in projectVariables)
                        {
                            if (v == null) continue;
                            if (IsInGameData(varList, v)) continue;
                            AppendVarRef(varList, v);
                            added++;
                        }
                        if (added > 0)
                        {
                            gameDataSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(gameDataSO.targetObject);
                            AssetDatabase.SaveAssets();
                            RepaintAll();
                        }
                        Debug.Log($"[Variables] Added {added} variables to GameData.");
                    }
                }
            });
        }

        // ─────────────────────────────────────────────────────────
        // Project browser — list
        // ─────────────────────────────────────────────────────────
        private void DrawProjectBrowserList(SerializedObject gameDataSO, SerializedProperty varList)
        {
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                if (projectVariables == null || projectVariables.Count == 0)
                {
                    EditorGUILayout.HelpBox("No NarrativeVariableSO assets found in the project.", MessageType.Info);
                    return;
                }

                projectScroll = EditorGUILayout.BeginScrollView(projectScroll, GUILayout.MinHeight(160), GUILayout.MaxHeight(280));
                foreach (var v in projectVariables)
                {
                    if (v == null) continue;

                    // Case-insensitive contains filter on asset name or VariableName
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        var q = searchQuery.ToLowerInvariant();
                        if (!v.name.ToLowerInvariant().Contains(q) &&
                            !v.VariableName.ToLowerInvariant().Contains(q))
                            continue;
                    }

                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        GUILayout.Label(EditorGUIUtility.ObjectContent(v, typeof(NarrativeVariableSO)).image, GUILayout.Width(20), GUILayout.Height(20));
                        EditorGUILayout.LabelField(
                            $"{v.name}   <i>({v.VariableName}, {v.Type})</i>",
                            new GUIStyle(EditorStyles.label) { richText = true });

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                            EditorGUIUtility.PingObject(v);

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                            Selection.activeObject = v;

                        bool alreadyIn = IsInGameData(varList, v);
                        using (new EditorGUI.DisabledScope(alreadyIn))
                        {
                            string btn = alreadyIn ? "Already in GameData" : "Add to GameData";
                            if (GUILayout.Button(btn, GUILayout.Width(140)))
                            {
                                AppendVarRef(varList, v);
                                gameDataSO.ApplyModifiedProperties();
                                EditorUtility.SetDirty(gameDataSO.targetObject);
                                AssetDatabase.SaveAssets();
                                RepaintAll();
                            }
                        }
                    });
                }
                EditorGUILayout.EndScrollView();
            });
        }

        // ─────────────────────────────────────────────────────────
        // Project browser — backing logic
        // ─────────────────────────────────────────────────────────
        private void RefreshProjectVariables()
        {
            projectVariables.Clear();
            string[] guids = AssetDatabase.FindAssets("t:NarrativeVariableSO");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var v = AssetDatabase.LoadAssetAtPath<NarrativeVariableSO>(path);
                if (v != null) projectVariables.Add(v);
            }
            projectVariables.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsInGameData(SerializedProperty varList, NarrativeVariableSO v)
        {
            for (int i = 0; i < varList.arraySize; i++)
            {
                var elem = varList.GetArrayElementAtIndex(i);
                if (elem != null && elem.objectReferenceValue == v) return true;
            }
            return false;
        }

        private static void AppendVarRef(SerializedProperty varList, NarrativeVariableSO v)
        {
            int newIndex = varList.arraySize;
            varList.InsertArrayElementAtIndex(newIndex);
            var elem = varList.GetArrayElementAtIndex(newIndex);
            elem.objectReferenceValue = v;
        }

        private static void RepaintAll()
        {
            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────
        // Create-new flow (unchanged behavior)
        // ─────────────────────────────────────────────────────────
        private void CreateNewVariableAndAddToList(SerializedObject gameDataSO, SerializedProperty varList)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Narrative Variable",
                "NewNarrativeVariable",
                "asset",
                "Select a location for the new NarrativeVariableSO asset."
            );
            if (string.IsNullOrEmpty(path)) return;

            var newVariable = ScriptableObject.CreateInstance<NarrativeVariableSO>();
            AssetDatabase.CreateAsset(newVariable, path);
            AssetDatabase.SaveAssets();

            gameDataSO.Update();
            int newIndex = varList.arraySize;
            varList.InsertArrayElementAtIndex(newIndex);
            var elem = varList.GetArrayElementAtIndex(newIndex);
            elem.objectReferenceValue = newVariable;

            gameDataSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(gameDataSO.targetObject);
            AssetDatabase.SaveAssets();

            if (!renameBuffer.ContainsKey(newVariable))
                renameBuffer[newVariable] = newVariable.name;

            EditorGUIUtility.PingObject(newVariable);
            Selection.activeObject = newVariable;

            RefreshProjectVariables();
            RepaintAll();
        }
    }
}
