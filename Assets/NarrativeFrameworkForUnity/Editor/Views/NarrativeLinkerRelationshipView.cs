// Assets/SimulationGame/Editor/Views/NarrativeLinkerRelationshipView.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal; // RepaintAllViews

namespace SimulationGame.Editor
{
    /// <summary>
    /// Relationship tab for the Narrative Linker.
    /// - Lists and edits relationships stored on GameData._relationships (via SerializedProperty).
    /// - Lets you add/delete rows, mirror a relationship, and auto-clamp strength to [-100..100].
    /// - Minimal changes: uses NarrativeLinkerContext for the active GameData (no GameData.Instance).
    /// </summary>
    public class NarrativeLinkerRelationshipView
    {
        private GameData gameData;       // active GameData from NarrativeLinkerContext
        private bool _initialized;       // lifecycle guard
        private Vector2 scroll;

        // New-relationship form state
        private ActorSO newSource;
        private ActorSO newTarget;
        private float   newStrength = 0f;
        private string  newType     = "Neutral";
        private bool    createReverse = false;

        // Cached options for popups
        private string[] actorNames = System.Array.Empty<string>();
        private ActorSO[] actorRefs = System.Array.Empty<ActorSO>();
        private int sourcePopupIndex = -1;
        private int targetPopupIndex = -1;

        // Filter state
        private ActorSO filterActor;
        private int filterActorPopupIndex = -1;

        // ─────────────────────────────────────────────────────────────
        // Lifecycle (subscribe to active GameData context)
        // ─────────────────────────────────────────────────────────────
        public void OnEnable()
        {
            if (!_initialized)
            {
                gameData = NarrativeLinkerContext.GetOrResolve();
                NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
                _initialized = true;
            }
            RebuildActorOptions();
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(gameData, gd)) return;
            gameData = gd;
            RebuildActorOptions();
            InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────────
        // Main UI
        // ─────────────────────────────────────────────────────────────
        public void Draw()
        {
            // Safety sync with context if window reopened after a change
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, gameData))
            {
                gameData = current;
                RebuildActorOptions();
            }

