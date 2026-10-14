using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Editor view for managing Actors and Actor Types inside the Narrative Linker Window.
    /// Uses NarrativeLinkerContext to follow the currently active GameData (no GameData.Instance).
    /// Tolerant property lookup via NarrativeLinkerGUI.FindPropAny to survive field renames.
    /// </summary>
    public class NarrativeLinkerActorView
    {
        // Active GameData from the shared context
        private GameData _gameData;
        private SerializedObject _gdSO;
        private SerializedProperty _pActors; // reference list inside GameData

        // UI state
        private Vector2 scrollPos;
        private ActorTypeSO[] actorTypes;
        private string[] actorTypeOptions;
        private int selectedActorTypeIndex = 0;

        private string newActorName = "New Actor";
        private readonly Dictionary<string, string> attributeValues = new();

        // New ActorType creation fields
        private string newTypeName = "NewType";
        private readonly List<string> newAttributes = new();

        private bool initialized = false;

        // ─────────────────────────────────────────────────────────────
        // Lifecycle (hook into active GameData)
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            if (!initialized)
            {
                _gameData = NarrativeLinkerContext.GetOrResolve();
                _gdSO     = (_gameData != null) ? new SerializedObject(_gameData) : null;
                ResolveGameDataProps();
                LoadActorTypes();
                NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
                initialized = true;
            }
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
            ResolveGameDataProps();

            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────────
        // Main draw
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            // Safety sync if view opened after a context change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
                OnActiveGameDataChanged(current);

            if (_gameData == null)
            {
                EditorGUILayout.HelpBox("No active GameData selected. Pick one in the Game Data tab.", MessageType.Warning);
                return;
            }

            if (_gdSO == null) _gdSO = new SerializedObject(_gameData);
            _gdSO.Update();
            ResolveGameDataProps();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // List all current actors in GameData
            if (_pActors != null && _pActors.isArray)
            {
                for (int i = 0; i < _pActors.arraySize; i++)
                {
                    var element = _pActors.GetArrayElementAtIndex(i);
                    var actor = element.objectReferenceValue as ActorSO;

                    EditorGUILayout.BeginVertical("box");

                    if (actor == null)
                    {
                        EditorGUILayout.LabelField($"Slot {i}: <empty>", EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove Slot", GUILayout.Width(100)))
                        {
                            _pActors.DeleteArrayElementAtIndex(i);
                            _gdSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_gameData);
                            AssetDatabase.SaveAssets();
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.EndVertical();
                        continue;
                    }

                    // Header with name and actions
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(actor.ActorName, EditorStyles.boldLabel);

                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(actor);

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = actor;

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        _pActors.DeleteArrayElementAtIndex(i);
                        if (i < _pActors.arraySize && _pActors.GetArrayElementAtIndex(i).objectReferenceValue != null)
                            _pActors.DeleteArrayElementAtIndex(i);

                        _gdSO.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_gameData);
                        AssetDatabase.SaveAssets();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        GUIUtility.ExitGUI();
                    }
                    else
                    {
                        EditorGUILayout.EndHorizontal();

                        // Inline edit: name + type
                        var actorSO = new SerializedObject(actor);
                        actorSO.Update();

                        EditorGUILayout.PropertyField(FindPropAny(actorSO, "_actorName", "actorName", "m_ActorName"));
                        EditorGUILayout.PropertyField(FindPropAny(actorSO, "_actorType", "actorType", "m_ActorType"));

                        // Attributes (draw raw array for flexibility)
                        var attributesProp = FindPropAny(actorSO, "_attributes", "attributes", "m_Attributes");
                        if (attributesProp != null)
                            EditorGUILayout.PropertyField(attributesProp, new GUIContent("Attributes"), true);
                        else
                            EditorGUILayout.HelpBox("Attribute list not found on ActorSO.", MessageType.None);

                        if (actorSO.ApplyModifiedProperties())
                        {
                            EditorUtility.SetDirty(actor);
                            AssetDatabase.SaveAssets();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(4);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Actors list not found on GameData (expected _actors/actors).", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Create New Actor", EditorStyles.boldLabel);

            newActorName = EditorGUILayout.TextField("Name", newActorName);

            if (actorTypeOptions == null || actorTypeOptions.Length == 0)
            {
                EditorGUILayout.HelpBox("No Actor Types found. Please create at least one ActorType asset.", MessageType.Warning);
            }
            else
            {
                selectedActorTypeIndex = Mathf.Clamp(selectedActorTypeIndex, 0, actorTypeOptions.Length - 1);
                selectedActorTypeIndex = EditorGUILayout.Popup("Type", selectedActorTypeIndex, actorTypeOptions);

                var selectedType = actorTypes[selectedActorTypeIndex];
                if (selectedType != null && selectedType.Attributes != null)
                {
                    foreach (var attr in selectedType.Attributes)
                    {
                        if (!attributeValues.ContainsKey(attr))
                            attributeValues[attr] = "";
                        attributeValues[attr] = EditorGUILayout.TextField(attr, attributeValues[attr]);
                    }
                }

                using (new EditorGUI.DisabledScope(_pActors == null))
                {
                    if (GUILayout.Button("Create Actor"))
                    {
                        CreateActor(newActorName, selectedType);
                    }
                }
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Create New Actor Type", EditorStyles.boldLabel);

            newTypeName = EditorGUILayout.TextField("Type Name", newTypeName);

            for (int i = 0; i < newAttributes.Count; i++)
                newAttributes[i] = EditorGUILayout.TextField($"Attribute {i + 1}", newAttributes[i]);

            if (GUILayout.Button("Add Attribute"))
                newAttributes.Add("");

            if (GUILayout.Button("Create Actor Type"))
                CreateActorType(newTypeName, newAttributes);

            _gdSO.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────
        // Creation helpers
        // ─────────────────────────────────────────────────────────────
        private void CreateActor(string name, ActorTypeSO type)
        {
            if (_gameData == null || _gdSO == null || _pActors == null)
            {
                EditorUtility.DisplayDialog("Error", "Active GameData or actors list is not available.", "OK");
                return;
            }

            string folderPath = "Assets/SimulationGame/Data/Actors";
            EnsureFolder(folderPath);

            var actor = ScriptableObject.CreateInstance<ActorSO>();
            var serialized = new SerializedObject(actor);

            var pName = FindPropAny(serialized, "_actorName", "actorName", "m_ActorName");
            var pType = FindPropAny(serialized, "_actorType", "actorType", "m_ActorType");
            if (pName != null) pName.stringValue = name;
            if (pType != null) pType.objectReferenceValue = type;

            // Fill attributes in the order defined by the ActorType
            var attributesProp = FindPropAny(serialized, "_attributes", "attributes", "m_Attributes");
            if (attributesProp != null)
            {
                attributesProp.ClearArray();
                if (type != null && type.Attributes != null)
                {
                    for (int i = 0; i < type.Attributes.Count; i++)
                    {
                        string attrKey = type.Attributes[i];
                        attributesProp.InsertArrayElementAtIndex(i);
                        var attr = attributesProp.GetArrayElementAtIndex(i);
                        // try common field names: Key/Value or name/value
                        var pKey = FindRelativePropAny(attr, "Key", "key", "Name", "name");
                        var pVal = FindRelativePropAny(attr, "Value", "value");
                        if (pKey != null) pKey.stringValue = attrKey;
                        if (pVal != null) pVal.stringValue = attributeValues.TryGetValue(attrKey, out var val) ? val : "";
                    }
                }
            }

            serialized.ApplyModifiedProperties();

            string safeName = string.IsNullOrWhiteSpace(name) ? "Actor" : name.Trim();
            string assetPath = $"{folderPath}/{FileNameSafe(safeName)}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(actor, assetPath);
            AssetDatabase.SaveAssets();

            // Append to GameData actors list
            _gdSO.Update();
            int newIndex = _pActors.arraySize;
            _pActors.InsertArrayElementAtIndex(newIndex);
            _pActors.GetArrayElementAtIndex(newIndex).objectReferenceValue = actor;
            _gdSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_gameData);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Actor: {safeName} of type {(type != null ? type.DisplayName : "<none>")}");
            newActorName = "New Actor";
            attributeValues.Clear();

            EditorGUIUtility.PingObject(actor);
            Selection.activeObject = actor;
        }

        private void CreateActorType(string typeName, List<string> attributes)
        {
            var actorType = ScriptableObject.CreateInstance<ActorTypeSO>();
            var so = new SerializedObject(actorType);

            var pName = FindPropAny(so, "_displayName", "displayName", "m_DisplayName");
            var pDesc = FindPropAny(so, "_description", "description", "m_Description");
            var pAttrs = FindPropAny(so, "_attributes", "attributes", "m_Attributes");

            if (pName != null) pName.stringValue = string.IsNullOrWhiteSpace(typeName) ? "NewType" : typeName.Trim();
            if (pDesc != null) pDesc.stringValue = "Description here";
            if (pAttrs != null)
            {
                pAttrs.ClearArray();
                for (int i = 0; i < attributes.Count; i++)
                {
                    pAttrs.InsertArrayElementAtIndex(i);
                    pAttrs.GetArrayElementAtIndex(i).stringValue = attributes[i];
                }
            }

            so.ApplyModifiedProperties();

            string folderPath = "Assets/SimulationGame/Data/ActorTypes";
            EnsureFolder(folderPath);

            string safe = FileNameSafe(typeName);
            string assetPath = $"{folderPath}/{safe}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(actorType, assetPath);
            AssetDatabase.SaveAssets();

            newTypeName = "NewType";
            newAttributes.Clear();
            LoadActorTypes();

            EditorGUIUtility.PingObject(actorType);
            Selection.activeObject = actorType;
        }

        // ─────────────────────────────────────────────────────────────
        // Data loading
        // ─────────────────────────────────────────────────────────────
        private void LoadActorTypes()
        {
            var loaded = new List<ActorTypeSO>();
            var options = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:ActorTypeSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.LoadAssetAtPath<ActorTypeSO>(path);
                if (type != null)
                {
                    loaded.Add(type);
                    options.Add(type.DisplayName);
                }
            }

            actorTypes = loaded.ToArray();
            actorTypeOptions = options.ToArray();

            // Reset selection index if out of bounds
            if (actorTypeOptions != null && actorTypeOptions.Length > 0)
                selectedActorTypeIndex = Mathf.Clamp(selectedActorTypeIndex, 0, actorTypeOptions.Length - 1);
            else
                selectedActorTypeIndex = 0;
        }

        // ─────────────────────────────────────────────────────────────
        // GameData plumbing
        // ─────────────────────────────────────────────────────────────
        private void ResolveGameDataProps()
        {
            _pActors = null;
            if (_gdSO == null) return;

            _gdSO.Update();
            // prefer canonical names first, then common fallbacks
            _pActors = NarrativeLinkerGUI.FindPropAny(
                _gdSO,
                "_actors", "actors", "m_Actors", "cast", "Cast"
            );
            _gdSO.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────
        private static SerializedProperty FindPropAny(SerializedObject so, params string[] names)
        {
            return NarrativeLinkerGUI.FindPropAny(so, names);
        }

        private static SerializedProperty FindRelativePropAny(SerializedProperty parent, params string[] names)
        {
            if (parent == null) return null;
            foreach (var n in names)
            {
                var p = parent.FindPropertyRelative(n);
                if (p != null) return p;
            }
            return null;
        }

        private static void EnsureFolder(string fullPath)
        {
            // fullPath e.g. "Assets/SimulationGame/Data/Actors"
            if (AssetDatabase.IsValidFolder(fullPath)) return;

            var parts = fullPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string FileNameSafe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Asset";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c.ToString(), "_");
            return s.Trim();
        }
    }
}
