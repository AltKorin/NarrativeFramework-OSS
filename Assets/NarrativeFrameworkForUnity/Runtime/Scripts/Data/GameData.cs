using System.Collections.Generic;
using UnityEngine;
using SimulationGame.DebugTools;
using UnityEditor;

namespace SimulationGame
{
    /// <summary>
    /// Top‑level data asset that holds all content needed
    /// for a single campaign build / vertical slice.
    /// </summary>
    [CreateAssetMenu(fileName = "GameData", menuName = "Narrative Framework/GameData")]
    public partial class GameData : ScriptableObject
    {
        /* ─────────────────────────────────────────────
         *  Public constants – property names for editors
         * ───────────────────────────────────────────── */
        public const string PROP_CAMPAIGN_LENGTH = "_campaignLength";
        public const string PROP_START_EVENT = "_startingEvent";
        public const string PROP_EVENTS = "_events";
        public const string PROP_ARCS = "_storyArcs";
        public const string PROP_ACTORS = "_actors";
        public const string PROP_RELATIONS = "_relationships";
        public const string PROP_RESOURCES = "_resources";
        public const string PROP_FLAGS = "_requiredFlags";
        public const string PROP_VARS = "_narrativeVariables";
        public const string PROP_CUSTOM_OUTCOMES = "_customOutcomes";
        public const string PROP_LAST_EVENT_ID = "_lastEventId";

        /* ─────────────────────────────────────────────
         *  Serialized data
         * ───────────────────────────────────────────── */
        [Header("Campaign")]
        [SerializeField, Range(1, 365)] private int _campaignLength = 30;

        [Header("Starting Event")]
        [SerializeField] private SimEventSO _startingEvent;

        [Header("Narrative / Story Stack")]
        public StoryStackSO DefaultStoryStack;


        [Header("Events & Story")]
        [SerializeField] private List<SimEventSO> _events = new();
        [SerializeField] private List<StoryArcSO> _storyArcs = new();

        [Header("Actors & Relationships")]
        [SerializeField] private List<ActorSO> _actors = new();
        [SerializeField] private List<Relationship> _relationships = new();

        [Header("Economy & Systems")]
        [SerializeField] private List<Resource> _resources = new();
        [SerializeField] private List<string> _requiredFlags = new();
        [SerializeField] private List<NarrativeVariableSO> _narrativeVariables = new();
        [SerializeField] private List<BaseOutcome> _customOutcomes = new();

        [Header("Game Over / Ending")]
        [SerializeField] private bool _enableResourceFailStates = true;
        [SerializeField] private bool _failOnAnyResourceAtMin = true;   // if true, any resource hitting MinValue is a fail
        [SerializeField] private List<ResourceFailRule> _resourceFailRules = new(); // per-resource overrides
        [SerializeField] private SimEventSO _defaultGameOverEvent;       // optional Ending event
        [SerializeField] private string _gameOverFlag = "game_over";     // set when game ends

        [Header("Editor Helpers")]
        [SerializeField] private int _lastEventId = 0; // Keeps track of last used event ID
        public int LastEventId => _lastEventId;

        

        /// <summary>
        /// Increments the last event ID counter and returns the new ID.
        /// </summary>
        public int IncrementEventId()
        {
            _lastEventId++;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return _lastEventId;
        }


        /* ─────────────────────────────────────────────
         *  Public read‑only accessors
         * ───────────────────────────────────────────── */
        public int CampaignLength => _campaignLength;
        public SimEventSO StartingEvent => _startingEvent;
        public IReadOnlyList<SimEventSO> Events => _events;
        public IReadOnlyList<StoryArcSO> StoryArcs => _storyArcs;
        public IReadOnlyList<ActorSO> Actors => _actors;
        public IReadOnlyList<Relationship> Relationships => _relationships;
        public IReadOnlyList<Resource> Resources => _resources;
        public IReadOnlyList<string> RequiredFlags => _requiredFlags;
        public IReadOnlyList<NarrativeVariableSO> NarrativeVariables => _narrativeVariables;
        public IReadOnlyList<BaseOutcome> CustomOutcomes => _customOutcomes;
        public bool EnableResourceFailStates => _enableResourceFailStates;
        public bool FailOnAnyResourceAtMin => _failOnAnyResourceAtMin;
        public IReadOnlyList<ResourceFailRule> ResourceFailRules => _resourceFailRules;
        public SimEventSO DefaultGameOverEvent => _defaultGameOverEvent;
        public string GameOverFlag => _gameOverFlag;

        /* ─────────────────────────────────────────────
         *  CRUD helpers – design‑time use only
         * ───────────────────────────────────────────── */
        public void AddEvent(SimEventSO e) { if (!_events.Contains(e)) _events.Add(e); }
        public void RemoveEvent(SimEventSO e) { _events.Remove(e); }
        public void ClearEvents() => _events.Clear();

        public void AddResource(Resource r) { if (!_resources.Contains(r)) _resources.Add(r); }
        public Resource GetResourceById(string id) => _resources.Find(r => r.name == id);

        public void AddNarrativeVariable(NarrativeVariableSO v)
        { if (!_narrativeVariables.Contains(v)) _narrativeVariables.Add(v); }

        public void AddActor(ActorSO a) { if (!_actors.Contains(a)) _actors.Add(a); }
        public void AddStoryArc(StoryArcSO arc) { if (!_storyArcs.Contains(arc)) _storyArcs.Add(arc); }