            // Status header
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                if (gameData == null)
                {
                    EditorGUILayout.HelpBox("No active GameData selected. Pick one in the Game Data tab.", MessageType.Warning);
                    if (GUILayout.Button("Refresh Context", GUILayout.Width(150)))
                    {
                        gameData = NarrativeLinkerContext.GetOrResolve();
                        RebuildActorOptions();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Active GameData", EditorStyles.boldLabel);
                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        EditorGUILayout.ObjectField(gameData, typeof(GameData), false);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Ping", GUILayout.Width(60)))   EditorGUIUtility.PingObject(gameData);
                        if (GUILayout.Button("Select", GUILayout.Width(70))) Selection.activeObject = gameData;
                    });
                }
            });

            if (gameData == null) return;

            var so = new SerializedObject(gameData);
            so.Update();

            DrawToolbar(so);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Relationships", EditorStyles.boldLabel);

            NarrativeLinkerGUI.Scroll(ref scroll, () =>
            {
                var listProp = so.FindProperty(GameData.PROP_RELATIONS) ?? so.FindProperty("_relationships");
                if (listProp == null)
                {
                    EditorGUILayout.HelpBox("Serialized list '_relationships' not found on GameData.", MessageType.Error);
                }
                else
                {
                    DrawRelationshipList(so, listProp);

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Add New Relationship", EditorStyles.boldLabel);
                    DrawNewRelationshipForm(so, listProp);
                }
            });

            so.ApplyModifiedProperties();
        }

        private void DrawToolbar(SerializedObject so)
        {
            NarrativeLinkerGUI.Horizontal(() =>
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    RebuildActorOptions();
                }

                // Actor filter
                EditorGUILayout.LabelField("Filter by Source", GUILayout.Width(100));
                filterActor = (ActorSO)EditorGUILayout.ObjectField(filterActor, typeof(ActorSO), false, GUILayout.Width(180));
                DrawActorPopup(ref filterActor, ref filterActorPopupIndex, "Pick");
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    filterActor = null;
                    filterActorPopupIndex = -1;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Auto-clamp strengths"))
                {
                    var relList = so.FindProperty(GameData.PROP_RELATIONS) ?? so.FindProperty("_relationships");
                    if (relList != null)
                    {
                        for (int i = 0; i < relList.arraySize; i++)
                        {
                            var elem = relList.GetArrayElementAtIndex(i);
                            var strengthProp = elem.FindPropertyRelative("Strength");
                            strengthProp.floatValue = Mathf.Clamp(strengthProp.floatValue, -100f, 100f);
                        }
                    }
                }
            }, EditorStyles.toolbar);
        }

        // ─────────────────────────────────────────────────────────────
        // List drawing
        // ─────────────────────────────────────────────────────────────
        private void DrawRelationshipList(SerializedObject gameDataSO, SerializedProperty relList)
        {
            if (relList.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No relationships defined yet.", MessageType.Info);
                return;
            }

            for (int i = 0; i < relList.arraySize; i++)
            {
                var elem = relList.GetArrayElementAtIndex(i);
                var pSource   = elem.FindPropertyRelative("Source");
                var pTarget   = elem.FindPropertyRelative("Target");
                var pStrength = elem.FindPropertyRelative("Strength");
                var pType     = elem.FindPropertyRelative("RelationshipType");

                // FILTER: skip if filterActor is set and doesn't match Source
                if (filterActor != null && pSource.objectReferenceValue != filterActor)
                    continue;

                EditorGUILayout.BeginVertical("box");

                // Header row
                EditorGUILayout.BeginHorizontal();
                var srcName = pSource.objectReferenceValue ? ((ActorSO)pSource.objectReferenceValue).ActorName : "<none>";
                var tgtName = pTarget.objectReferenceValue ? ((ActorSO)pTarget.objectReferenceValue).ActorName : "<none>";
                EditorGUILayout.LabelField($"{srcName} → {tgtName}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Mirror", GUILayout.Width(70)))
                {
                    MirrorRelationship(gameDataSO, relList, i);
                    // early continue to avoid layout issues
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                if (GUILayout.Button("Delete", GUILayout.Width(70)))
                {
                    RemoveAt(relList, i);
                    gameDataSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(gameDataSO.targetObject);
                    AssetDatabase.SaveAssets();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    // After removal, the list shifted; bail out of GUI to be safe
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                // Editable fields
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(pSource, new GUIContent("Source"));
                EditorGUILayout.PropertyField(pTarget, new GUIContent("Target"));
                pStrength.floatValue = EditorGUILayout.Slider("Strength", pStrength.floatValue, -100f, 100f);
                pType.stringValue    = EditorGUILayout.TextField("Type", pType.stringValue);
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Add-new form
        // ─────────────────────────────────────────────────────────────
        private void DrawNewRelationshipForm(SerializedObject gameDataSO, SerializedProperty relList)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Source / Target: object field + optional popup for convenience
                EditorGUILayout.LabelField("Source Actor", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                newSource = (ActorSO)EditorGUILayout.ObjectField(newSource, typeof(ActorSO), false);
                DrawActorPopup(ref newSource, ref sourcePopupIndex, "Pick");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Target Actor", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                newTarget = (ActorSO)EditorGUILayout.ObjectField(newTarget, typeof(ActorSO), false);
                DrawActorPopup(ref newTarget, ref targetPopupIndex, "Pick");
                EditorGUILayout.EndHorizontal();

                newStrength = EditorGUILayout.Slider("Strength", newStrength, -100f, 100f);
                newType     = EditorGUILayout.TextField("Type", newType);

                createReverse = EditorGUILayout.ToggleLeft("Also create reverse relationship", createReverse);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add Relationship", GUILayout.Width(160)))
                    {
                        if (newSource == null || newTarget == null)
                        {
                            Debug.LogWarning("Both Source and Target must be assigned.");
                        }
                        else
                        {
                            // Check for existing pair
                            int existingIndex = FindRelationship(relList, newSource, newTarget);
                            if (existingIndex >= 0)
                            {
                                if (EditorUtility.DisplayDialog(
                                        "Overwrite existing?",
                                        $"A relationship {newSource.ActorName} → {newTarget.ActorName} already exists.\nOverwrite values?",
                                        "Overwrite", "Cancel"))
                                {
                                    var elem = relList.GetArrayElementAtIndex(existingIndex);
                                    elem.FindPropertyRelative("Strength").floatValue = newStrength;
                                    elem.FindPropertyRelative("RelationshipType").stringValue = newType;
                                }
                            }
                            else
                            {
                                AppendRelationship(relList, newSource, newTarget, newStrength, newType);
                            }

                            if (createReverse)
                            {
                                int backIndex = FindRelationship(relList, newTarget, newSource);
                                if (backIndex >= 0)
                                {
                                    var elem = relList.GetArrayElementAtIndex(backIndex);
                                    elem.FindPropertyRelative("Strength").floatValue = newStrength;
                                    elem.FindPropertyRelative("RelationshipType").stringValue = newType;
                                }
                                else
                                {
                                    AppendRelationship(relList, newTarget, newSource, newStrength, newType);
                                }
                            }

                            gameDataSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(gameDataSO.targetObject);
                            AssetDatabase.SaveAssets();

                            // reset form
                            newSource = null;
                            newTarget = null;
                            sourcePopupIndex = targetPopupIndex = -1;
                            newStrength = 0f;
                            newType = "Neutral";
                            createReverse = false;
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private void RebuildActorOptions()
        {
            if (gameData == null || gameData.Actors == null)
            {
                actorNames = System.Array.Empty<string>();
                actorRefs  = System.Array.Empty<ActorSO>();
                sourcePopupIndex = targetPopupIndex = -1;
                return;
            }

            var list = new List<string>();
            var refs = new List<ActorSO>();
            foreach (var a in gameData.Actors)
            {
                if (!a) continue;
                list.Add(a.ActorName);
                refs.Add(a);
            }
            actorNames = list.ToArray();
            actorRefs  = refs.ToArray();

            // Keep indices in range
            if (sourcePopupIndex >= actorNames.Length) sourcePopupIndex = -1;
            if (targetPopupIndex >= actorNames.Length) targetPopupIndex = -1;
        }

        private void DrawActorPopup(ref ActorSO current, ref int popupIndex, string buttonLabel)
        {
            using (new EditorGUI.DisabledScope(actorNames.Length == 0))
            {
                if (GUILayout.Button(buttonLabel, GUILayout.Width(60)))
                {
                    // jump index to current if present
                    int localPopupIndex = IndexOfActor(current);
                    int selectedActorIndex = -1; // local capture for GenericMenu
                    GenericMenu menu = new GenericMenu();
                    for (int i = 0; i < actorNames.Length; i++)
                    {
                        int capture = i;
                        menu.AddItem(new GUIContent(actorNames[i]), i == localPopupIndex, () =>
                        {
                            selectedActorIndex = capture;
                        });
                    }
                    menu.ShowAsContext();
                    // After menu closes, assign if a selection was made
                    if (selectedActorIndex >= 0)
                    {
                        popupIndex = selectedActorIndex;
                        current = actorRefs[selectedActorIndex];
                    }
                }
            }
        }

        private int IndexOfActor(ActorSO a)
        {
            if (a == null) return -1;
            for (int i = 0; i < actorRefs.Length; i++)
                if (actorRefs[i] == a) return i;
            return -1;
        }

        private static void RemoveAt(SerializedProperty list, int index)
        {
            list.DeleteArrayElementAtIndex(index);
        }

        private static void MirrorRelationship(SerializedObject gameDataSO, SerializedProperty list, int index)
        {
            var src = list.GetArrayElementAtIndex(index).FindPropertyRelative("Source").objectReferenceValue as ActorSO;
            var tgt = list.GetArrayElementAtIndex(index).FindPropertyRelative("Target").objectReferenceValue as ActorSO;
            var str = list.GetArrayElementAtIndex(index).FindPropertyRelative("Strength").floatValue;
            var typ = list.GetArrayElementAtIndex(index).FindPropertyRelative("RelationshipType").stringValue;

            if (src == null || tgt == null) return;

            // If reverse already exists, update instead of duplicating
            int reverse = FindRelationship(list, tgt, src);
            if (reverse >= 0)
            {
                var elem = list.GetArrayElementAtIndex(reverse);
                elem.FindPropertyRelative("Strength").floatValue = str;
                elem.FindPropertyRelative("RelationshipType").stringValue = typ;
            }
            else
            {
                AppendRelationship(list, tgt, src, str, typ);
            }

            gameDataSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(gameDataSO.targetObject);
            AssetDatabase.SaveAssets();
        }

        private static void AppendRelationship(SerializedProperty list, ActorSO source, ActorSO target, float strength, string type)
        {
            int idx = list.arraySize;
            list.InsertArrayElementAtIndex(idx);
            var elem = list.GetArrayElementAtIndex(idx);
            elem.FindPropertyRelative("Source").objectReferenceValue = source;
            elem.FindPropertyRelative("Target").objectReferenceValue = target;
            elem.FindPropertyRelative("Strength").floatValue = Mathf.Clamp(strength, -100f, 100f);
            elem.FindPropertyRelative("RelationshipType").stringValue = type ?? "Neutral";
        }

        private static int FindRelationship(SerializedProperty list, ActorSO source, ActorSO target)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                var s = e.FindPropertyRelative("Source").objectReferenceValue as ActorSO;
                var t = e.FindPropertyRelative("Target").objectReferenceValue as ActorSO;
                if (s == source && t == target) return i;
            }
            return -1;
        }
    }
}
