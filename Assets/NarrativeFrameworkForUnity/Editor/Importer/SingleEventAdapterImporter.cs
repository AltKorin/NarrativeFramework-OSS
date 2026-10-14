#if UNITY_EDITOR
// Assets/SimulationGame/Editor/Importer/SingleEventAdapterImporter.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SimulationGame;              // GameData, Resource, NarrativeVariableSO, SimEventSO, StoryArcSO, OutcomeType

namespace SimulationGame.Editor
{
    /// <summary>
    /// Single-Event → Scenario wrapper importer.
    /// Adapts a minimal single-event JSON into the ScenarioJson format then calls the
    /// existing ScenarioJsonImporter.ImportScenario(string) (via reflection).
    ///
    /// Features:
    /// - Null-safe parsing (missing lists defaulted).
    /// - Optional placeholders for Resources / Variables / Events / StoryArcs / Actors.
    /// - Default "Continue" choice if event has no choices.
    /// - Event stubs for any referenced nextEventId / TriggerEvent outcomes.
    /// - String outcome types get mapped to OutcomeType enum.
    /// - Compatible with your current pipeline (no core refactor required).
    /// </summary>
    public static class SingleEventAdapterImporter
    {
        /// <summary>
        /// Imports a single-event JSON by wrapping it into a scenario JSON and
        /// invoking ScenarioJsonImporter.ImportScenario(string).
        /// </summary>
        /// <param name="eventJson">The single-event JSON text.</param>
        /// <param name="createPlaceholders">Create ScriptableObject placeholders when referenced assets are missing.</param>
        /// <param name="createDefaultChoiceIfEmpty">Add a "Continue" choice if no choices are provided.</param>
        /// <param name="placeholdersRoot">Folder to create placeholder assets in.</param>
        /// <param name="registerNewAssetsInGameData">If provided, new placeholders are appended to this GameData.</param>
        public static void ImportSingleEventJson(
            string eventJson,
            bool createPlaceholders = true,
            bool createDefaultChoiceIfEmpty = true,
            string placeholdersRoot = "Assets/SimulationGame/Data/Placeholders",
            GameData registerNewAssetsInGameData = null)
        {
            if (string.IsNullOrWhiteSpace(eventJson))
                throw new ArgumentException("SingleEventAdapterImporter: eventJson is empty.");

            // 1) Parse input (null-safe)
            var ev = JsonUtility.FromJson<SingleEventJson>(eventJson);
            if (ev == null)
                throw new Exception("SingleEventAdapterImporter: invalid single-event JSON.");

            if (string.IsNullOrWhiteSpace(ev.id))
                ev.id = MakeIdFromTitle(ev.title ?? ev.titleKey ?? "IMPORTED_EVENT");

            ev.choices ??= new List<ChoiceJsonIn>();
            foreach (var ch in ev.choices)
                ch.outcomes ??= new List<OutcomeJsonIn>();

            string title = string.IsNullOrWhiteSpace(ev.title) ? (ev.titleKey ?? ev.id) : ev.title;
            string desc  = string.IsNullOrEmpty(ev.description) ? (ev.descriptionKey ?? "") : ev.description;

            if (createDefaultChoiceIfEmpty && ev.choices.Count == 0)
            {
                ev.choices.Add(new ChoiceJsonIn {
                    text        = "Continue",
                    goToNextDay = false,
                    outcomes    = new List<OutcomeJsonIn>()
                });
            }

            // 2) Collect referenced IDs + optionally create placeholders
            var referencedEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ch in ev.choices)
            {
                if (!string.IsNullOrWhiteSpace(ch.nextEventId))
                    referencedEventIds.Add(ch.nextEventId);

                foreach (var o in ch.outcomes)
                {
                    var t = (o.type ?? "").Trim();

                    // ensure assets for various outcome targets (optional)
                    if (t.Equals("ModifyResource", StringComparison.OrdinalIgnoreCase))
                        EnsureResource(o.targetId, createPlaceholders, placeholdersRoot, registerNewAssetsInGameData);

                    else if (t.Equals("ModifyNarrativeVariable", StringComparison.OrdinalIgnoreCase) ||
                             t.Equals("SetNarrativeBoolTrue",  StringComparison.OrdinalIgnoreCase) ||
                             t.Equals("SetNarrativeBoolFalse", StringComparison.OrdinalIgnoreCase))
                        EnsureVariable(o.targetId, createPlaceholders, placeholdersRoot, registerNewAssetsInGameData);

                    else if (t.Equals("TriggerEvent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(o.targetId))
                            referencedEventIds.Add(o.targetId);

                        EnsureEvent(o.targetId, createPlaceholders, placeholdersRoot, registerNewAssetsInGameData);
                    }
                    else if (t.Equals("TriggerStoryArc", StringComparison.OrdinalIgnoreCase))
                    {
                        EnsureStoryArc(o.targetId, createPlaceholders, placeholdersRoot, registerNewAssetsInGameData);
                    }
                    else if (t.Equals("SetPlayerActor", StringComparison.OrdinalIgnoreCase) ||
                             t.Equals("SetRelationship", StringComparison.OrdinalIgnoreCase))
                    {
                        EnsureActor(o.targetId, createPlaceholders, placeholdersRoot, registerNewAssetsInGameData);
                    }
                }
            }

            // 3) Build a minimal scenario wrapper (include stubs for referenced events)
            var sc = new ScenarioJson
            {
                scenarioName    = ev.id + "_Wrapper",
                campaignLength  = 1,
                startingEventId = ev.id,
                requiredFlags   = new List<string>(),
                storyArcs       = new List<object>(),
                resources       = new List<object>(),
                events          = new List<EventJson>()
            };

            // Primary event
            sc.events.Add(new EventJson
            {
                id          = ev.id,
                title       = title,
                description = desc,
                choices     = ev.choices.Select(ConvertChoiceInToOut).ToList()
            });

            // Stub events (to allow importer to link nextEventId/TriggerEvent references)
            foreach (var refId in referencedEventIds)
            {
                if (string.Equals(refId, ev.id, StringComparison.OrdinalIgnoreCase)) continue;

                sc.events.Add(new EventJson
                {
                    id          = refId,
                    title       = $"[TODO] {refId}",
                    description = "",
                    choices     = new List<ChoiceJson>() // empty for now
                });
            }

            // 4) Serialize and invoke existing ScenarioJsonImporter
            string wrapperJson = JsonUtility.ToJson(sc, prettyPrint: true);

            var scenarioImporter = ScriptableObject.CreateInstance<ScenarioJsonImporter>();
            // Call ImportScenario via reflection (works whether public or private)
            var mi = typeof(ScenarioJsonImporter)
                .GetMethod("ImportScenario", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (mi == null)
                throw new Exception("SingleEventAdapterImporter: ScenarioJsonImporter.ImportScenario(string) not found.");

            mi.Invoke(scenarioImporter, new object[] { wrapperJson });

            Debug.Log("[SingleEventAdapterImporter] Imported single event via Scenario importer.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  JSON models (input single-event + wrapper scenario)
        // ─────────────────────────────────────────────────────────────────────
        [Serializable] private sealed class SingleEventJson
        {
            public string id;
            public string type;              // optional hint, ignored by wrapper currently
            public string title;
            public string description;
            public string titleKey;          // optional localization key fallback
            public string descriptionKey;
            public List<ChoiceJsonIn> choices;
        }

        [Serializable] private sealed class ChoiceJsonIn
        {
            public string text;
            public bool goToNextDay;
            public string nextEventId;
            public List<OutcomeJsonIn> outcomes;
        }

        [Serializable] private sealed class OutcomeJsonIn
        {
            public string type;             // string → mapped to OutcomeType
            public string targetId;
            public float  value;
            public float  probability;
            public string description;
        }

        [Serializable] private sealed class ScenarioJson
        {
            public string scenarioName;
            public int    campaignLength;
            public string startingEventId;
            public List<string>  requiredFlags;
            public List<object>  storyArcs;
            public List<object>  resources;
            public List<EventJson> events;
        }

        [Serializable] private sealed class EventJson
        {
            public string id;
            public string title;
            public string description;
            public List<ChoiceJson> choices;
        }

        [Serializable] private sealed class ChoiceJson
        {
            public string text;
            public bool   goToNextDay;
            public string nextEventId;
            public List<OutcomeJson> outcomes;
        }

        [Serializable] private sealed class OutcomeJson
        {
            public OutcomeType type;        // enum (JsonUtility serializes as int)
            public string  targetId;
            public float   value;
            public float   probability;     // 0..1
            public string  description;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Converters & helpers
        // ─────────────────────────────────────────────────────────────────────
        private static ChoiceJson ConvertChoiceInToOut(ChoiceJsonIn ch)
        {
            var outOutcomes = new List<OutcomeJson>();
            foreach (var o in ch.outcomes ?? new List<OutcomeJsonIn>())
            {
                outOutcomes.Add(new OutcomeJson
                {
                    type        = ParseOutcomeType(o.type),
                    targetId    = o.targetId ?? "",
                    value       = o.value,
                    probability = (o.probability <= 0f) ? 1f : Mathf.Clamp01(o.probability),
                    description = o.description ?? ""
                });
            }

            return new ChoiceJson
            {
                text        = string.IsNullOrWhiteSpace(ch.text) ? "Continue" : ch.text,
                goToNextDay = ch.goToNextDay,
                nextEventId = ch.nextEventId,
                outcomes    = outOutcomes
            };
        }

        private static OutcomeType ParseOutcomeType(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return OutcomeType.ModifyResource;
            s = s.Trim();

            // quick aliases
            if (s.Equals("add_flag",     StringComparison.OrdinalIgnoreCase)) return OutcomeType.AddFlag;
            if (s.Equals("remove_flag",  StringComparison.OrdinalIgnoreCase)) return OutcomeType.RemoveFlag;
            if (s.Equals("trigger",      StringComparison.OrdinalIgnoreCase)) return OutcomeType.TriggerEvent;

            if (Enum.TryParse<OutcomeType>(s, true, out var result)) return result;

            Debug.LogWarning($"[SingleEventAdapterImporter] Unknown outcome type '{s}', defaulting to ModifyResource.");
            return OutcomeType.ModifyResource;
        }

        private static string MakeIdFromTitle(string title)
        {
            var s = (title ?? "IMPORTED_EVENT").Trim();
            var arr = s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
            s = new string(arr);
            while (s.Contains("__")) s = s.Replace("__", "_");
            s = s.Trim('_');
            return string.IsNullOrEmpty(s) ? "imported_event" : s;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Placeholder creators (optional)
        // ─────────────────────────────────────────────────────────────────────
        private static T LoadByExactName<T>(string name) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj  = AssetDatabase.LoadAssetAtPath<T>(path);
                if (obj != null && obj.name.Equals(name, StringComparison.Ordinal)) return obj;
            }
            return null;
        }

        private static T CreatePlaceholder<T>(string name, string root) where T : ScriptableObject
        {
            EnsureFolder(root);
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            var path = $"{root}/{name}.asset";
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\','/') ?? "Assets";
            var leaf   = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void TryRegister(GameData gd, UnityEngine.Object obj)
        {
            if (gd == null || obj == null) return;

            if (obj is Resource r)                 gd.AddResource(r);
            else if (obj is NarrativeVariableSO v) gd.AddNarrativeVariable(v);
            else if (obj is SimEventSO e)          gd.AddEvent(e);
            else if (obj is StoryArcSO arc)        gd.AddStoryArc(arc);
            else if (obj is ActorSO ac)            gd.AddActor(ac);

            EditorUtility.SetDirty(gd);
        }

        private static Resource EnsureResource(string id, bool enabled, string root, GameData gd)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id)) return null;
            var existing = LoadByExactName<Resource>(id);
            if (existing) return existing;

            var res = CreatePlaceholder<Resource>(id, root);
            // Initialize a sensible minimal state (via SerializedObject constants).
            SetStringSerialized(res, Resource.PROP_DISPLAY_NAME, id);
            SetStringSerialized(res, Resource.PROP_TARGET_ID,    id);
            EditorUtility.SetDirty(res);
            TryRegister(gd, res);
            return res;
        }

        private static NarrativeVariableSO EnsureVariable(string id, bool enabled, string root, GameData gd)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id)) return null;
            var existing = LoadByExactName<NarrativeVariableSO>(id);
            if (existing) return existing;

