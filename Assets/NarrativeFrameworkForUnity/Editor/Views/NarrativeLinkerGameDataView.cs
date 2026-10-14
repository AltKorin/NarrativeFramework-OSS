// Assets/SimulationGame/Editor/Views/NarrativeLinkerGameDataView.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Designer-first GameData editor view using NarrativeLinkerGUI helpers.
    /// - GameData selector with persistence (EditorPrefs) + New…
    /// - Sections: Campaign, Starting Event, Events, Story Arcs, Actors, Relationships,
    ///             Resources (+ ID validator), Required Flags, Variables, Custom Outcomes,
    ///             Game Over / Endings.
    /// - Safe UI wrappers & tolerant property lookup via NarrativeLinkerGUI.
    /// </summary>
    public class NarrativeLinkerGameDataView
    {
        // Scroll state
        private Vector2 _scroll;
        private Vector2 _resIdScroll;

        // Foldout state
        private static bool _foldCampaign       = true;
        private static bool _foldStartingEvent  = true;
        private static bool _foldEvents         = true;
        private static bool _foldStoryArcs      = true;
        private static bool _foldActors         = true;
        private static bool _foldRelationships  = false;
        private static bool _foldResources      = true;
        private static bool _foldReqFlags       = false;
        private static bool _foldVariables      = true;
        private static bool _foldCustomOutcomes = false;
        private static bool _foldGameOver       = true;

        // Editor state
        private const string PREF_GAMEDATA_PATH = "NarrativeLinker.PreferredGameDataPath";
        private GameData _gameData;

        public NarrativeLinkerGameDataView() { }

        public void Draw()
        {
            EnsureGameDataRef();

            NarrativeLinkerGUI.Scroll(ref _scroll, () =>
            {
                EditorGUILayout.LabelField("Game Data", NarrativeLinkerGUI.Header);
                EditorGUILayout.Space(4);

                DrawHeaderToolbar();

                if (_gameData == null)
                {
                    EditorGUILayout.HelpBox("GameData.asset not found. Click 'Create' or 'New…' above to generate it.", MessageType.Warning);
                    return;
                }

                var so = new SerializedObject(_gameData);
                so.Update();

                EditorGUILayout.Space(6);

                // ───────────────────────── Campaign ─────────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldCampaign, "Campaign"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pLen = NarrativeLinkerGUI.FindPropAny(so, "campaignLength", "_campaignLength");
                        if (pLen != null) EditorGUILayout.PropertyField(pLen, new GUIContent("Campaign Length (days)"));
                        else MissingProp("Campaign Length", "campaignLength/_campaignLength");
                    });
                }

                // ─────────────────────── Starting Event ─────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldStartingEvent, "Starting Event"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pStart = NarrativeLinkerGUI.FindPropAny(so, "startingEvent", "_startingEvent");
                        if (pStart != null) EditorGUILayout.PropertyField(pStart, new GUIContent("Starting Event"));
                        else MissingProp("Starting Event", "startingEvent/_startingEvent");
                    });
                }

                // ─────────────────────────── Events ─────────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldEvents, "Events"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pEvents = NarrativeLinkerGUI.FindPropAny(so, "events", "_events");
                        if (pEvents != null) EditorGUILayout.PropertyField(pEvents, new GUIContent("Events"), true);
                        else MissingProp("Events", "events/_events");
                    });
                }

                // ──────────────────────── Story Arcs ────────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldStoryArcs, "Story Arcs"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pArcs = NarrativeLinkerGUI.FindPropAny(so, "storyArcs", "_storyArcs");
                        if (pArcs != null) EditorGUILayout.PropertyField(pArcs, new GUIContent("Story Arcs"), true);
                        else MissingProp("Story Arcs", "storyArcs/_storyArcs");
                    });
                }

                // ─────────────────────────── Actors ─────────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldActors, "Actors"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pActors = NarrativeLinkerGUI.FindPropAny(so, "actors", "_actors");
                        if (pActors != null) EditorGUILayout.PropertyField(pActors, new GUIContent("Actors"), true);
                        else MissingProp("Actors", "actors/_actors");
                    });
                }

                // ─────────────────────── Relationships ──────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldRelationships, "Relationships"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pRel = NarrativeLinkerGUI.FindPropAny(so, "relationships", "_relationships");
                        if (pRel != null) EditorGUILayout.PropertyField(pRel, new GUIContent("Relationships"), true);
                        else MissingProp("Relationships", "relationships/_relationships");
                    });
                }

                // ───────────────────────── Resources ────────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldResources, "Resources"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pRes = NarrativeLinkerGUI.FindPropAny(so, "resources", "_resources");
                        if (pRes != null) EditorGUILayout.PropertyField(pRes, new GUIContent("Resources"), true);
                        else MissingProp("Resources", "resources/_resources");

                        DrawResourceIdTools(_gameData);
                    });
                }

                // ─────────────────────── Required Flags ─────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldReqFlags, "Required Flags"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pFlags = NarrativeLinkerGUI.FindPropAny(so, "requiredFlags", "_requiredFlags");
                        if (pFlags != null) EditorGUILayout.PropertyField(pFlags, new GUIContent("Required Flags"), true);
                        else MissingProp("Required Flags", "requiredFlags/_requiredFlags");
                    });
                }

                // ─────────────────── Narrative Variables ────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldVariables, "Narrative Variables"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pVars = NarrativeLinkerGUI.FindPropAny(so, "narrativeVariables", "_narrativeVariables");
                        if (pVars != null) EditorGUILayout.PropertyField(pVars, new GUIContent("Variables"), true);
                        else MissingProp("Variables", "narrativeVariables/_narrativeVariables");
                    });
                }

                // ─────────────────────── Custom Outcomes ────────────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldCustomOutcomes, "Custom Outcomes"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        var pCo = NarrativeLinkerGUI.FindPropAny(so, "customOutcomes", "_customOutcomes");
                        if (pCo != null) EditorGUILayout.PropertyField(pCo, new GUIContent("Custom Outcomes"), true);
                        else MissingProp("Custom Outcomes", "customOutcomes/_customOutcomes");
                    });
                }

                // ─────────────── Game Over / Endings (optional) ─────────────
                if (NarrativeLinkerGUI.Foldout(ref _foldGameOver, "Game Over / Endings"))
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        DrawGameOverSection(so, _gameData);
                    });
                }

                so.ApplyModifiedProperties();
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Header with helpers (selector + toolbar rows)
        // ─────────────────────────────────────────────────────────────
        private void DrawHeaderToolbar()
        {
            NarrativeLinkerGUI.VerticalBox(() =>
            {
               // Row 1: GameData selector + New…
            NarrativeLinkerGUI.Horizontal(() =>
            {
                var picked = (GameData)EditorGUILayout.ObjectField(
                    new GUIContent("Current GameData"), _gameData, typeof(GameData), false);

                if (picked != _gameData)
                {
                    _gameData = picked;

                    if (_gameData != null)
                    {
                        var path = AssetDatabase.GetAssetPath(_gameData);
                        if (!string.IsNullOrEmpty(path))
                            EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
                    }

                    // publish to all tabs/views
                    NarrativeLinkerContext.SetActive(_gameData); // can be null if cleared
                    NarrativeLinkerWindow.UpdateAllViews(_gameData); // nudge UI to refresh now
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("New…", GUILayout.Width(70)))
                {
                    CreateNewGameDataViaSavePanel();
                    EnsureGameDataRef();
                    NarrativeLinkerContext.SetActive(_gameData); // publish the newly created asset
                    NarrativeLinkerWindow.UpdateAllViews(_gameData); // refresh all tabs
                }
            });


                // Row 2: Toolbar buttons (using helper)
                NarrativeLinkerGUI.Toolbar(
                    ("Ping", () =>
                    {
                        if (_gameData) EditorGUIUtility.PingObject(_gameData);
                    }),
                    ("Clean Nulls & Duplicates", () =>
                    {
                        if (_gameData) _gameData.Editor_CleanNullsAndDuplicates();
                    }),
                    (_gameData == null ? "Create" : "Locate Asset", () =>
                    {
                        if (_gameData == null) CreateCanonicalGameDataAsset();
                        else
                        {
                            Selection.activeObject = _gameData;
                            EditorGUIUtility.PingObject(_gameData);
                        }
                    })
                );
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Game Over / Endings section (safe if fields are missing)
        // ─────────────────────────────────────────────────────────────
        private void DrawGameOverSection(SerializedObject so, GameData gd)
        {
            var pEnableDeaths   = NarrativeLinkerGUI.FindPropAny(so, "enableResourceDeaths", "_enableResourceDeaths");
            var pRules          = NarrativeLinkerGUI.FindPropAny(so, "resourceDeathRules", "_resourceDeathRules");
            var pCampaignEnding = NarrativeLinkerGUI.FindPropAny(so, "campaignEnding", "_campaignEnding");
            var pGameOverFlags  = NarrativeLinkerGUI.FindPropAny(so, "gameOverFlags", "_gameOverFlags"); // optional

            if (pEnableDeaths != null)
                EditorGUILayout.PropertyField(pEnableDeaths, new GUIContent("Enable Resource-Death Endings"));
            else
                MissingProp("Enable Resource-Death Endings", "enableResourceDeaths/_enableResourceDeaths");

            EditorGUILayout.Space(4);

            // Default Campaign Ending
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Default Campaign Ending", EditorStyles.boldLabel);

                if (pCampaignEnding != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(pCampaignEnding, GUIContent.none);
                    if (EditorGUI.EndChangeCheck())
                        so.ApplyModifiedProperties();

                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        if (GUILayout.Button("Create New Ending Event", GUILayout.Width(200)))
                        {
                            CreateEndingAssetAndAssign(pCampaignEnding);
                            so.ApplyModifiedProperties();
                        }

                        UnityEngine.Object sel = Selection.activeObject;
                        bool canUseSelected = sel is SimEventSO se && se.Type == SimEventSO.EventType.Ending;

                        using (new EditorGUI.DisabledScope(!canUseSelected))
                        {
                            if (GUILayout.Button("Use Selected", GUILayout.Width(120)) && canUseSelected)
                            {
                                pCampaignEnding.objectReferenceValue = sel;
                                so.ApplyModifiedProperties();
                                EditorGUIUtility.PingObject(sel);
                            }
                        }
                    });
                }
                else
                {
                    MissingProp("Default Campaign Ending", "campaignEnding/_campaignEnding");
                }
            });

            EditorGUILayout.Space(6);

            // Resource-fail rules
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Resource-Fail Endings", EditorStyles.boldLabel);
                if (pRules != null) EditorGUILayout.PropertyField(pRules, GUIContent.none, true);
                else MissingProp("Resource Death Rules", "resourceDeathRules/_resourceDeathRules");
            });

            EditorGUILayout.Space(6);

            // Optional: Game Over Flags
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Game Over Flags (optional)", EditorStyles.boldLabel);
                if (pGameOverFlags != null) EditorGUILayout.PropertyField(pGameOverFlags, GUIContent.none, true);
                else EditorGUILayout.HelpBox("No 'game over' flags field found. This is optional.", MessageType.None);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Resource Id tools (empty/duplicate detection + quick fixes)
        // ─────────────────────────────────────────────────────────────
        private void DrawResourceIdTools(GameData gd)
        {
            if (gd == null || gd.Resources == null || gd.Resources.Count == 0)
            {
                EditorGUILayout.HelpBox("No resources to validate.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Resource Id Validator", EditorStyles.boldLabel);

            var issues = new List<(Resource res, string id, string issue, string path, int dupCount)>();
            var byId = new Dictionary<string, int>();

            foreach (var r in gd.Resources)
            {
                if (r == null) continue;
                string id = r.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add((r, id, "Empty Id", AssetDatabase.GetAssetPath(r), 0));
                }
                else
                {
                    byId[id] = byId.TryGetValue(id, out int c) ? c + 1 : 1;
                }
            }

            foreach (var r in gd.Resources)
            {
                if (r == null) continue;
                string id = r.Id;
                if (!string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id, out int count) && count > 1)
                {
                    issues.Add((r, id, "Duplicate Id", AssetDatabase.GetAssetPath(r), count));
                }
            }

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No resource id issues detected.", MessageType.Info);
                return;
            }

            using (var scope = new GUILayout.ScrollViewScope(_resIdScroll, GUILayout.MinHeight(140)))
            {
                _resIdScroll = scope.scrollPosition;

                foreach (var i in issues)
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            EditorGUILayout.ObjectField(i.res, typeof(Resource), false);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Open", GUILayout.Width(60)))
                                Selection.activeObject = i.res;
                        });

                        EditorGUILayout.LabelField($"Issue: {i.issue}" + (i.issue == "Duplicate Id" ? $" (×{i.dupCount})" : ""), EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField("Path:", i.path, EditorStyles.miniLabel);

                        string current = string.IsNullOrWhiteSpace(i.id) ? "<empty>" : i.id;
                        EditorGUILayout.LabelField("Current Id:", current);

                        NarrativeLinkerGUI.Horizontal(() =>
                        {
                            if (GUILayout.Button("Auto-fill → Slug(name)"))
                            {
                                string suggested = Slug(string.IsNullOrWhiteSpace(i.res.name) ? i.res.GetType().Name : i.res.name);
                                Undo.RecordObject(i.res, "Auto-fill Resource Id");
                                i.res.Id = suggested;
                                EditorUtility.SetDirty(i.res);
                            }

                            // legacy private "_targetId" → optional copy
                            var targetIdField = typeof(Resource).GetField("_targetId",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            string legacy = targetIdField != null ? (targetIdField.GetValue(i.res) as string) : null;

                            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(legacy)))
                            {
                                if (GUILayout.Button("Use TargetId"))
                                {
                                    Undo.RecordObject(i.res, "Use TargetId as Id");
                                    i.res.Id = legacy.Trim();
                                    EditorUtility.SetDirty(i.res);
                                }
                            }

                            if (GUILayout.Button("Ping", GUILayout.Width(60)))
                                EditorGUIUtility.PingObject(i.res);
                        });
                    });
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private void EnsureGameDataRef()
        {
            // Remember previous so we publish only on change
            var before = _gameData;

            // Keep current if it still exists in the project
            if (!(_gameData != null && AssetDatabase.Contains(_gameData)))
            {
                // try last-picked path
                var prefPath = EditorPrefs.GetString(PREF_GAMEDATA_PATH, null);
                if (!string.IsNullOrEmpty(prefPath))
                {
                    var pref = AssetDatabase.LoadAssetAtPath<GameData>(prefPath);
                    if (pref != null) _gameData = pref;
                }

                // prefer canonical path
                if (_gameData == null)
                {
                    var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/SimulationGame/Resources/GameData.asset");
                    if (canonical != null) _gameData = canonical;
                }

                // fallback: any GameData
                if (_gameData == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:GameData");
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var gd = AssetDatabase.LoadAssetAtPath<GameData>(path);
                        if (gd != null) { _gameData = gd; break; }
                    }
                }
            }

            // Publish only if the reference actually changed
            if (!ReferenceEquals(before, _gameData))
            {
                if (_gameData != null)
                {
                    var path = AssetDatabase.GetAssetPath(_gameData);
                    if (!string.IsNullOrEmpty(path))
                        EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
                }

                // Notify all tabs/views about the new active GameData (can be null to clear)
                NarrativeLinkerContext.SetActive(_gameData);
                NarrativeLinkerWindow.UpdateAllViews(_gameData); // nudge UI to refresh now
            }
        }


        private void CreateCanonicalGameDataAsset()
        {
            string root = "Assets/SimulationGame";
            string res = root + "/Resources";
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "SimulationGame");
            if (!AssetDatabase.IsValidFolder(res))
                AssetDatabase.CreateFolder(root, "Resources");

            string path = res + "/GameData.asset";
            if (AssetDatabase.LoadAssetAtPath<GameData>(path) != null)
            {
                EditorUtility.DisplayDialog("GameData", "Assets/SimulationGame/Resources/GameData.asset already exists.", "OK");
                return;
            }

            var gd = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(gd, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _gameData = gd;
            EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
            Selection.activeObject = gd;
            EditorGUIUtility.PingObject(gd);
            NarrativeLinkerContext.SetActive(_gameData);
            NarrativeLinkerWindow.UpdateAllViews(_gameData);

        }

        private void CreateNewGameDataViaSavePanel()
        {
            string dir = "Assets/SimulationGame/Resources";
            if (!AssetDatabase.IsValidFolder("Assets/SimulationGame"))
                AssetDatabase.CreateFolder("Assets", "SimulationGame");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/SimulationGame", "Resources");

            string path = EditorUtility.SaveFilePanelInProject("Create GameData", "GameData_New.asset", "asset",
                "Choose location for the new GameData asset.", dir);
            if (string.IsNullOrEmpty(path)) return;

            var gd = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(gd, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _gameData = gd;
            EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
            Selection.activeObject = gd;
            EditorGUIUtility.PingObject(gd);
            NarrativeLinkerContext.SetActive(_gameData);
            NarrativeLinkerWindow.UpdateAllViews(_gameData);

        }

        private void MissingProp(string label, string propName)
        {
            EditorGUILayout.HelpBox($"Missing property '{label}' (expected: {propName}).", MessageType.None);
        }

        private static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "resource";
            s = s.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\s+", "_");
            s = Regex.Replace(s, @"[^a-z0-9_]", "");
            if (string.IsNullOrEmpty(s)) s = "resource";
            return s;
        }

        private void CreateEndingAssetAndAssign(SerializedProperty pCampaignEnding)
        {
            // Use helper to safely create & name the asset folder-wise
            var ev = NarrativeLinkerGUI.CreateAsset<SimEventSO>("Assets/SimulationGame/Data/Events", "Ending_New");
            ev.simEventTitle = "New Ending";
            ev.simEventDescription = "Describe the ending here…";
            ev.Type = SimEventSO.EventType.Ending;
            NarrativeLinkerGUI.RenameAsset(ev, "Ending_New");

            pCampaignEnding.objectReferenceValue = ev;
            Selection.activeObject = ev;
            EditorGUIUtility.PingObject(ev);
        }
    }
}
