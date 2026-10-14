using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace SimulationGame.Editor
{
    public class NarrativeDebugWindow : EditorWindow
    {
        private List<ScriptableObject> _variables = new();
        private List<ScriptableObject> _resources = new();
        private List<ScriptableObject> _events = new();
        private List<ScriptableObject> _storyArcs = new();
        private List<ScriptableObject> _actors = new();

        // Foldout state
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>
        {
            { "Narrative Variables", true },
            { "Resources",           true },
            { "Events",              true },
            { "Story Arcs",          true },
            { "Actors",              true }
        };

        private Vector2 _scrollPos;

        [MenuItem("Tools/Narrative Framework/Debug/Narrative Asset Explorer")]
        public static void ShowWindow()
        {
            GetWindow<NarrativeDebugWindow>("Narrative Asset Explorer");
        }

        private void OnEnable() => Refresh();

        private void OnGUI()
        {
            if (GUILayout.Button("⟳ Refresh"))
                Refresh();

            NarrativeLinkerGUI.Scroll(ref _scrollPos, () =>
        {
            DrawSection("Narrative Variables", _variables);
            DrawSection("Resources", _resources);
            DrawSection("Events", _events);
            DrawSection("Story Arcs", _storyArcs);
            DrawSection("Actors", _actors);
        });
        }

        // Tries multiple serialized names (e.g., "Events", "_events", "simEvents")
        public static SerializedProperty FindPropAny(SerializedObject so, params string[] candidates)
        {
            if (so == null) return null;
            foreach (var c in candidates)
            {
                if (string.IsNullOrEmpty(c)) continue;
                var p = so.FindProperty(c);
                if (p != null) return p;
            }
            return null;
        }

        public static void Box(Action body)
        {
            using (new EditorGUILayout.VerticalScope("box"))
                body?.Invoke();
        }

        public static void Row(Action body)
        {
            using (new EditorGUILayout.HorizontalScope())
                body?.Invoke();
        }



        private void Refresh()
        {
            _variables = LoadAssetsByType("NarrativeVariableSO");
            _resources = LoadAssetsByType("Resource");
            _events = LoadAssetsByType("SimEventSO");
            _storyArcs = LoadAssetsByType("StoryArcSO");
            _actors = LoadAssetsByType("ActorSO");

            // Defensive: prune destroyed/null refs (prevents MissingReferenceException)
            PruneNulls(_variables);
            PruneNulls(_resources);
            PruneNulls(_events);
            PruneNulls(_storyArcs);
            PruneNulls(_actors);
        }

        private static List<ScriptableObject> LoadAssetsByType(string typeName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeName}");
            var list = new List<ScriptableObject>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        /// <summary>
        /// Safely draws a foldout section with a simple list. Skips destroyed/null items.
        /// </summary>
        private void DrawSection(string title, List<ScriptableObject> list)
        {
            var state = _foldouts[title];
            if (NarrativeLinkerGUI.Foldout(ref state, $"[{title}]"))
            {
                _foldouts[title] = true;
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    if (list != null && list.Count > 0)
                    {
                        bool foundAny = false;
                        foreach (var item in list)
                        {
                            if (!item) continue; // destroyed/null guard
                            foundAny = true;
                            NarrativeLinkerGUI.Horizontal(() =>
                            {
                                GUILayout.Label($"• {NarrativeLinkerGUI.SafeName(item)}");
                                if (GUILayout.Button("Ping", GUILayout.Width(40)))
                                    EditorGUIUtility.PingObject(item);
                            });
                        }

                        if (!foundAny)
                        {
                            EditorGUILayout.LabelField("  (none valid)");
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("  (none)");
                    }
                });
            }
            else
            {
                _foldouts[title] = false;
            }
            GUILayout.Space(4);
        }


        /// <summary>
        /// Removes null/destroyed (Unity null) entries from a list.
        /// </summary>
        private static void PruneNulls(List<ScriptableObject> list)
        {
            if (list == null) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var obj = list[i];
                // UnityEngine.Object overrides == and bool, so use both checks
                if (obj == null || !obj)
                    list.RemoveAt(i);
            }
        }
        // Shows a popup built from an array/list SerializedProperty of Object refs.
        // Returns new selected index, and outputs picked UnityEngine.Object.
        public static int PopupFromObjectArray(SerializedProperty arrayProp, int currentIndex, Func<UnityEngine.Object, string> nameOf, out UnityEngine.Object picked)
        {
            picked = null;
            if (arrayProp == null || !arrayProp.isArray)
                return currentIndex;

            var names = new List<string>();
            var objs = new List<UnityEngine.Object>();

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var el = arrayProp.GetArrayElementAtIndex(i);
                var o = el != null ? el.objectReferenceValue : null;
                if (!o) continue;
                objs.Add(o);
                names.Add(nameOf != null ? nameOf(o) : o.name);
            }

            int safeCurrent = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, objs.Count - 1));
            int next = EditorGUILayout.Popup("Event List", objs.Count == 0 ? -1 : safeCurrent, names.ToArray());
            if (next >= 0 && next < objs.Count) picked = objs[next];
            return next;
        }
        
        public static void ProbabilitySlider(SerializedProperty p, string label = "Probability")
        {
            if (p == null) return;
            float pct = Mathf.Clamp01(p.floatValue) * 100f;
            pct = EditorGUILayout.Slider(label, pct, 0f, 100f);
            p.floatValue = Mathf.Clamp01(pct / 100f);
        }

}
    
}