            var varSo = CreatePlaceholder<NarrativeVariableSO>(id, root);
            varSo.Initialize(id, NarrativeVariableSO.VariableType.Float, 0f, false);
            EditorUtility.SetDirty(varSo);
            TryRegister(gd, varSo);
            return varSo;
        }

        private static SimEventSO EnsureEvent(string id, bool enabled, string root, GameData gd)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id)) return null;
            var existing = LoadByExactName<SimEventSO>(id);
            if (existing) return existing;

            var ev = CreatePlaceholder<SimEventSO>(id, root);
            ev.simEventId          = id;
            ev.simEventTitle       = $"[TODO] {id}";
            ev.simEventDescription = "";
            ev.Type                = SimEventSO.EventType.Main;
            ev.MinTimeUnit         = 1;
            ev.MaxTimeUnit         = 30;
            ev.Probability         = 1f;
            EditorUtility.SetDirty(ev);
            TryRegister(gd, ev);
            return ev;
        }

        private static StoryArcSO EnsureStoryArc(string id, bool enabled, string root, GameData gd)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id)) return null;
            var existing = LoadByExactName<StoryArcSO>(id);
            if (existing) return existing;

            var arc = CreatePlaceholder<StoryArcSO>(id, root);
            arc.Initialize(id, "");
            EditorUtility.SetDirty(arc);
            TryRegister(gd, arc);
            return arc;
        }

        private static ActorSO EnsureActor(string id, bool enabled, string root, GameData gd)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id)) return null;
            var existing = LoadByExactName<ActorSO>(id);
            if (existing) return existing;

            var ac = CreatePlaceholder<ActorSO>(id, root);
            // Initialize display name using ActorSO’s public constant if available.
            TrySetStringField(ac, ActorSO.PROP_NAME, "_displayName", "_name", id);
            EditorUtility.SetDirty(ac);
            TryRegister(gd, ac);
            return ac;
        }

        // Helper for tolerant string assignment (checks multiple property names).
        private static void TrySetStringField(UnityEngine.Object obj, params string[] nameThenValue)
        {
            if (obj == null || nameThenValue == null || nameThenValue.Length < 2) return;

            string value = nameThenValue[^1];
            var so = new SerializedObject(obj);
            bool set = false;

            for (int i = 0; i < nameThenValue.Length - 1; i++)
            {
                var p = so.FindProperty(nameThenValue[i]);
                if (p != null && p.propertyType == SerializedPropertyType.String)
                {
                    p.stringValue = value;
                    set = true;
                    break;
                }
            }

            if (set) so.ApplyModifiedProperties();
        }

        private static void SetStringSerialized(UnityEngine.Object obj, string propName, string value)
        {
            if (obj == null) return;
            var so = new SerializedObject(obj);
            var p  = so.FindProperty(propName);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            {
                p.stringValue = value;
                so.ApplyModifiedProperties();
            }
        }
    }
}
#endif