        public void SetCampaignLength(int days) => _campaignLength = Mathf.Clamp(days, 1, 365);

        /* ─────────────────────────────────────────────
         *  Runtime / editor life‑cycle
         * ───────────────────────────────────────────── */
        private void OnEnable()
        {
            _narrativeVariables ??= new List<NarrativeVariableSO>();
            _actors ??= new List<ActorSO>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _campaignLength = Mathf.Clamp(_campaignLength, 1, 365);
        }
#endif

        /* ─────────────────────────────────────────────
         *  Singleton access with fallback
         * ───────────────────────────────────────────── */
        private static GameData _instance;
        private static GameData _fallback;

        public static GameData Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = UnityEngine.Resources.Load<GameData>("GameData");
                if (_instance == null)
                {
                    Log.Error("GameData.asset not found in Resources! Creating fallback instance.");
                    _instance = _fallback ??= CreateFallback();
                }
                return _instance;
            }
        }
        public void AddRelationship(Relationship rel) => _relationships.Add(rel);
        public void RemoveRelationship(Relationship rel) => _relationships.Remove(rel);
        private static GameData CreateFallback()
        {
            var gd = CreateInstance<GameData>();
            gd._campaignLength = 1;
            gd._events = new List<SimEventSO>();
            gd._storyArcs = new List<StoryArcSO>();
            gd._actors = new List<ActorSO>();
            gd._relationships = new List<Relationship>();
            gd._resources = new List<Resource>();
            gd._requiredFlags = new List<string>();
            gd._narrativeVariables = new List<NarrativeVariableSO>();
            gd._customOutcomes = new List<BaseOutcome>();

            Log.Warning("Using fallback GameData – please create a proper GameData.asset in the Resources folder.");
            return gd;
        }

        public static void ResetInstance()
        {
            _instance = null;
            _fallback = null;
        }

        /* ─────────────────────────────────────────────
         *  Sanity check helpers
         * ───────────────────────────────────────────── */
        public bool IsValid() =>
            _events != null && _resources != null && _narrativeVariables != null;

        // ─────────────────────────────────────────────────────────────
        //  Ensure lists exist (never null) — call before any mutation
        // ─────────────────────────────────────────────────────────────
        private void EnsureCollections()
        {
            // If your field names differ, adjust them here.
            _events ??= new List<SimEventSO>();
            _storyArcs ??= new List<StoryArcSO>();
            _actors ??= new List<ActorSO>();
            _resources ??= new List<Resource>();
            _narrativeVariables ??= new List<NarrativeVariableSO>();
            _customOutcomes ??= new List<BaseOutcome>();
        }

        // ─────────────────────────────────────────────────────────────
        //  Cleaning: remove nulls & duplicates from all lists
        // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Clean Nulls & Duplicates")]
        public void Editor_CleanNullsAndDuplicates()
        {
            EnsureCollections();

            int removedNulls = 0, removedDuplicates = 0;

            removedNulls += _events.RemoveAll(e => e == null);
            removedNulls += _storyArcs.RemoveAll(a => a == null);
            removedNulls += _actors.RemoveAll(a => a == null);
            removedNulls += _resources.RemoveAll(r => r == null);
            removedNulls += _narrativeVariables.RemoveAll(v => v == null);
            removedNulls += _customOutcomes.RemoveAll(c => c == null);

            // De-duplicate by reference
            removedDuplicates += DeduplicateInPlace(_events);
            removedDuplicates += DeduplicateInPlace(_storyArcs);
            removedDuplicates += DeduplicateInPlace(_actors);
            removedDuplicates += DeduplicateInPlace(_resources);
            removedDuplicates += DeduplicateInPlace(_narrativeVariables);
            removedDuplicates += DeduplicateInPlace(_customOutcomes);

            // If StartingEvent exists but is not listed in Events, add it
            if (StartingEvent && !_events.Contains(StartingEvent))
                _events.Add(StartingEvent);

            // Ask arcs to clean their own internal lists
            foreach (var arc in _storyArcs)
            {
                if (!arc) continue;
                arc.Editor_CleanNullsAndDuplicatesIfAvailable();
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameData] Cleaned: removedNulls={removedNulls}, removedDuplicates={removedDuplicates}");
        }

        private static int DeduplicateInPlace<T>(List<T> list) where T : Object
        {
            int before = list.Count;
            // Keep first occurrence order-stable
            var seen = new HashSet<T>();
            int write = 0;
            for (int read = 0; read < list.Count; read++)
            {
                var item = list[read];
                if (item && seen.Add(item))
                {
                    list[write++] = item;
                }
            }
            if (write < list.Count)
                list.RemoveRange(write, list.Count - write);
            return before - list.Count;
        }

        // Optional: menu command when GameData is selected
        [MenuItem("Tools/Narrative Framework/GameData/Clean Selected")]
        private static void Menu_CleanSelected()
        {
            var gd = Selection.activeObject as GameData;
            if (!gd)
            {
                Debug.LogWarning("[GameData] Select a GameData asset first.");
                return;
            }
            gd.Editor_CleanNullsAndDuplicates();
        }
#endif

    [System.Serializable]
    public class ResourceFailRule
        {
            public Resource Resource;              // SO ref
            public bool Enabled = true;
            public float Threshold;                // when current <= Threshold -> fail
            public SimEventSO SpecificEnding;      // optional Ending event for this failure
            public string FlagOnFail = "";         // optional flag to set
            public string MessageOverride = "";    // optional message in summary
        }
    }
}

