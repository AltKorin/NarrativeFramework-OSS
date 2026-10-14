// Assets/SimulationGame/Editor/EndingsDebuggerWindow.cs
// Minimal, non-breaking Ending Rules panel for designers.
// - Lists and edits GameData.ResourceFailRules (priority = list order).
// - Dry-run: predicts which ending would trigger for a given resource value.
// - Utilities: select/ping assets, create missing Ending events, validate rules.
//
// This window DOES NOT modify runtime code and DOES NOT depend on internal fields.
// It only uses public fields already present in GameData and SimEventSO.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimulationGame.EditorTools
{
    public class EndingsDebuggerWindow : EditorWindow
    {
        private GameData _gameData;
        private SerializedObject _soGameData;
        private SerializedProperty _propRules;
        private SerializedProperty _propDefaultEnding;

        // Dry-run tester state
        private Resource _testResource;
        private float _testValue = 0f;
        private string _dryRunMsg = "";

        // UI
        private Vector2 _scroll;

        [MenuItem("Tools/Narrative Framework/Endings Debugger")]
        public static void Open()
        {
            var win = GetWindow<EndingsDebuggerWindow>("Endings");
            win.minSize = new Vector2(560, 420);
            win.Show();
        }

        private void OnEnable()
        {
            // Try to use the canonical singleton if available
            _gameData = GameData.Instance;
            if (_gameData != null)
            {
                _soGameData = new SerializedObject(_gameData);
                _propRules = _soGameData.FindProperty("_resourceFailRules");
                _propDefaultEnding = _soGameData.FindProperty("_defaultGameOverEvent");
            }
        }

        private void OnGUI()
        {
            if (_gameData == null)
            {
                EditorGUILayout.HelpBox("GameData.asset not found in a Resources folder. Create/assign one to use this window.", MessageType.Warning);
                if (GUILayout.Button("Select Resources folders"))
                {
                    var ids = AssetDatabase.FindAssets("t:DefaultAsset Resources");
                    if (ids != null && ids.Length > 0)
                        Selection.objects = GUIDsToObjects(ids);
                }
                return;
            }

            if (_soGameData == null)
            {
                _soGameData = new SerializedObject(_gameData);
                _propRules = _soGameData.FindProperty("_resourceFailRules");
                _propDefaultEnding = _soGameData.FindProperty("_defaultGameOverEvent");
            }

            _soGameData.Update();

            DrawHeader();
            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRulesList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            EditorGUILayout.Separator();
            DrawDryRunTester();

            _soGameData.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("GameData", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(_gameData, typeof(GameData), false);
                }

                if (GUILayout.Button("Select", GUILayout.Width(70)))
                    Selection.activeObject = _gameData;

                if (GUILayout.Button("Ping", GUILayout.Width(60)))
                    EditorGUIUtility.PingObject(_gameData);
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_propDefaultEnding, new GUIContent("Default Game Over Ending"));
                EditorGUILayout.LabelField("Priority", "Rules are evaluated top-to-bottom; first match wins.", EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate Rules"))
                        ValidateRules();
                    if (GUILayout.Button("Sort by Resource Name (A→Z)"))
                        SortRulesByResourceName();
                    if (GUILayout.Button("Add Rule"))
                    {
                        _propRules.arraySize++;
                    }
                }
            }
        }

        private void DrawRulesList()
        {
            if (_propRules == null)
            {
                EditorGUILayout.HelpBox("GameData has no _resourceFailRules field.", MessageType.Info);
                return;
            }

            int removeAt = -1;

            for (int i = 0; i < _propRules.arraySize; i++)
            {
                var element = _propRules.GetArrayElementAtIndex(i);
                if (element == null) continue;

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Rule #{i + 1}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("▲", GUILayout.Width(26)) && i > 0)
                            _propRules.MoveArrayElement(i, i - 1);
                        if (GUILayout.Button("▼", GUILayout.Width(26)) && i < _propRules.arraySize - 1)
                            _propRules.MoveArrayElement(i, i + 1);
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                            removeAt = i;
                    }

                    EditorGUILayout.Space(2);

                    var spEnabled       = element.FindPropertyRelative("Enabled");
                    var spResource      = element.FindPropertyRelative("Resource");
                    var spThreshold     = element.FindPropertyRelative("Threshold");
                    var spEnding        = element.FindPropertyRelative("SpecificEnding");
                    var spFlagOnFail    = element.FindPropertyRelative("FlagOnFail");
                    var spMsgOverride   = element.FindPropertyRelative("MessageOverride");

                    EditorGUILayout.PropertyField(spEnabled);
                    EditorGUILayout.PropertyField(spResource);
                    EditorGUILayout.PropertyField(spThreshold);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(spEnding);
                        if (GUILayout.Button("New Ending", GUILayout.Width(100)))
                        {
                            CreateNewEndingForRule(spEnding, spResource);
                        }
                    }

                    EditorGUILayout.PropertyField(spFlagOnFail);
                    EditorGUILayout.PropertyField(spMsgOverride);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (spResource.objectReferenceValue != null)
                        {
                            if (GUILayout.Button("Select Resource"))
                                Selection.activeObject = spResource.objectReferenceValue;
                            if (GUILayout.Button("Ping Resource"))
                                EditorGUIUtility.PingObject(spResource.objectReferenceValue);
                        }

                        if (spEnding.objectReferenceValue != null)
                        {
                            if (GUILayout.Button("Select Ending"))
                                Selection.activeObject = spEnding.objectReferenceValue;
                            if (GUILayout.Button("Ping Ending"))
                                EditorGUIUtility.PingObject(spEnding.objectReferenceValue);
                        }

                        GUILayout.FlexibleSpace();

                        // Quick prediction message (editor-time) based on current fields
                        EditorGUILayout.LabelField(GetRuleQuickStatus(spEnabled.boolValue, spResource.objectReferenceValue, spEnding.objectReferenceValue),
                            EditorStyles.miniLabel, GUILayout.Width(280));
                    }
                }
            }

            if (removeAt >= 0)
                _propRules.DeleteArrayElementAtIndex(removeAt);
        }

        private void DrawDryRunTester()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Dry-Run Tester (Editor-time prediction)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Predicts which ending would trigger if the chosen resource reached the test value.\n" +
                    "This does not modify runtime state. The order of rules determines priority.", MessageType.Info);

                _testResource = (Resource)EditorGUILayout.ObjectField("Resource (SO)", _testResource, typeof(Resource), false);
                _testValue    = EditorGUILayout.FloatField("Test Value", _testValue);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Run Prediction"))
                    {
                        _dryRunMsg = PredictEndingMessage(_testResource, _testValue, out var _);
                    }

                    if (GUILayout.Button("Clear"))
                    {
                        _dryRunMsg = "";
                        _testResource = null;
                        _testValue = 0f;
                    }
                }

                if (!string.IsNullOrEmpty(_dryRunMsg))
                {
                    EditorGUILayout.HelpBox(_dryRunMsg, MessageType.None);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private string GetRuleQuickStatus(bool enabled, Object resource, Object ending)
        {
            if (!enabled) return "Disabled";
            if (resource == null) return "⚠ No Resource assigned";
            if (ending == null && _propDefaultEnding.objectReferenceValue == null)
                return "⚠ No SpecificEnding and no DefaultGameOver set";
            if (ending == null)
                return "Uses Default Game Over";
            return "OK";
        }

        private string PredictEndingMessage(Resource resource, float testValue, out SimEventSO chosen)
        {
            chosen = null;

            if (_gameData == null) return "No GameData.";
            if (!_gameData.EnableResourceFailStates) return "Resource fail states are disabled in GameData.";
            if (resource == null) return "Pick a Resource.";

            var rules = _gameData.ResourceFailRules;
            if (rules == null || rules.Count == 0) return "No rules configured.";

            // Emulate runtime selection: first rule whose resource matches and value <= threshold wins
            foreach (var rule in rules)
            {
                if (rule == null || !rule.Enabled || rule.Resource == null) continue;
                if (rule.Resource == resource && testValue <= rule.Threshold)
                {
                    chosen = rule.SpecificEnding != null ? rule.SpecificEnding : _gameData.DefaultGameOverEvent;
                    var name = resource.name;
                    var info = rule.SpecificEnding != null
                        ? $"will trigger SpecificEnding: {rule.SpecificEnding.name}"
                        : (_gameData.DefaultGameOverEvent != null
                            ? $"will trigger DefaultGameOver: {_gameData.DefaultGameOverEvent.name}"
                            : "no ending assigned (neither SpecificEnding nor DefaultGameOver).");
                    return $"Rule match for '{name}' (value {testValue} <= threshold {rule.Threshold}) → {info}";
                }
            }

            return "No rule matched (value above all thresholds or different resource).";
        }

        private void CreateNewEndingForRule(SerializedProperty spEnding, SerializedProperty spResource)
        {
            var res = spResource != null ? spResource.objectReferenceValue as Resource : null;

            string defaultName = res != null ? $"Ending_{res.name}" : "Ending_New";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Ending SimEvent",
                defaultName,
                "asset",
                "Pick a location and name for the new Ending event.");

            if (string.IsNullOrEmpty(path)) return;

            // Create a new SimEventSO configured as an Ending
            var ending = ScriptableObject.CreateInstance<SimEventSO>();
            // Best-effort init – do not assume private fields; rely on public setters if any.
            // If SimEventSO has exposed helpers, prefer them; otherwise we leave defaults.
            // We at least enforce the asset name to carry info.
            ending.name = System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(ending, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            spEnding.objectReferenceValue = ending;
            _soGameData.ApplyModifiedProperties();
            Selection.activeObject = ending;
            EditorGUIUtility.PingObject(ending);

            EditorUtility.DisplayDialog("Ending Created", $"Created SimEventSO: {ending.name}\nAssign details in the Inspector.", "OK");
        }

        private void ValidateRules()
        {
            var msgs = new List<string>();

            if (_gameData.DefaultGameOverEvent == null)
                msgs.Add("• Default Game Over Ending is not set (rules without SpecificEnding will have no fallback).");

            var seen = new HashSet<Resource>();
            foreach (var r in _gameData.ResourceFailRules)
            {
                if (r == null) continue;
                if (!r.Enabled) continue;
                if (r.Resource == null) { msgs.Add("• A rule is enabled but has no Resource assigned."); continue; }

                if (!seen.Add(r.Resource))
                    msgs.Add($"• Multiple enabled rules reference the same Resource '{r.Resource.name}'. " +
                             "Only the first matching rule will be used at runtime.");

                // Threshold note (not an error)
                if (r.Threshold > 0f && r.SpecificEnding == null && _gameData.DefaultGameOverEvent == null)
                    msgs.Add($"• Rule for '{r.Resource.name}' has Threshold {r.Threshold} but no SpecificEnding and no DefaultGameOver.");
            }

            if (msgs.Count == 0)
                EditorUtility.DisplayDialog("Validation", "No issues found. ✔", "OK");
            else
                EditorUtility.DisplayDialog("Validation", string.Join("\n", msgs), "OK");
        }

        private void SortRulesByResourceName()
        {
            if (_propRules == null) return;

            // Extract to temp list for sorting
            var temp = new List<(string name, SerializedProperty element)>();
            for (int i = 0; i < _propRules.arraySize; i++)
            {
                var el = _propRules.GetArrayElementAtIndex(i);
                var spRes = el.FindPropertyRelative("Resource");
                var obj = spRes != null ? spRes.objectReferenceValue as Resource : null;
                var name = obj != null ? obj.name : "~";
                temp.Add((name, el.Copy()));
            }

            temp.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            // Rebuild array in sorted order
            _propRules.ClearArray();
            foreach (var t in temp)
            {
                _propRules.arraySize++;
                _propRules.GetArrayElementAtIndex(_propRules.arraySize - 1).SerializedObjectCopyFrom(t.element);
            }

            _soGameData.ApplyModifiedProperties();
        }

        // Utility to convert GUIDs to objects
        private static Object[] GUIDsToObjects(string[] guids)
        {
            var list = new List<Object>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var o = AssetDatabase.LoadAssetAtPath<Object>(p);
                if (o) list.Add(o);
            }
            return list.ToArray();
        }
    }

    internal static class SerializedPropertyExtensions
    {
        // Copies values from source property into target property (best-effort).
        // Unity doesn't expose a direct copy API for SerializedProperty elements,
        // so we copy field-by-field for known subfields of ResourceFailRule.
        public static void SerializedObjectCopyFrom(this SerializedProperty target, SerializedProperty source)
        {
            if (target == null || source == null) return;

            // List element structure:
            //   .Resource (ObjectReference)
            //   .Enabled (bool)
            //   .Threshold (float)
            //   .SpecificEnding (ObjectReference)
            //   .FlagOnFail (string)
            //   .MessageOverride (string)

            target.FindPropertyRelative("Enabled").boolValue =
                source.FindPropertyRelative("Enabled").boolValue;

            target.FindPropertyRelative("Resource").objectReferenceValue =
                source.FindPropertyRelative("Resource").objectReferenceValue;

            target.FindPropertyRelative("Threshold").floatValue =
                source.FindPropertyRelative("Threshold").floatValue;

            target.FindPropertyRelative("SpecificEnding").objectReferenceValue =
                source.FindPropertyRelative("SpecificEnding").objectReferenceValue;

            target.FindPropertyRelative("FlagOnFail").stringValue =
                source.FindPropertyRelative("FlagOnFail").stringValue;

            target.FindPropertyRelative("MessageOverride").stringValue =
                source.FindPropertyRelative("MessageOverride").stringValue;
        }
    }
}
#endif
