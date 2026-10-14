using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Story Arc authoring view that follows the currently active GameData
    /// published via NarrativeLinkerContext. It loads arcs from GameData._storyArcs
    /// (fallback: all assets) and updates when the active GameData changes.
    /// </summary>
    public class NarrativeLinkerStoryArcView
    {
        private Vector2 _scrollPos;
        private List<StoryArcSO> _storyArcs;
        private Dictionary<StoryArcSO, bool[]> _foldouts = new();

        // Active GameData (provided by NarrativeLinkerContext)
        private GameData _gameData;
        private SerializedObject _gdSO;

        // ─────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            _gameData = NarrativeLinkerContext.GetOrResolve();
            _gdSO = (_gameData != null) ? new SerializedObject(_gameData) : null;
            NarrativeLinkerContext.Changed += OnActiveGameDataChanged;

            LoadStoryArcsFromActiveGameData();
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gameData, gd)) return;

            _gameData = gd;
            _gdSO = (_gameData != null) ? new SerializedObject(_gameData) : null;

            LoadStoryArcsFromActiveGameData();
        }

        // ─────────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            // Safety sync if view opened after the change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
                OnActiveGameDataChanged(current);

            if (_storyArcs == null)
                LoadStoryArcsFromActiveGameData();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh List"))
            {
                LoadStoryArcsFromActiveGameData();
            }
            using (new EditorGUI.DisabledScope(_gameData == null))
            {
                if (GUILayout.Button("Create New Story Arc"))
                {
                    CreateNewStoryArc();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_gameData == null)
            {
                EditorGUILayout.HelpBox("No active GameData selected. Pick one in the Game Data tab.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_storyArcs != null)
            {
                foreach (var arc in _storyArcs)
                {
                    if (arc == null) continue;

                    if (!_foldouts.ContainsKey(arc))
                        _foldouts[arc] = new bool[5]; // Meta, Story, Window, Conditions, Triggered

                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.BeginHorizontal();
                    arc.name = EditorGUILayout.TextField("Asset Name", arc.name);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = arc;
                    }
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Story Arc?",
                            "Are you sure you want to delete " + arc.name + "?", "Yes", "No"))
                        {
                            // Remove from GameData list if present
                            RemoveArcFromActiveGameDataList(arc);

                            string path = AssetDatabase.GetAssetPath(arc);
                            AssetDatabase.DeleteAsset(path);
                            AssetDatabase.SaveAssets();
                            LoadStoryArcsFromActiveGameData();
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    // 1. Meta
                    _foldouts[arc][0] = EditorGUILayout.Foldout(_foldouts[arc][0], "Meta", true);
                    if (_foldouts[arc][0])
                    {
                        arc.SetArcDescription(EditorGUILayout.TextField("Arc Name", arc.ArcName));

                        var so = new SerializedObject(arc);
                        // tolerant lookup for private fields
                        var pDesc = NarrativeLinkerGUI.FindPropAny(so, "_arcDescription", "arcDescription", "m_ArcDescription");
                        var pIllu = NarrativeLinkerGUI.FindPropAny(so, "_arcIllustration", "arcIllustration", "m_ArcIllustration");

                        if (pDesc != null)
                            pDesc.stringValue = EditorGUILayout.TextArea(arc.ArcDescription, GUILayout.MinHeight(40));
                        else
                            EditorGUILayout.HelpBox("Missing property: Arc Description", MessageType.None);

                        if (pIllu != null)
                            pIllu.objectReferenceValue = EditorGUILayout.ObjectField("Illustration", arc.ArcIllustration, typeof(Sprite), false);
                        else
                            EditorGUILayout.HelpBox("Missing property: Arc Illustration", MessageType.None);

                        so.ApplyModifiedProperties();
                    }

                    // 2. Story Content
                    _foldouts[arc][1] = EditorGUILayout.Foldout(_foldouts[arc][1], "Story Content", true);
                    if (_foldouts[arc][1])
                    {
                        var so = new SerializedObject(arc);

                        var pAllowMulti = NarrativeLinkerGUI.FindPropAny(so, "_allowMultipleActivations", "allowMultipleActivations");
                        if (pAllowMulti != null)
                            pAllowMulti.boolValue = EditorGUILayout.Toggle("Allow Multiple Activations", pAllowMulti.boolValue);
                        else
                            EditorGUILayout.HelpBox("Missing property: AllowMultipleActivations", MessageType.None);

                        var eventsProp = NarrativeLinkerGUI.FindPropAny(so, "_eventsInArc", "eventsInArc");
                        DrawList<SimEventSO>(eventsProp, "Events In Arc");

                        // Event ID helper (needs GameData)
                        DrawEventIdTools(eventsProp);

                        so.ApplyModifiedProperties();
                    }

                    // 3. Activation Window
                    _foldouts[arc][2] = EditorGUILayout.Foldout(_foldouts[arc][2], "Activation Window", true);
                    if (_foldouts[arc][2])
                    {
                        arc.SetMinDay(EditorGUILayout.IntField("Min Day", arc.MinDay));
                        arc.SetMaxDay(EditorGUILayout.IntField("Max Day", arc.MaxDay));
                    }

                    // 4. Conditions
                    _foldouts[arc][3] = EditorGUILayout.Foldout(_foldouts[arc][3], "Conditions", true);
                    if (_foldouts[arc][3])
                    {
                        var so = new SerializedObject(arc);

                        DrawList<ActorSO>(NarrativeLinkerGUI.FindPropAny(so, "_involvedActors", "involvedActors"), "Involved Actors");
                        DrawStringList(NarrativeLinkerGUI.FindPropAny(so, "_requiredFlags", "requiredFlags"), "Required Flags");
                        DrawStringList(NarrativeLinkerGUI.FindPropAny(so, "_forbiddenFlags", "forbiddenFlags"), "Forbidden Flags");
                        DrawList<SimEventSO>(NarrativeLinkerGUI.FindPropAny(so, "_requiredEvents", "requiredEvents"), "Required Events");
                        DrawClassList(NarrativeLinkerGUI.FindPropAny(so, "_additionalConditions", "additionalConditions"), "Additional Conditions");

                        so.ApplyModifiedProperties();
                    }

                    // 5. Triggered Event
                    _foldouts[arc][4] = EditorGUILayout.Foldout(_foldouts[arc][4], "Triggered Event", true);
                    if (_foldouts[arc][4])
                    {
                        var so = new SerializedObject(arc);
                        var pTrig = NarrativeLinkerGUI.FindPropAny(so, "_triggeredEvent", "triggeredEvent", "m_TriggeredEvent");
                        if (pTrig != null)
                            pTrig.objectReferenceValue = EditorGUILayout.ObjectField("Triggered Event", pTrig.objectReferenceValue, typeof(SimEventSO), false);
                        else
                            EditorGUILayout.HelpBox("Missing property: Triggered Event", MessageType.None);

                        so.ApplyModifiedProperties();
                    }

                    if (GUI.changed)
                    {
                        EditorUtility.SetDirty(arc);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────
        // Data loading / creation
        // ─────────────────────────────────────────────────────────────
        private void LoadStoryArcsFromActiveGameData()
        {
            // If we have an active GameData, load arcs from its serialized list (_storyArcs).
            if (_gameData != null)
            {
                _gdSO = new SerializedObject(_gameData);
                var arcsProp = NarrativeLinkerGUI.FindPropAny(_gdSO, "_storyArcs", "storyArcs", "m_StoryArcs");

                if (arcsProp != null && arcsProp.isArray)
                {
                    _storyArcs = new List<StoryArcSO>(arcsProp.arraySize);
                    for (int i = 0; i < arcsProp.arraySize; i++)
                    {
                        var elem = arcsProp.GetArrayElementAtIndex(i).objectReferenceValue as StoryArcSO;
                        if (elem != null) _storyArcs.Add(elem);
                    }
                    return;
                }
            }

            // Fallback: list all arcs in the project (older setups)
            _storyArcs = new List<StoryArcSO>();
            string[] guids = AssetDatabase.FindAssets("t:StoryArcSO");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var arc = AssetDatabase.LoadAssetAtPath<StoryArcSO>(path);
                if (arc != null) _storyArcs.Add(arc);
            }
        }

        private void CreateNewStoryArc()
        {
            var arc = ScriptableObject.CreateInstance<StoryArcSO>();
            string path = EditorUtility.SaveFilePanelInProject("Save New Story Arc", "NewStoryArc", "asset", "Select location to save");
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(arc, path);
            AssetDatabase.SaveAssets();

            // If we have active GameData, add the new arc to its _storyArcs list.
            if (_gameData != null)
            {
                var arcsProp = NarrativeLinkerGUI.FindPropAny(_gdSO ?? new SerializedObject(_gameData), "_storyArcs", "storyArcs", "m_StoryArcs");
                if (arcsProp != null && arcsProp.isArray)
                {
                    int index = arcsProp.arraySize;
                    arcsProp.InsertArrayElementAtIndex(index);
                    arcsProp.GetArrayElementAtIndex(index).objectReferenceValue = arc;
                    _gdSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_gameData);
                }
            }

            LoadStoryArcsFromActiveGameData();
            Selection.activeObject = arc;
        }

        private void RemoveArcFromActiveGameDataList(StoryArcSO arc)
        {
            if (_gameData == null || arc == null) return;

            var arcsProp = NarrativeLinkerGUI.FindPropAny(_gdSO ?? new SerializedObject(_gameData), "_storyArcs", "storyArcs", "m_StoryArcs");
            if (arcsProp == null || !arcsProp.isArray) return;

            for (int i = 0; i < arcsProp.arraySize; i++)
            {
                var elem = arcsProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue == arc)
                {
                    elem.objectReferenceValue = null;
                    arcsProp.DeleteArrayElementAtIndex(i);
                    _gdSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_gameData);
                    break;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Small UI helpers (with null-guards)
        // ─────────────────────────────────────────────────────────────
        private void DrawList<T>(SerializedProperty listProp, string label) where T : UnityEngine.Object
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (listProp == null)
            {
                EditorGUILayout.HelpBox($"Missing property for list '{label}'.", MessageType.None);
                return;
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                listProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    EditorGUILayout.ObjectField(listProp.GetArrayElementAtIndex(i).objectReferenceValue, typeof(T), false);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add " + typeof(T).Name))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
            }
        }

        private void DrawStringList(SerializedProperty listProp, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (listProp == null)
            {
                EditorGUILayout.HelpBox($"Missing property for list '{label}'.", MessageType.None);
                return;
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                listProp.GetArrayElementAtIndex(i).stringValue =
                    EditorGUILayout.TextField(listProp.GetArrayElementAtIndex(i).stringValue);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add String"))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
            }
        }

        private void DrawClassList(SerializedProperty listProp, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (listProp == null)
            {
                EditorGUILayout.HelpBox($"Missing property for list '{label}'.", MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(element, new GUIContent("Condition " + (i + 1)), true);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
            }
            if (GUILayout.Button("+ Add Condition"))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
            }
            EditorGUI.indentLevel--;
        }

        // ─────────────────────────────────────────────────────────────
        // Event ID helpers for events listed in this arc
        // ─────────────────────────────────────────────────────────────
        private void DrawEventIdTools(SerializedProperty eventsProp)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Event IDs", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(_gameData == null || eventsProp == null))
                    {
                        if (GUILayout.Button("Assign IDs to missing events", GUILayout.Width(220)))
                        {
                            AssignMissingEventIds(eventsProp);
                        }
                    }
                }

                EditorGUILayout.LabelField(
                    _gameData == null
                        ? "GameData not found – cannot allocate sequential IDs."
                        : (eventsProp == null
                            ? "Events property not found on this arc."
                            : "Assigns EVT_<n> only to events with empty simEventId."),
                    EditorStyles.miniLabel);
            }
        }

        private void AssignMissingEventIds(SerializedProperty eventsProp)
        {
            if (_gameData == null || eventsProp == null) return;

            bool changed = false;

            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                var ev = eventsProp.GetArrayElementAtIndex(i).objectReferenceValue as SimEventSO;
                if (ev == null) continue;

                if (string.IsNullOrEmpty(ev.simEventId))
                {
                    int next = _gameData.IncrementEventId();
                    ev.simEventId = $"EVT_{next}";
                    EditorUtility.SetDirty(ev);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(_gameData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[StoryArcView] Assigned missing simEventId to events in arc.");
            }
        }
    }
}
