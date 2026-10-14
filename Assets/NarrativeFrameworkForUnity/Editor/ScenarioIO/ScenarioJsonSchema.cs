#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace SimulationGame.Editor.ScenarioIO
{
    /// <summary>
    /// JSON schema for full scenario import/export.
    /// v1.1: adds variables, actorTypes/actors, relationships, storyStacks, endings (campaign + resource-death rules), flags.
    /// Keep this stable; bump version if you evolve it and handle migrations in importer.
    /// </summary>
    [Serializable]
    public class ScenarioJson
    {
        public string version = "1.1.0";
        public string scenarioName = "NewScenario";
        public int campaignLength = 10;

        public List<string> flags = new();

        public List<VariableJson> variables = new();
        public List<ResourceJson> resources = new();
        public List<ActorTypeJson> actorTypes = new();
        public List<ActorJson> actors = new();

        public RelationshipBlock relationships = new();

        public List<StoryArcJson> storyArcs = new();
        public List<StoryStackJson> storyStacks = new();

        public List<EventJson> events = new();

        public EndingsJson endings = new();

        public string startingEventId;
    }

    // -------- Game systems --------

    [Serializable]
    public class VariableJson
    {
        public string uid;
        public string type; // Bool | Float | Int | String
        public bool defaultBool;
        public float defaultFloat;
        public int defaultInt;
        public string defaultString;
        public string title;
    }

    [Serializable]
    public class ResourceJson
    {
        public string uid;
        public float initialValue = 0f;
    }

    [Serializable]
    public class ActorTypeJson
    {
        public string uid;
        public string displayName;
    }

    [Serializable]
    public class ActorJson
    {
        public string uid;
        public string displayName;
        public string typeUid;
    }

    [Serializable]
    public class RelationshipBlock
    {
        public List<RelationshipDefJson> definitions = new();
        public List<RelationshipValueJson> initialValues = new();
    }

    [Serializable]
    public class RelationshipDefJson
    {
        public string uid;
        public string title;
        public int min = -100;
        public int max = 100;
    }

    [Serializable]
    public class RelationshipValueJson
    {
        public string actorUid;
        public string defUid;
        public int value = 0;
    }

    [Serializable]
    public class StoryArcJson
    {
        public string uid;
        public string title;
        public string description;
        public bool allowMultipleActivations;
        public int minDay = 0;
        public int maxDay = 99999;
        public List<string> requiredFlags = new();
        public List<string> forbiddenFlags = new();
        public List<string> requiredEvents = new();
        public List<String> eventsInArc = new();
        public string triggeredEventId; // optional
    }

    [Serializable]
    public class StoryStackJson
    {
        public string uid;
        public List<StoryStackItemJson> items = new();
    }

    [Serializable]
    public class StoryStackItemJson
    {
        public string eventId;
        public float weight = 1f;
    }

    [Serializable]
    public class EventJson
    {
        public string id;
        public string title;
        public string description;
        public bool isEnding = false;
        public List<ChoiceJson> choices = new();
    }

    [Serializable]
    public class ChoiceJson
    {
        public string text;
        public bool goToNextDay;
        public string nextEventId;
        public List<OutcomeJson> outcomes = new();
    }

    [Serializable]
    public class OutcomeJson
    {
        // Outcome type, by enum name in your project (e.g., ModifyResource, SetFlag, ClearFlag, JumpToEvent, TriggerStoryArc, ModifyRelationship, SetVariable).
        public string type;

        // What the outcome targets depends on type. We keep a generic targetId + value + probability + description.
        // - resource: targetId = resource uid
        // - event:    targetId = event id
        // - flag:     targetId = flag string
        // - variable: targetId = variable uid
        // - relationship: targetId = relationship definition uid
        public string targetId;

        public float value = 0;           // generic numeric value for resource deltas, relationship deltas, etc.
        public float probability = 1;     // 0..1
        public string description;
    }

    [Serializable]
    public class EndingsJson
    {
        public string campaignEndingEventId;
        public bool enableResourceDeaths = true;
        public List<ResourceDeathRuleJson> resourceDeathRules = new();
    }

    [Serializable]
    public class ResourceDeathRuleJson
    {
        public string resourceUid;
        public float threshold = 0f;         // when resource <= threshold -> death
        public string endingEventId;         // SimEventSO for death
        public string flagOnDeath;           // optional flag set on death
    }
}
#endif
