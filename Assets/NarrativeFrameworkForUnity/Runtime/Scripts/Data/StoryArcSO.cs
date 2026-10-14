using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// A themed sequence of events that can automatically
    /// trigger when its conditions are satisfied.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStoryArc", menuName = "Narrative Framework/Story Arc")]
    public class StoryArcSO : NarrativeObject
    {
        /* ─────────────────────────────────────────────
         *  Public constants – property names for editors
         * ───────────────────────────────────────────── */
        public const string PROP_NAME = "_arcName";
        public const string PROP_DESCRIPTION = "_arcDescription";
        public const string PROP_ILLUSTRATION = "_arcIllustration";
        public const string PROP_EVENTS = "_eventsInArc";
        public const string PROP_MULTIPLE = "_allowMultipleActivations";
        public const string PROP_MIN_DAY = "_minDay";
        public const string PROP_MAX_DAY = "_maxDay";
        public const string PROP_ACTORS = "_involvedActors";
        public const string PROP_REQ_FLAGS = "_requiredFlags";
        public const string PROP_FORB_FLAGS = "_forbiddenFlags";
        public const string PROP_REQ_EVENTS = "_requiredEvents";
        public const string PROP_EXTRA_COND = "_additionalConditions";
        public const string PROP_TRIGGER_EVENT = "_triggeredEvent";

        /* ─────────────────────────────────────────────
         *  Serialized data
         * ───────────────────────────────────────────── */
        [Header("Meta")]
        [SerializeField] private string _arcName = "Unnamed Arc";

        [SerializeField, TextArea(2, 5)]
        private string _arcDescription = "Describe what this arc is about.";

        [SerializeField] private Sprite _arcIllustration;

        [Header("Story Content")]
        [SerializeField] private List<SimEventSO> _eventsInArc = new();

        [Tooltip("If false, this arc can activate only once per campaign.")]
        [SerializeField] private bool _allowMultipleActivations = false;

        [Header("Activation Window")]
        [SerializeField, Min(1)] private int _minDay = 1;
        [SerializeField, Min(1)] private int _maxDay = 30;

        [Header("Conditions")]
        [SerializeField] private List<ActorSO> _involvedActors = new();
        [SerializeField] private List<string> _requiredFlags = new();
        [SerializeField] private List<string> _forbiddenFlags = new();
        [SerializeField] private List<SimEventSO> _requiredEvents = new();
        [SerializeField] private List<StoryArcCondition> _additionalConditions = new();

        [Header("Triggered Event")]
        [SerializeField] private SimEventSO _triggeredEvent;

        /* runtime‑only flag */
        [HideInInspector] private bool _isActivated = false;

        /* ─────────────────────────────────────────────
         *  Public read‑only accessors
         * ───────────────────────────────────────────── */
        public string ArcName => _arcName;
        public string ArcDescription => _arcDescription;
        public Sprite ArcIllustration => _arcIllustration;
        public IReadOnlyList<SimEventSO> EventsInArc => _eventsInArc;

        public bool AllowMultipleActivations => _allowMultipleActivations;
        public int MinDay => _minDay;
        public int MaxDay => _maxDay;

        public IReadOnlyList<ActorSO> InvolvedActors => _involvedActors;
        public IReadOnlyList<string> RequiredFlags => _requiredFlags;
        public IReadOnlyList<string> ForbiddenFlags => _forbiddenFlags;
        public IReadOnlyList<SimEventSO> RequiredEvents => _requiredEvents;
        public IReadOnlyList<StoryArcCondition> AdditionalConditions => _additionalConditions;

        public SimEventSO TriggeredEvent => _triggeredEvent;
        public bool IsActivated => _isActivated;

        /* ─────────────────────────────────────────────
         *  Core logic
         * ───────────────────────────────────────────── */
        public bool IsAvailable(int currentDay,
                                IEnumerable<string> currentFlags,
                                HashSet<SimEventSO> occurredEvents)
        {
            if (currentDay < _minDay || currentDay > _maxDay) return false;
            if (!_allowMultipleActivations && _isActivated) return false;

            var flagSet = new HashSet<string>(currentFlags);

            foreach (var f in _requiredFlags) if (!flagSet.Contains(f)) return false;
            foreach (var f in _forbiddenFlags) if (flagSet.Contains(f)) return false;
            foreach (var e in _requiredEvents) if (!occurredEvents.Contains(e)) return false;

            foreach (var cond in _additionalConditions)
                if (!cond.Evaluate(GameController.Instance)) return false;

            return true;
        }

        public void MarkAsActivated() => _isActivated = true;

        /// <summary>For debugging or New Game +.</summary>
        public void ResetActivation() => _isActivated = false;

#if UNITY_EDITOR
        /* ─────────────────────────────────────────────
         *  Validation
         * ───────────────────────────────────────────── */
        private void OnValidate()
        {
            if (_minDay < 1) _minDay = 1;
            if (_maxDay < _minDay) _maxDay = _minDay;
        }

        // ---------------------------------------------------------------------
        //  Design-time helpers for wizards / importers
        // ---------------------------------------------------------------------
        public void Initialize(string name, string description)
        {
            _arcName = name;
            _arcDescription = description;
        }

        public void AddEventToArc(SimEventSO ev)
        {
            if (ev != null && !_eventsInArc.Contains(ev))
                _eventsInArc.Add(ev);
        }

        public void AddActorToArc(ActorSO actor)
        {
            if (actor != null && !_involvedActors.Contains(actor))
                _involvedActors.Add(actor);
        }
        // ─────────────────────────────────────────────────────────────
        //  DESIGN-TIME SETTERS FOR EDITOR SCRIPTS / IMPORTERS
        //  (kept outside of runtime builds if you like with #if UNITY_EDITOR)
        // ─────────────────────────────────────────────────────────────
        public void SetArcDescription(string description) => _arcDescription = description;
        public void SetMinDay(int day) => _minDay = Mathf.Max(1, day);
        public void SetMaxDay(int day) => _maxDay = Mathf.Max(_minDay, day);
        public void SetArcIllustration(Sprite sprite) => _arcIllustration = sprite;
        public void SetTriggeredEvent(SimEventSO ev) => _triggeredEvent = ev;
        public void SetRequiredFlags(List<string> list) => _requiredFlags = list ?? new List<string>();
        public void SetForbiddenFlags(List<string> list) => _forbiddenFlags = list ?? new List<string>();
        public void SetRequiredEvents(List<SimEventSO> list) => _requiredEvents = list ?? new List<SimEventSO>();
        public void SetAdditionalConditions(List<StoryArcCondition> list)
                                                                     => _additionalConditions = list ?? new List<StoryArcCondition>();
        public void SetInvolvedActors(List<ActorSO> list) => _involvedActors = list ?? new List<ActorSO>();
#endif
#if UNITY_EDITOR
        /// <summary>
        /// Editor-only cleanup for nulls & duplicates in arc internals.
        /// </summary>
        public void Editor_CleanNullsAndDuplicatesIfAvailable()
        {
            // These fields exist in your StoryArcSO:
            _eventsInArc?.RemoveAll(e => e == null);
            _involvedActors?.RemoveAll(a => a == null);
            _requiredFlags?.RemoveAll(string.IsNullOrWhiteSpace);
            _forbiddenFlags?.RemoveAll(string.IsNullOrWhiteSpace);
            _requiredEvents?.RemoveAll(e => e == null);
            _additionalConditions?.RemoveAll(c => c == null);

            // De-duplicate by reference/value where it makes sense
            Dedup(_eventsInArc);
            Dedup(_involvedActors);
            Dedup(_requiredEvents);
            DedupGeneric(_additionalConditions);
            DedupStrings(_requiredFlags);
            DedupStrings(_forbiddenFlags);

            EditorUtility.SetDirty(this);

            static void Dedup<T>(List<T> list) where T : Object
            {
                if (list == null) return;
                var seen = new HashSet<T>();
                int write = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it && seen.Add(it)) list[write++] = it;
                }
                if (write < list.Count) list.RemoveRange(write, list.Count - write);
            }

            static void DedupGeneric<T>(List<T> list)
            {
                if (list == null) return;
                var seen = new HashSet<T>();
                int write = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it != null && seen.Add(it)) list[write++] = it;
                }
                if (write < list.Count) list.RemoveRange(write, list.Count - write);
            }

            static void DedupStrings(List<string> list)
            {
                if (list == null) return;
                var seen = new HashSet<string>();
                int write = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (!string.IsNullOrWhiteSpace(it) && seen.Add(it)) list[write++] = it;
                }
                if (write < list.Count) list.RemoveRange(write, list.Count - write);
            }
        }
#endif

    }
    
}
