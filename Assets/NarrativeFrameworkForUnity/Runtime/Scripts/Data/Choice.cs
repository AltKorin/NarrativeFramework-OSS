using System.Collections.Generic;
using UnityEngine;
using SimulationGame.Interfaces;

namespace SimulationGame
{
    /// <summary>
    /// One decision card: text, flag gates, outcomes, and an optional link
    /// to another SimEvent or straight to next day.
    /// </summary>
    [CreateAssetMenu(fileName = "Choice", menuName = "Narrative Framework/Choice")]
    public class Choice : NarrativeObject
    {
        /* ─────────────────────────────────────────────
         *  Constants – property names for editor scripts
         * ───────────────────────────────────────────── */
        public const string PROP_ID            = "_choiceId";
        public const string PROP_TEXT          = "_choiceText";
        public const string PROP_REQ_FLAGS     = "_requiredFlags";
        public const string PROP_FORB_FLAGS    = "_forbiddenFlags";
        public const string PROP_OUTCOMES      = "_outcomes";
        public const string PROP_NEXT_EVENT    = "_nextEvent";
        public const string PROP_GO_NEXT_DAY   = "_goToNextDay";
        public const string PROP_MIN_TIME_UNIT = "_minChoiceTimeUnit";

        /* ─────────────────────────────────────────────
         *  Serialized data
         * ───────────────────────────────────────────── */
        [SerializeField] private string _choiceId = "choice_01";
        [SerializeField] [TextArea(2, 4)] private string _choiceText = "Do the thing?";
        [SerializeField] private List<string> _requiredFlags  = new();
        [SerializeField] private List<string> _forbiddenFlags = new();
        [SerializeField] private List<Outcome> _outcomes      = new();

        [SerializeField] private SimEventSO _nextEvent;
        [SerializeField] private bool _goToNextDay        = false;
        [SerializeField] private int  _minChoiceTimeUnit  = 1;

        /* ─────────────────────────────────────────────
         *  Public read‑only accessors
         * ───────────────────────────────────────────── */
        public string ChoiceId            => _choiceId;
        public string ChoiceText
        {
            get => _choiceText;
            set => _choiceText = value;           // designers may tweak at runtime
        }

        public IReadOnlyList<string> RequiredFlags  => _requiredFlags;
        public IReadOnlyList<string> ForbiddenFlags => _forbiddenFlags;
        public IReadOnlyList<Outcome> Outcomes      => _outcomes;

        public SimEventSO NextEvent => _nextEvent;

        public bool GoToNextDay
        {
            get => _goToNextDay;
            set => _goToNextDay = value;
        }

        public int MinChoiceTimeUnit => _minChoiceTimeUnit;

        /* ─────────────────────────────────────────────
         *  Runtime helpers
         * ───────────────────────────────────────────── */
        public void Initialize(string choiceId,
                               string choiceText,
                               IEnumerable<string> requiredFlags  = null,
                               IEnumerable<string> forbiddenFlags = null,
                               bool goToNextDay   = false,
                               int  minTimeUnit   = 1)
        {
            _choiceId    = choiceId;
            _choiceText  = choiceText;
            _goToNextDay = goToNextDay;
            _minChoiceTimeUnit = minTimeUnit;

            _requiredFlags .Clear();
            _forbiddenFlags.Clear();

            if (requiredFlags  != null) _requiredFlags .AddRange(requiredFlags);
            if (forbiddenFlags != null) _forbiddenFlags.AddRange(forbiddenFlags);
        }

        public void AddOutcome(Outcome outcome) => _outcomes.Add(outcome);
        public void SetNextEvent(SimEventSO nextEvent) => _nextEvent = nextEvent;

        /// <summary>True if current day/flags allow this choice to appear.</summary>
        public bool IsAvailable(int currentDay, IEnumerable<string> currentFlags)
        {
            if (currentDay < _minChoiceTimeUnit) return false;

            var flagSet = new HashSet<string>(currentFlags);
            foreach (var f in _requiredFlags)  if (!flagSet.Contains(f)) return false;
            foreach (var f in _forbiddenFlags) if ( flagSet.Contains(f)) return false;

            return true;
        }

        /// <summary>Execute every outcome in the list.</summary>
        public void ApplyOutcomes(ISimulationManager sim)
        {
            // iterate over a copy in case outcomes modify the list
            foreach (var outcome in new List<Outcome>(_outcomes))
                outcome?.Apply(sim);
        }

#if UNITY_EDITOR
        /* Keep IDs sane when designers duplicate assets */
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_choiceId))
                _choiceId = name.ToLowerInvariant().Replace(' ', '_');
        }
#endif
    }
}
