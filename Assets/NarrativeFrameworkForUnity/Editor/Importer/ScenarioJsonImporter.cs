// Assets/SimulationGame/Editor/Importer/ScenarioJsonImporter.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SimulationGame;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Imports a full Scenario from JSON and creates/updates assets under:
    ///   Assets/SimulationGame/Data/ImportedScenarios/{scenarioName}/...
    /// It APPENDS to an existing GameData (chosen in the UI or remembered),
    /// and ONLY creates a new GameData if none exists anywhere in the project.
    /// </summary>
    public class ScenarioJsonImporter : EditorWindow
    {
        // ─────────────────────────────────────────────────────────────
        // UI state
        // ─────────────────────────────────────────────────────────────
        [SerializeField] private TextAsset _jsonFile;

        [SerializeField] private GameData _targetGameData;       // user-chosen GameData
        [SerializeField] private bool _appendToExisting = true;  // kept for clarity (always true in our flow)
        [SerializeField] private bool _setStartingEvent = true;  // optionally set starting event from JSON

        private const string PREF_GAMEDATA_PATH = "NarrativeLinker.PreferredGameDataPath";

        [MenuItem("Tools/Narrative Framework/Import Scenario JSON")]
        public static void ShowWindow()
        {
            GetWindow<ScenarioJsonImporter>("Import Scenario JSON");
        }

        private void OnGUI()
        {
            GUILayout.Label("Import Scenario from JSON", EditorStyles.boldLabel);
            _jsonFile = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("JSON File"), _jsonFile, typeof(TextAsset), false);

            _targetGameData = (GameData)EditorGUILayout.ObjectField(new GUIContent("Target GameData"), _targetGameData, typeof(GameData), false);
            _appendToExisting = EditorGUILayout.ToggleLeft(new GUIContent("Append to existing GameData (do not create a new one)"), true);
            _setStartingEvent = EditorGUILayout.ToggleLeft(new GUIContent("Set Starting Event from JSON"), _setStartingEvent);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_jsonFile == null))
            {
                if (GUILayout.Button("Import Scenario"))
                {
                    try
                    {
                        ImportScenario(_jsonFile.text);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("[ScenarioJsonImporter] Import failed: " + ex);
                        EditorUtility.DisplayDialog("Import failed", ex.Message, "OK");
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "This importer appends Resources/Events/Story Arcs into the selected GameData.\n" +
                "It creates a new GameData only if none exists in the project.\n" +
                "Assets are created under Assets/SimulationGame/Data/ImportedScenarios/{scenarioName}/…",
                MessageType.Info);
        }

        /// <summary>
        /// Public so other editor tools (e.g., single-event adapter window) can wrap JSON and reuse this logic.
        /// </summary>
        public void ImportScenario(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new System.Exception("JSON text is empty.");

            // Parse and basic validation
            var scenarioData = JsonUtility.FromJson<ScenarioJson>(json);
            if (scenarioData == null)
                throw new System.Exception("JSON does not match Scenario schema.");
            if (string.IsNullOrWhiteSpace(scenarioData.scenarioName))
                scenarioData.scenarioName = "ImportedScenario";

            // Normalize lists to avoid null refs during iteration
            scenarioData.requiredFlags ??= new List<string>();
            scenarioData.storyArcs     ??= new List<StoryArcJson>();
            scenarioData.resources     ??= new List<ResourceJson>();
            scenarioData.events        ??= new List<EventJson>();

            // Target folder for assets created from this scenario
            string basePath = $"Assets/SimulationGame/Data/ImportedScenarios/{scenarioData.scenarioName}";
            EnsureDir(basePath);

            // Resolve GameData: chosen → last-picked → canonical → any → create canonical
            var gameData = ResolveGameData();
            if (gameData == null) throw new System.Exception("Failed to resolve or create a GameData asset.");

            // Reflective accessors for GameData fields (consistent with a wide range of branches)
            var fCampaignLength = typeof(GameData).GetField("_campaignLength", BindingFlags.NonPublic | BindingFlags.Instance);
            var fRequiredFlags  = typeof(GameData).GetField("_requiredFlags",  BindingFlags.NonPublic | BindingFlags.Instance);
            var fResources      = typeof(GameData).GetField("_resources",      BindingFlags.NonPublic | BindingFlags.Instance);
            var fEvents         = typeof(GameData).GetField("_events",         BindingFlags.NonPublic | BindingFlags.Instance);
            var fStoryArcs      = typeof(GameData).GetField("_storyArcs",      BindingFlags.NonPublic | BindingFlags.Instance);
            var fStartingEvent  = typeof(GameData).GetField("_startingEvent",  BindingFlags.NonPublic | BindingFlags.Instance);

            // Pull existing lists (or create if missing)
            var existingResources = (List<Resource>) (fResources?.GetValue(gameData) ?? new List<Resource>());
            var existingEvents    = (List<SimEventSO>)(fEvents?.GetValue(gameData)    ?? new List<SimEventSO>());
            var existingArcs      = (List<StoryArcSO>)(fStoryArcs?.GetValue(gameData) ?? new List<StoryArcSO>());

            // Campaign setup: safe overwrite from JSON
            if (fCampaignLength != null) fCampaignLength.SetValue(gameData, scenarioData.campaignLength);
            if (fRequiredFlags  != null) fRequiredFlags.SetValue(gameData, scenarioData.requiredFlags);

            // ───────────────────────────────────────────────────────── Resources
            string resourceFolder = Path.Combine(basePath, "Resources");
            EnsureDir(resourceFolder);

            // Map existing resources by Id for quick reuse
            var resById = new Dictionary<string, Resource>();
            foreach (var r in existingResources)
                if (r != null && !string.IsNullOrWhiteSpace(r.Id) && !resById.ContainsKey(r.Id))
                    resById.Add(r.Id, r);

            // Keep a local list of resources created now (for lookup when wiring outcomes)
            var newlyCreatedResources = new List<Resource>();

            foreach (var resData in scenarioData.resources)
            {
                if (resData == null || string.IsNullOrWhiteSpace(resData.id)) continue;

                // Reuse existing resource by Id if present; otherwise create new asset
                if (!resById.TryGetValue(resData.id, out var res))
                {
                    res = CreateInstance<Resource>();
                    res.name = resData.id;

                    // Initialize via private fields (common pattern across branches)
                    TrySetField(res, "_id", resData.id);
                    TrySetField(res, "_initialValue", resData.initialValue);

                    AssetDatabase.CreateAsset(res, Path.Combine(resourceFolder, res.name + ".asset"));
                    newlyCreatedResources.Add(res);
                    resById[resData.id] = res;

                    AppendUnique(existingResources, res);
                }
                else
                {
                    // Update initial value if defined
                    TrySetField(res, "_initialValue", resData.initialValue);
                    EditorUtility.SetDirty(res);
                }
            }

            fResources?.SetValue(gameData, existingResources);

            // ───────────────────────────────────────────────────────── Events + map
            string eventFolder = Path.Combine(basePath, "Events");
            EnsureDir(eventFolder);

            // Build an event map (by asset name) including existing events
            var eventMap = new Dictionary<string, SimEventSO>();
            foreach (var e in existingEvents)
                if (e != null && !string.IsNullOrWhiteSpace(e.name) && !eventMap.ContainsKey(e.name))
                    eventMap.Add(e.name, e);

            var createdEvents = new List<SimEventSO>();

            // First pass: create (or reuse) event assets
            foreach (var evtData in scenarioData.events)
            {
                if (evtData == null || string.IsNullOrWhiteSpace(evtData.id)) continue;

                if (!eventMap.TryGetValue(evtData.id, out var evt))
                {
                    evt = CreateInstance<SimEventSO>();
                    evt.name = evtData.id;

                    // Title / Description (try common names across branches)
                    SetMemberFlexible(evt, new[] { "simEventTitle", "Title" }, evtData.title);
                    SetMemberFlexible(evt, new[] { "simEventDescription", "Description" }, evtData.description);

                    // Ensure choices list
                    SetMemberFlexible(evt, new[] { "_choices" }, new List<Choice>());

                    AssetDatabase.CreateAsset(evt, Path.Combine(eventFolder, evt.name + ".asset"));
                    createdEvents.Add(evt);
                    eventMap.Add(evt.name, evt);

                    AppendUnique(existingEvents, evt);
                }
                else
                {
                    // Update basic fields when reusing an existing event
                    SetMemberFlexible(evt, new[] { "simEventTitle", "Title" }, evtData.title);
                    SetMemberFlexible(evt, new[] { "simEventDescription", "Description" }, evtData.description);

                    // Replace choices to match JSON deterministically
                    SetMemberFlexible(evt, new[] { "_choices" }, new List<Choice>());
                }
            }

            // ───────────────────────────────────────────────────────── Choices & Outcomes
            string outcomeFolder = Path.Combine(basePath, "Outcomes");
            EnsureDir(outcomeFolder);

            foreach (var evtData in scenarioData.events)
            {
                if (evtData == null || string.IsNullOrWhiteSpace(evtData.id)) continue;

                if (!eventMap.TryGetValue(evtData.id, out var evt))
                {
                    Debug.LogWarning($"[ScenarioJsonImporter] Event '{evtData.id}' not found after creation phase.");
                    continue;
                }

                var choices = (List<Choice>) GetFieldValue(evt, "_choices") ?? new List<Choice>();
                // Replace any existing choices
                choices.Clear();

                // Build choices
                evtData.choices ??= new List<ChoiceJson>();
                foreach (var chData in evtData.choices)
                {
                    if (chData == null) continue;

                    var choice = CreateInstance<Choice>();
                    choice.name = $"Choice_{Sanitize(chData.text)}";

                    // Text (prefer property ChoiceText; fallback to _text field)
                    if (!TrySetProperty(choice, "ChoiceText", string.IsNullOrWhiteSpace(chData.text) ? "..." : chData.text.Trim()))
                        TrySetField(choice, "_text", string.IsNullOrWhiteSpace(chData.text) ? "..." : chData.text.Trim());

                    // Next day flag (usually public)
                    choice.GoToNextDay = chData.goToNextDay;

                    // nextEventId link (rely on name mapping)
                    if (!string.IsNullOrEmpty(chData.nextEventId) && eventMap.TryGetValue(chData.nextEventId, out var nextEvt))
                    {
                        if (!TrySetProperty(choice, "NextEvent", nextEvt))
                            TrySetField(choice, "_nextEvent", nextEvt);
                    }

                    // Outcomes
                    var outcomes = new List<Outcome>();
                    chData.outcomes ??= new List<OutcomeJson>();
                    foreach (var outData in chData.outcomes)
                    {
                        if (outData == null) continue;

                        var outcome = CreateInstance<Outcome>();
                        outcome.name = $"Outcome_{outData.type}_{Sanitize(outData.targetId)}";

                        // Prefer an Initialize(...) method if your branch has it
                        var init = outcome.GetType().GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                        if (init != null)
                        {
                            init.Invoke(outcome, new object[]
                            {
                                outData.type,
                                outData.targetId ?? "",
                                outData.value,
                                Mathf.Clamp01(outData.probability),
                                outData.description ?? ""
                            });
                        }
                        else
                        {
                            // Fallback to fields/properties
                            TrySetField(outcome, "_type", outData.type);
                            outcome.Probability = outData.probability;
                            TrySetField(outcome, "_value", outData.value);
                            TrySetField(outcome, "_outcomeDescription", outData.description);
                        }

                        // Wire target resource by Id, if outcome type uses resources/flags
                        if (outData.type == OutcomeType.ModifyResource ||
                            outData.type == OutcomeType.AddFlag ||
                            outData.type == OutcomeType.RemoveFlag)
                        {
                            var res = FindResourceById(existingResources, outData.targetId)
                                   ?? FindResourceById(newlyCreatedResources, outData.targetId);

                            var setRes = outcome.GetType().GetMethod("SetTargetResource", BindingFlags.Public | BindingFlags.Instance);
                            if (setRes != null) setRes.Invoke(outcome, new object[] { res });
                            else TrySetField(outcome, "targetResource", res);
                        }

                        // Wire target event for TriggerEvent
                        if (outData.type == OutcomeType.TriggerEvent && eventMap.TryGetValue(outData.targetId, out var targetEvt))
                        {
                            var setEvt = outcome.GetType().GetMethod("SetTargetEvent", BindingFlags.Public | BindingFlags.Instance);
                            if (setEvt != null) setEvt.Invoke(outcome, new object[] { targetEvt });
                            else TrySetField(outcome, "targetEvent", targetEvt);
                        }

                        AssetDatabase.CreateAsset(outcome, Path.Combine(outcomeFolder, outcome.name + ".asset"));
                        outcomes.Add(outcome);
                    }

                    // Add outcomes to choice (prefer AddOutcome method; fallback to internal list)
                    var addOutcome = choice.GetType().GetMethod("AddOutcome", BindingFlags.Public | BindingFlags.Instance);
                    if (addOutcome != null)
                    {
                        foreach (var o in outcomes) addOutcome.Invoke(choice, new object[] { o });
                    }
                    else
                    {
                        TrySetField(choice, "outcomes", outcomes); // older branches
                    }

                    // Store choice as sub-asset of the event and register in the list
                    AssetDatabase.AddObjectToAsset(choice, evt);
                    choices.Add(choice);
                }

                // Write back the mutated list
                TrySetField(evt, "_choices", choices);
                EditorUtility.SetDirty(evt);
            }

            // ───────────────────────────────────────────────────────── Story Arcs
            string storyArcFolder = Path.Combine(basePath, "StoryArcs");
            EnsureDir(storyArcFolder);

            var createdArcs = new List<StoryArcSO>();
            foreach (var arcJson in scenarioData.storyArcs)
            {
                if (arcJson == null || string.IsNullOrWhiteSpace(arcJson.arcName)) continue;

                // Reuse StoryArc by name if already present
                var existingArc = existingArcs.Find(a => a != null && a.name == arcJson.arcName);
                StoryArcSO arc = existingArc != null ? existingArc : CreateInstance<StoryArcSO>();
                arc.name = arcJson.arcName;

                TrySetField(arc, "_arcName", arcJson.arcName);
                TrySetField(arc, "_arcDescription", arcJson.arcDescription);
                TrySetField(arc, "_minDay", arcJson.minDay);
                TrySetField(arc, "_maxDay", arcJson.maxDay);
                TrySetField(arc, "_allowMultipleActivations", arcJson.allowMultipleActivations);
                TrySetField(arc, "_requiredFlags",  arcJson.requiredFlags  ?? new List<string>());
                TrySetField(arc, "_forbiddenFlags", arcJson.forbiddenFlags ?? new List<string>());

                // Required/contained events
                var requiredEvents = new List<SimEventSO>();
                if (arcJson.requiredEvents != null)
                {
                    foreach (var id in arcJson.requiredEvents)
                        if (!string.IsNullOrEmpty(id) && eventMap.TryGetValue(id, out var re))
                            requiredEvents.Add(re);
                }
                TrySetField(arc, "_requiredEvents", requiredEvents);

                var eventsInArc = new List<SimEventSO>();
                if (arcJson.eventsInArc != null)
                {
                    foreach (var id in arcJson.eventsInArc)
                        if (!string.IsNullOrEmpty(id) && eventMap.TryGetValue(id, out var ee))
                            eventsInArc.Add(ee);
                }
                TrySetField(arc, "_eventsInArc", eventsInArc);

                // Triggered event
                if (!string.IsNullOrEmpty(arcJson.triggeredEventId) && eventMap.TryGetValue(arcJson.triggeredEventId, out var trigEvt))
                    TrySetField(arc, "_triggeredEvent", trigEvt);

                if (existingArc == null)
                {
                    AssetDatabase.CreateAsset(arc, Path.Combine(storyArcFolder, arc.name + ".asset"));
                    createdArcs.Add(arc);
                    AppendUnique(existingArcs, arc);
                }
                else
                {
                    EditorUtility.SetDirty(arc);
                }
            }

            fStoryArcs?.SetValue(gameData, existingArcs);

            // ───────────────────────────────────────────────────────── Starting Event
            if (_setStartingEvent && !string.IsNullOrWhiteSpace(scenarioData.startingEventId) &&
                eventMap.TryGetValue(scenarioData.startingEventId, out var startEvt))
            {
                fStartingEvent?.SetValue(gameData, startEvt);
            }

            // Persist GameData list changes
            fEvents?.SetValue(gameData, existingEvents);
            fResources?.SetValue(gameData, existingResources);
            fStoryArcs?.SetValue(gameData, existingArcs);

            EditorUtility.SetDirty(gameData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Import Complete",
                $"Scenario '{scenarioData.scenarioName}' imported into GameData '{gameData.name}'.", "OK");
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers — filesystem & list
        // ─────────────────────────────────────────────────────────────

        /// <summary>Create folder tree if missing (Assets-relative path).</summary>
        private static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var segments = path.Replace("\\", "/").Split('/');
            if (segments.Length <= 1 || segments[0] != "Assets") return;

            string cur = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{cur}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, segments[i]);
                cur = next;
            }
        }

        /// <summary>Append item to list only if not already present.</summary>
        private static void AppendUnique<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item == null) return;
            if (list == null) return;
            if (!list.Contains(item)) list.Add(item);
        }

        /// <summary>Find a Resource in a list by its Id.</summary>
        private static Resource FindResourceById(List<Resource> list, string id)
        {
            if (list == null || string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r != null && r.Id == id) return r;
            }
            return null;
        }

        /// <summary>Make a safe asset/name fragment.</summary>
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Item";
            var chars = new List<char>(s.Length);
            foreach (var c in s)
                if (char.IsLetterOrDigit(c) || c == '_') chars.Add(c);
            return chars.Count == 0 ? "Item" : new string(chars.ToArray());
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers — reflection (robust across branches)
        // ─────────────────────────────────────────────────────────────

        private static bool TrySetProperty(object target, string propName, object value)
        {
            if (target == null) return false;
            var t = target.GetType();
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value);
                return true;
            }
            return false;
        }

        private static bool TrySetField(object target, string fieldName, object value)
        {
            if (target == null) return false;
            var t = target.GetType();
            var f = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                f.SetValue(target, value);
                return true;
            }
            return false;
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var t = target.GetType();
            var f = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(target) : null;
        }

        /// <summary>
        /// Try a list of member names, prefer property then field. Useful when branches rename members.
        /// </summary>
        private static void SetMemberFlexible(object target, string[] names, object value)
        {
            foreach (var n in names)
            {
                if (TrySetProperty(target, n, value)) return;
                if (TrySetField(target, n, value)) return;
            }
            // If nothing matched, silently ignore (keeps importer robust across branches)
        }

        /// <summary>
        /// Decide which GameData to use: user selection → last-picked path → canonical → any → create canonical.
        /// </summary>
        private GameData ResolveGameData()
        {
            // 1) User-selected in this window
            if (_targetGameData != null && AssetDatabase.Contains(_targetGameData))
            {
                var path = AssetDatabase.GetAssetPath(_targetGameData);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
                return _targetGameData;
            }

            // 2) Last-picked in NarrativeLinker/other tools
            var prefPath = EditorPrefs.GetString(PREF_GAMEDATA_PATH, null);
            if (!string.IsNullOrEmpty(prefPath))
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameData>(prefPath);
                if (pref != null) return pref;
            }

            // 3) Canonical path
            var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/SimulationGame/Resources/GameData.asset");
            if (canonical != null) return canonical;

            // 4) Any existing in project
            foreach (var guid in AssetDatabase.FindAssets("t:GameData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var gd = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (gd != null)
                {
                    EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
                    return gd;
                }
            }

            // 5) None exists → create canonical
            string root = "Assets/SimulationGame";
            string res  = root + "/Resources";
            EnsureDir(res);

            string newPath = res + "/GameData.asset";
            var created = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(created, newPath);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetString(PREF_GAMEDATA_PATH, newPath);
            Debug.LogWarning($"[ScenarioJsonImporter] Created new GameData at {newPath}");
            return created;
        }

        // ─────────────────────────────────────────────────────────────
        // JSON models (same as your existing schema)
        // ─────────────────────────────────────────────────────────────

        [System.Serializable]
        private class ScenarioJson
        {
            public string scenarioName;
            public int campaignLength;
            public string startingEventId;
            public List<string> requiredFlags;
            public List<StoryArcJson> storyArcs;
            public List<ResourceJson> resources;
            public List<EventJson> events;
        }

        [System.Serializable]
        private class ResourceJson
        {
            public string id;
            public float initialValue;
        }

        [System.Serializable]
        private class EventJson
        {
            public string id;
            public string title;
            public string description;
            public List<ChoiceJson> choices;
        }

        [System.Serializable]
        private class ChoiceJson
        {
            public string text;
            public bool goToNextDay;
            public string nextEventId;
            public List<OutcomeJson> outcomes;
        }

        [System.Serializable]
        private class OutcomeJson
        {
            public OutcomeType type;       // ModifyResource, AddFlag, RemoveFlag, TriggerEvent
            public string targetId;        // resource id or event id (for TriggerEvent)
            public float value;
            public float probability;
            public string description;
        }

        [System.Serializable]
        private class StoryArcJson
        {
            public string arcName;
            public string arcDescription;
            public string triggeredEventId;
            public List<string> eventsInArc;
            public bool allowMultipleActivations;
            public int minDay;
            public int maxDay;
            public List<string> requiredFlags;
            public List<string> forbiddenFlags;
            public List<string> requiredEvents;
        }
    }
}
