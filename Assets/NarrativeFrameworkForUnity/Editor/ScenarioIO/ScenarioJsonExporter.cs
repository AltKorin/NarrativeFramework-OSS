#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using SimulationGame.Editor.ScenarioIO;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Exports a GameData (v1.1 snapshot) to ScenarioJson.
    /// Tolerant: reads via public properties where available, then SerializedObject (fallbacks).
    /// </summary>
    public class ScenarioJsonExporter : EditorWindow
    {
        [SerializeField] private GameData _source;

        [MenuItem("Tools/Narrative Framework/Scenario IO/Export Scenario JSON", priority = 501)]
        public static void ShowWindow()
        {
            GetWindow<ScenarioJsonExporter>("Export Scenario JSON");
        }

        private void OnGUI()
        {
            GUILayout.Label("Export Scenario to JSON", EditorStyles.boldLabel);
            _source = (GameData)EditorGUILayout.ObjectField(new GUIContent("GameData"), _source, typeof(GameData), false);

            if (_source == null)
            {
                EditorGUILayout.HelpBox("Assign the GameData you want to export.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Export to JSON..."))
            {
                Export(_source);
            }
        }

        private static SerializedProperty FindAny(SerializedObject so, params string[] names)
        {
            foreach (var n in names)
            {
                var p = so.FindProperty(n);
                if (p != null) return p;
            }
            return null;
        }

        private static string GetString(SerializedObject so, params string[] names)
        {
            var p = FindAny(so, names);
            return p != null ? p.stringValue : null;
        }
        private static bool GetBool(SerializedObject so, bool def, params string[] names)
        {
            var p = FindAny(so, names);
            return p != null && p.propertyType == SerializedPropertyType.Boolean ? p.boolValue : def;
        }
        private static int GetInt(SerializedObject so, int def, params string[] names)
        {
            var p = FindAny(so, names);
            return p != null && p.propertyType == SerializedPropertyType.Integer ? p.intValue : def;
        }
        private static float GetFloat(SerializedObject so, float def, params string[] names)
        {
            var p = FindAny(so, names);
            return p != null && p.propertyType == SerializedPropertyType.Float ? p.floatValue : def;
        }

        private void Export(GameData gd)
        {
            string savePath = EditorUtility.SaveFilePanel("Save Scenario JSON", Application.dataPath, "scenario.json", "json");
            if (string.IsNullOrEmpty(savePath)) return;

            var scenario = new ScenarioJson();
            scenario.scenarioName = gd.name;

            var so = new SerializedObject(gd);

            // campaign length
            scenario.campaignLength = GetInt(so, 10, "_campaignLength", "campaignLength");

            // flags
            var pFlags = FindAny(so, "_requiredFlags", "requiredFlags");
            if (pFlags != null && pFlags.isArray)
            {
                scenario.flags.Clear();
                for (int i = 0; i < pFlags.arraySize; i++)
                    scenario.flags.Add(pFlags.GetArrayElementAtIndex(i).stringValue);
            }

            // variables
            var pVars = FindAny(so, "_narrativeVariables", "narrativeVariables", "Variables", "_variables");
            if (pVars != null && pVars.isArray)
            {
                for (int i = 0; i < pVars.arraySize; i++)
                {
                    var v = (NarrativeVariableSO)pVars.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (v == null) continue;
                    var vso = new SerializedObject(v);

                    var item = new VariableJson();
                    item.uid = GetString(vso, "_variableId", "VariableId", "id", "Id") ?? v.name;

                    // read enum name as string
                    var pType = FindAny(vso, "_type", "type", "Type");
                    item.type = pType != null && pType.propertyType == SerializedPropertyType.Enum
                        ? pType.enumDisplayNames[pType.enumValueIndex]
                        : "Bool";

                    item.defaultBool   = GetBool (vso, false, "_defaultBool", "defaultBool");
                    item.defaultFloat  = GetFloat(vso, 0,     "_defaultFloat", "defaultFloat");
                    item.defaultInt    = GetInt  (vso, 0,     "_defaultInt", "defaultInt");
                    item.defaultString = GetString(vso, "_defaultString", "defaultString");
                    item.title         = GetString(vso, "_title", "title", "displayName");

                    scenario.variables.Add(item);
                }
            }

            // resources
            var idForResource = new Dictionary<Resource, string>();
            var pRes = FindAny(so, "_resources", "resources");
            if (pRes != null && pRes.isArray)
            {
                for (int i = 0; i < pRes.arraySize; i++)
                {
                    var r = (Resource)pRes.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (r == null) continue;
                    var rso = new SerializedObject(r);
                    var item = new ResourceJson
                    {
                        uid = GetString(rso, "_id", "Id", "id") ?? r.name,
                        initialValue = GetFloat(rso, 0, "_initialValue", "initialValue", "InitialValue")
                    };
                    scenario.resources.Add(item);
                    idForResource[r] = item.uid;
                }
            }

            // actor types: will be collected from actors below
            var idForActorType = new Dictionary<ActorTypeSO, string>();

            // actors
            var idForActor = new Dictionary<ActorSO, string>();
            var pActors = FindAny(so, "_actors", "actors");
            if (pActors != null && pActors.isArray)
            {
                for (int i = 0; i < pActors.arraySize; i++)
                {
                    var a = (ActorSO)pActors.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (a == null) continue;
                    var aso = new SerializedObject(a);

                    var item = new ActorJson();
                    item.uid = a.name;
                    item.displayName = GetString(aso, ActorSO.PROP_NAME, "_actorName", "displayName", "_displayName", "_name") ?? a.name;

                    var typeRef = (ActorTypeSO)(FindAny(aso, "_actorType", "actorType", "Type")?.objectReferenceValue);
                    if (typeRef != null)
                    {
                        var tso = new SerializedObject(typeRef);
                        var typeUid = typeRef.name;
                        var disp = GetString(tso, "_displayName", "displayName", "Title", "_title", "_name") ?? typeUid;

                        if (!idForActorType.ContainsKey(typeRef))
                        {
                            scenario.actorTypes.Add(new ActorTypeJson { uid = typeUid, displayName = disp });
                            idForActorType[typeRef] = typeUid;
                        }
                        item.typeUid = idForActorType[typeRef];
                    }

                    scenario.actors.Add(item);
                    idForActor[a] = item.uid;
                }
            }

            // relationships (if present on GameData)
            var pRelDefs = FindAny(so, "_relationshipDefinitions", "relationshipDefinitions");
            if (pRelDefs != null && pRelDefs.isArray)
            {
                for (int i = 0; i < pRelDefs.arraySize; i++)
                {
                    var elem = pRelDefs.GetArrayElementAtIndex(i);
                    var def = new RelationshipDefJson();
                    def.uid = elem.FindPropertyRelative("uid")?.stringValue;
                    def.title = elem.FindPropertyRelative("title")?.stringValue;
                    var pMin = elem.FindPropertyRelative("min"); if (pMin != null && pMin.propertyType == SerializedPropertyType.Integer) def.min = pMin.intValue;
                    var pMax = elem.FindPropertyRelative("max"); if (pMax != null && pMax.propertyType == SerializedPropertyType.Integer) def.max = pMax.intValue;
                    if (!string.IsNullOrEmpty(def.uid))
                        scenario.relationships.definitions.Add(def);
                }
            }
            var pRelInit = FindAny(so, "_relationshipInitialValues", "relationshipInitialValues");
            if (pRelInit != null && pRelInit.isArray)
            {
                for (int i = 0; i < pRelInit.arraySize; i++)
                {
                    var elem = pRelInit.GetArrayElementAtIndex(i);
                    var rv = new RelationshipValueJson();
                    rv.actorUid = elem.FindPropertyRelative("actorUid")?.stringValue;
                    rv.defUid   = elem.FindPropertyRelative("defUid")?.stringValue;
                    var pVal = elem.FindPropertyRelative("value");
                    if (pVal != null && pVal.propertyType == SerializedPropertyType.Integer) rv.value = pVal.intValue;
                    if (!string.IsNullOrEmpty(rv.actorUid) && !string.IsNullOrEmpty(rv.defUid))
                        scenario.relationships.initialValues.Add(rv);
                }
            }

            // events
            var idForEvent = new Dictionary<SimEventSO, string>();
            var pEvents = FindAny(so, "_events", "events");
            if (pEvents != null && pEvents.isArray)
            {
                for (int i = 0; i < pEvents.arraySize; i++)
                {
                    var ev = (SimEventSO)pEvents.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (ev == null) continue;

                    var evSO = new SerializedObject(ev);

                    var ej = new EventJson
                    {
                        id = ev.name,
                        title = ev.Title,                                  // keep using public Title if present
                        description = ev.Description,                       // keep using public Description if present
                        // IMPORTANT FIX: do NOT call ev.IsEnding (it may not exist)
                        isEnding = GetBool(evSO, false, "_isEnding", "isEnding", "IsEnding")
                    };
                    idForEvent[ev] = ej.id;

                    // choices
                    var pChoices = FindAny(evSO, "_choices", "choices");
                    if (pChoices != null && pChoices.isArray)
                    {
                        for (int c = 0; c < pChoices.arraySize; c++)
                        {
                            var ch = (Choice)pChoices.GetArrayElementAtIndex(c).objectReferenceValue;
                            if (ch == null) continue;

                            var chSO = new SerializedObject(ch);
                            var cj = new ChoiceJson
                            {
                                text = GetString(chSO, "_text", "text", "Text") ?? ch.name,
                                goToNextDay = (FindAny(chSO, "GoToNextDay", "_goToNextDay")?.boolValue) ?? false
                            };
                            var nextRef = (SimEventSO)(FindAny(chSO, "_nextEvent", "nextEvent")?.objectReferenceValue);
                            if (nextRef != null) cj.nextEventId = nextRef.name;

                            // outcomes
                            var pOut = FindAny(chSO, "outcomes", "_outcomes");
                            if (pOut != null && pOut.isArray)
                            {
                                for (int o = 0; o < pOut.arraySize; o++)
                                {
                                    var oc = (Outcome)pOut.GetArrayElementAtIndex(o).objectReferenceValue;
                                    if (oc == null) continue;

                                    var ocSO = new SerializedObject(oc);
                                    // enum name
                                    string typeName = "ModifyResource";
                                    var pType = FindAny(ocSO, "_type", "type", "Type");
                                    if (pType != null && pType.propertyType == SerializedPropertyType.Enum)
                                        typeName = pType.enumDisplayNames[pType.enumValueIndex];

                                    float prob = (FindAny(ocSO, "_probability", "probability")?.floatValue) ?? 1f;
                                    float val  = (FindAny(ocSO, "_value", "value")?.floatValue) ?? 0f;
                                    string desc= GetString(ocSO, "_outcomeDescription", "outcomeDescription") ?? string.Empty;

                                    // generic target id from resource/event/flag/variable/relationship
                                    string targetId = null;

                                    var resRef = (Resource)(FindAny(ocSO, "targetResource", "_targetResource")?.objectReferenceValue);
                                    if (resRef != null)
                                    {
                                        if (!idForResource.TryGetValue(resRef, out targetId))
                                        {
                                            var rrso = new SerializedObject(resRef);
                                            targetId = GetString(rrso, "_id", "Id", "id") ?? resRef.name;
                                        }
                                    }
                                    var evRef = (SimEventSO)(FindAny(ocSO, "targetEvent", "_targetEvent")?.objectReferenceValue);
                                    if (evRef != null) targetId = evRef.name;

                                    var flagId = GetString(ocSO, "_flagId", "flagId", "FlagId");
                                    if (!string.IsNullOrEmpty(flagId)) targetId = flagId;

                                    var varRef = (NarrativeVariableSO)(FindAny(ocSO, "_targetVariable", "targetVariable")?.objectReferenceValue);
                                    if (varRef != null)
                                    {
                                        var vrso = new SerializedObject(varRef);
                                        targetId = GetString(vrso, "_variableId", "VariableId", "id", "Id") ?? varRef.name;
                                    }

                                    var relDefId = GetString(ocSO, "_relationshipDefId", "relationshipDefId");
                                    if (!string.IsNullOrEmpty(relDefId)) targetId = relDefId;

                                    cj.outcomes.Add(new OutcomeJson
                                    {
                                        type = typeName,
                                        targetId = targetId,
                                        value = val,
                                        probability = prob,
                                        description = desc
                                    });
                                }
                            }

                            ej.choices.Add(cj);
                        }
                    }

                    scenario.events.Add(ej);
                }
            }

            // starting event
            var pStart = FindAny(so, "_startingEvent", "startingEvent");
            if (pStart != null && pStart.objectReferenceValue != null)
                scenario.startingEventId = pStart.objectReferenceValue.name;

            // story arcs
            var pArcs = FindAny(so, "_storyArcs", "storyArcs");
            if (pArcs != null && pArcs.isArray)
            {
                for (int i = 0; i < pArcs.arraySize; i++)
                {
                    var arc = (StoryArcSO)pArcs.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (arc == null) continue;

                    var aSO = new SerializedObject(arc);
                    var aj = new StoryArcJson
                    {
                        uid = arc.name,
                        title = GetString(aSO, "_arcName", "arcName", "Title", "_title", "_name") ?? arc.name,
                        description = GetString(aSO, "_arcDescription", "arcDescription", "Description", "_description") ?? string.Empty,
                        allowMultipleActivations = GetBool(aSO, false, "_allowMultipleActivations", "allowMultipleActivations"),
                        minDay = GetInt(aSO, 0, "_minDay", "minDay"),
                        maxDay = GetInt(aSO, 99999, "_maxDay", "maxDay")
                    };

                    var pReq = FindAny(aSO, "_requiredFlags", "requiredFlags");
                    if (pReq != null && pReq.isArray)
                        for (int r = 0; r < pReq.arraySize; r++)
                            aj.requiredFlags.Add(pReq.GetArrayElementAtIndex(r).stringValue);

                    var pForb = FindAny(aSO, "_forbiddenFlags", "forbiddenFlags");
                    if (pForb != null && pForb.isArray)
                        for (int r = 0; r < pForb.arraySize; r++)
                            aj.forbiddenFlags.Add(pForb.GetArrayElementAtIndex(r).stringValue);

                    var pReqEv = FindAny(aSO, "_requiredEvents", "requiredEvents");
                    if (pReqEv != null && pReqEv.isArray)
                        for (int r = 0; r < pReqEv.arraySize; r++)
                        {
                            var ev = (SimEventSO)pReqEv.GetArrayElementAtIndex(r).objectReferenceValue;
                            if (ev != null) aj.requiredEvents.Add(ev.name);
                        }

                    var pArcEv = FindAny(aSO, "_eventsInArc", "eventsInArc");
                    if (pArcEv != null && pArcEv.isArray)
                        for (int r = 0; r < pArcEv.arraySize; r++)
                        {
                            var ev = (SimEventSO)pArcEv.GetArrayElementAtIndex(r).objectReferenceValue;
                            if (ev != null) aj.eventsInArc.Add(ev.name);
                        }

                    var pTrig = FindAny(aSO, "_triggeredEvent", "triggeredEvent");
                    if (pTrig != null && pTrig.objectReferenceValue != null)
                        aj.triggeredEventId = pTrig.objectReferenceValue.name;

                    scenario.storyArcs.Add(aj);
                }
            }

            // story stacks
            var pStacks = FindAny(so, "_storyStacks", "storyStacks");
            if (pStacks != null && pStacks.isArray)
            {
                for (int i = 0; i < pStacks.arraySize; i++)
                {
                    var stack = (StoryStackSO)pStacks.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (stack == null) continue;

                    var stSO = new SerializedObject(stack);
                    var sj = new StoryStackJson { uid = stack.name };

                    var pItems = FindAny(stSO, "_items", "items", "Entries", "_entries");
                    if (pItems != null && pItems.isArray)
                    {
                        for (int r = 0; r < pItems.arraySize; r++)
                        {
                            var elem = pItems.GetArrayElementAtIndex(r);
                            var evRef = (SimEventSO)(elem.FindPropertyRelative("_event") ??
                                                     elem.FindPropertyRelative("eventRef") ??
                                                     elem.FindPropertyRelative("Event"))?.objectReferenceValue;
                            float w = 1f;
                            var pW = elem.FindPropertyRelative("_weight") ?? elem.FindPropertyRelative("weight") ?? elem.FindPropertyRelative("Weight");
                            if (pW != null && pW.propertyType == SerializedPropertyType.Float) w = pW.floatValue;

                            if (evRef != null) sj.items.Add(new StoryStackItemJson { eventId = evRef.name, weight = w });
                        }
                    }

                    scenario.storyStacks.Add(sj);
                }
            }

            // endings
            var pCamp = FindAny(so, "_campaignEnding", "campaignEnding");
            if (pCamp != null && pCamp.objectReferenceValue != null)
                scenario.endings.campaignEndingEventId = pCamp.objectReferenceValue.name;

            scenario.endings.enableResourceDeaths = GetBool(so, true, "_enableResourceDeaths", "enableResourceDeaths");

            var pRules = FindAny(so, "_resourceFailRules", "resourceFailRules");
            if (pRules != null && pRules.isArray)
            {
                for (int i = 0; i < pRules.arraySize; i++)
                {
                    var elem = pRules.GetArrayElementAtIndex(i);
                    var rj = new ResourceDeathRuleJson();

                    var resRef = (Resource)(elem.FindPropertyRelative("resource") ??
                                            elem.FindPropertyRelative("_resource") ??
                                            elem.FindPropertyRelative("resourceRef"))?.objectReferenceValue;
                    if (resRef != null)
                    {
                        var rrso = new SerializedObject(resRef);
                        rj.resourceUid = GetString(rrso, "_id", "Id", "id") ?? resRef.name;
                    }
                    var pTh = elem.FindPropertyRelative("threshold") ?? elem.FindPropertyRelative("_threshold");
                    if (pTh != null && pTh.propertyType == SerializedPropertyType.Float) rj.threshold = pTh.floatValue;

                    var endRef = (SimEventSO)(elem.FindPropertyRelative("endingEvent") ??
                                              elem.FindPropertyRelative("_endingEvent"))?.objectReferenceValue;
                    if (endRef != null) rj.endingEventId = endRef.name;

                    rj.flagOnDeath = elem.FindPropertyRelative("flagOnDeath")?.stringValue ??
                                     elem.FindPropertyRelative("_flagOnDeath")?.stringValue;

                    scenario.endings.resourceDeathRules.Add(rj);
                }
            }

            // write file (pretty)
            var json = JsonUtility.ToJson(scenario, true);
            File.WriteAllText(savePath, json);
            EditorUtility.RevealInFinder(savePath);
            EditorUtility.DisplayDialog("Scenario Export", "Scenario JSON exported.", "OK");
        }
    }
}
#endif
