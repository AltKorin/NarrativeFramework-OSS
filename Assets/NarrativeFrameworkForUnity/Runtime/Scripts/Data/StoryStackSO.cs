// Assets/SimulationGame/Runtime/Scripts/Data/StoryStackSO.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// A ScriptableObject that defines a collection of events to be used as a source for "soft continuation".
    /// The GameController can query this stack when a choice doesn't lead to a specific next event,
    /// providing a way to insert semi-random or filler content.
    /// This asset is data-only; the state is managed by the internal StoryStackRuntime class during gameplay.
    /// </summary>
    [CreateAssetMenu(
        fileName = "StoryStack",
        menuName = "Narrative Framework/Story Stack",
        order = 1200)]
    public class StoryStackSO : ScriptableObject
    {
        /// <summary>
        /// Defines the method used to select the next event from the stack.
        /// </summary>
        public enum Mode
        {
            /// <summary>Events are presented in a fixed, predefined order.</summary>
            FixedSequence = 0,
            /// <summary>An event is chosen randomly from a weighted pool, with optional filters.</summary>
            RandomWeighted = 1
        }

        [Header("Basic Configuration")]
        [Tooltip("A unique identifier for this story stack.")]
        [SerializeField] private string _stackId = "StoryStack_Default";
        [Tooltip("The mode used to select the next event.")]
        [SerializeField] private Mode _mode = Mode.FixedSequence;

        [Header("Behavior")]
        [Tooltip("When using RandomWeighted mode, avoids picking an event that was among the last N events chosen. Set to 0 to disable.")]
        [Min(0)] public int avoidLastN = 3;

        [Tooltip("When FixedSequence mode reaches the end, this determines if it should loop back to the beginning.")]
        public bool loopSequence = true;

        [Tooltip("In RandomWeighted mode, if no entries are eligible, this allows the system to ignore the 'avoidLastN' history to find a valid event.")]
        public bool allowRepeatIfNoEligible = true;

        [Tooltip("If true, entries in the random pool that point to a null Event asset will be ignored.")]
        public bool skipNullEntries = true;

        [Tooltip("If true, entries pointing to events of type 'Ending' will be ignored in RandomWeighted mode.")]
        public bool excludeEndingEventsInPool = true;

        [Tooltip("If true, logs warnings to the console if issues are found during event selection (e.g., empty pools, zero weights).")]
        public bool verboseWarnings = true;

        [Header("Content")]
        [Tooltip("The list of events to use when in FixedSequence mode.")]
        [SerializeField] private List<SimEventSO> _sequence = new();

        [Tooltip("The pool of events to draw from when in RandomWeighted mode.")]
        [SerializeField] private List<WeightedEntry> _pool = new();

        #region Public Accessors
        /// <summary>
        /// The unique identifier for this story stack.
        /// </summary>
        public string StackId => string.IsNullOrWhiteSpace(_stackId) ? name : _stackId;

        /// <summary>
        /// The mode used to select the next event (FixedSequence or RandomWeighted).
        /// </summary>
        public Mode StackMode => _mode;

        /// <summary>
        /// A read-only list of events for FixedSequence mode.
        /// </summary>
        public IReadOnlyList<SimEventSO> Sequence => _sequence;

        /// <summary>
        /// A read-only list of weighted entries for RandomWeighted mode.
        /// </summary>
        public IReadOnlyList<WeightedEntry> Pool => _pool;
        #endregion

        /// <summary>
        /// Represents a single entry in the weighted random pool.
        /// Contains the event, its selection weight, and eligibility filters.
        /// </summary>
        [Serializable]
        public class WeightedEntry
        {
            [Tooltip("The event that may be selected.")]
            public SimEventSO eventRef;

            [Tooltip("The relative chance of this event being picked from the eligible pool. Must be >= 0.")]
            [Min(0f)] public float weight = 1f;

            [Header("Eligibility Filters (all must pass)")]
            [Tooltip("To be eligible, all flags in this list must be present in the game state.")]
            public List<string> requiredFlags = new();

            [Tooltip("To be eligible, none of the flags in this list can be present in the game state.")]
            public List<string> forbiddenFlags = new();

            [Tooltip("Optional tags for informational purposes or for use with custom selectors.")]
            public List<string> tags = new();
        }

        #region Editor
#if UNITY_EDITOR
        /// <summary>
        /// [Editor-Only] Validates the Story Stack configuration and reports issues.
        /// </summary>
        [ContextMenu("Validate StoryStack")]
        public void Editor_Validate()
        {
            var issues = new System.Text.StringBuilder();
            int warnings = 0;

            if (_mode == Mode.FixedSequence)
            {
                if (_sequence == null || _sequence.Count == 0)
                    issues.AppendLine("- FixedSequence mode has no events assigned.");
            }
            else if (_mode == Mode.RandomWeighted)
            {
                if (_pool == null || _pool.Count == 0)
                    issues.AppendLine("- RandomWeighted mode has an empty pool.");

                bool anyEligible = false;
                for (int i = 0; i < (_pool?.Count ?? 0); i++)
                {
                    var e = _pool[i];
                    if (e == null) { issues.AppendLine($"- Pool entry at index {i} is null."); continue; }

                    // Fix: build a safe display name and avoid invalid string interpolation.
                    string entryName = e.eventRef != null ? e.eventRef.name : "NULL";

                    if (e.weight <= 0f)
                        issues.AppendLine($"- Pool[{i}] '{entryName}' has zero or negative weight.");

                    if (skipNullEntries && e.eventRef == null)
                        issues.AppendLine($"- Pool[{i}] has a null event reference while 'Skip Null Entries' is enabled.");

                    if (excludeEndingEventsInPool && e.eventRef != null && e.eventRef.Type == SimEventSO.EventType.Ending)
                    {
                        warnings++;
                        issues.AppendLine($"- (Warning) Pool[{i}] '{e.eventRef.name}' is an Ending event and will be skipped.");
                    }

                    if (e != null && e.eventRef != null && e.weight > 0f)
                        anyEligible = true;
                }
                if (!anyEligible) issues.AppendLine("- The pool has no potentially eligible entries (all entries are null, have zero weight, or are otherwise invalid).");
            }

            if (issues.Length == 0)
                UnityEditor.EditorUtility.DisplayDialog("StoryStack Validation", "No issues found.", "OK");
            else
                UnityEditor.EditorUtility.DisplayDialog("StoryStack Validation", "Issues Found:\n\n" + issues.ToString(), "OK");
        }
#endif

        /// <summary>
        /// [Editor-Only] Called when the script is loaded or a value is changed in the Inspector.
        /// Used here to enforce valid data, like preventing negative weights.
        /// </summary>
        private void OnValidate()
        {
            if (_pool != null)
            {
                foreach (var entry in _pool)
                {
                    if (entry == null) continue;
                    if (entry.weight < 0f) entry.weight = 0f;
                }
            }

            if (_sequence != null)
                _sequence.RemoveAll(s => s == null);

            if (avoidLastN < 0) avoidLastN = 0;
        }
        #endregion

        /// <summary>
        /// A small, stateful runtime class that manages the selection process for a single game run.
        /// It tracks the current position in a sequence and the history of recently picked events.
        /// </summary>
        [Serializable]
        public class StoryStackRuntime
        {
            private readonly StoryStackSO _def;
            private readonly Queue<string> _recent; // Stores event IDs to avoid picking them again.
            private int _seqIndex;

            /// <summary>
            /// If the last call to Next() failed, this contains the reason why.
            /// </summary>
            public string LastFailureReason { get; private set; } = null;
            private void SetFail(string reason) { LastFailureReason = reason; }

            public StoryStackRuntime(StoryStackSO definition)
            {
                _def = definition;
                _recent = new Queue<string>(Mathf.Max(0, _def.avoidLastN));
                _seqIndex = 0;
            }

            /// <summary>
            /// Resets the runtime state to its initial values.
            /// </summary>
            public void Reset()
            {
                _seqIndex = 0;
                _recent.Clear();
            }

            /// <summary>
            /// Notifies the runtime that an event has occurred, adding it to the recent history
            /// to prevent it from being picked again immediately.
            /// </summary>
            /// <param name="evt">The event that just occurred.</param>
            public void NotifyOccurred(SimEventSO evt)
            {
                if (_def == null || evt == null) return;
                if (_def.avoidLastN <= 0) return;

                string id = SafeEventId(evt);
                if (string.IsNullOrEmpty(id)) return;

                // Maintain a queue of the last N event IDs.
                _recent.Enqueue(id);
                while (_recent.Count > _def.avoidLastN)
                    _recent.Dequeue();
            }

            /// <summary>
            /// Gets the next event from the stack according to its configured mode and rules.
            /// </summary>
            /// <param name="activeFlags">The set of currently active flags in the game state.</param>
            /// <returns>An eligible SimEventSO, or null if no suitable event is found.</returns>
            public SimEventSO Next(IReadOnlyCollection<string> activeFlags)
            {
                if (_def == null) return null;
                LastFailureReason = null;

                switch (_def._mode)
                {
                    case Mode.FixedSequence:
                        return NextFromSequence();

                    case Mode.RandomWeighted:
                        return NextFromPool(activeFlags);

                    default:
                        return null;
                }
            }

            private SimEventSO NextFromSequence()
            {
                var seq = _def._sequence;
                if (seq == null || seq.Count == 0) return null;

                // Loop through the sequence to find the next valid, non-null event.
                int attempts = 0;
                while (attempts < seq.Count)
                {
                    if (_seqIndex >= seq.Count)
                    {
                        if (_def.loopSequence) _seqIndex = 0;
                        else return null; // End of sequence and not looping.
                    }

                    var evt = seq[_seqIndex];
                    _seqIndex++;

                    if (evt != null) return evt;
                    attempts++;
                }
                return null; // No valid events found in the entire sequence.
            }

            private SimEventSO NextFromPool(IReadOnlyCollection<string> activeFlags)
            {
                var pool = _def._pool;
                if (pool == null || pool.Count == 0)
                {
                    SetFail("Pool is empty.");
                    if (_def.verboseWarnings) Debug.LogWarning($"[StoryStackSO] '{_def.name}': {LastFailureReason}", _def);
                    return null;
                }

                // First pass: build a list of eligible events, respecting all rules.
                Span<int> indices = stackalloc int[pool.Count];
                int eligibleCount = 0;
                float totalWeight = 0f;

                for (int i = 0; i < pool.Count; i++)
                {
                    var e = pool[i];
                    if (e == null) continue;
                    if (_def.skipNullEntries && e.eventRef == null) continue;
                    if (e.weight <= 0f) continue;
                    if (_def.excludeEndingEventsInPool && e.eventRef != null && e.eventRef.Type == SimEventSO.EventType.Ending) continue;
                    if (!PassesFlags(activeFlags, e.requiredFlags, e.forbiddenFlags)) continue;

                    // Check recent history.
                    if (_def.avoidLastN > 0 && e.eventRef != null)
                    {
                        string id = SafeEventId(e.eventRef);
                        if (!string.IsNullOrEmpty(id) && _recent.Contains(id))
                            continue;
                    }

                    // If all checks pass, add it to the eligible list.
                    indices[eligibleCount++] = i;
                    totalWeight += e.weight;
                }

                // Second pass (optional): if nothing was found, try again while ignoring recent history.
                if (eligibleCount == 0 && _def.allowRepeatIfNoEligible)
                {
                    for (int i = 0; i < pool.Count; i++)
                    {
                        var e = pool[i];
                        if (e == null) continue;
                        if (_def.skipNullEntries && e.eventRef == null) continue;
                        if (e.weight <= 0f) continue;
                        if (_def.excludeEndingEventsInPool && e.eventRef != null && e.eventRef.Type == SimEventSO.EventType.Ending) continue;
                        if (!PassesFlags(activeFlags, e.requiredFlags, e.forbiddenFlags)) continue;

                        indices[eligibleCount++] = i;
                        totalWeight += e.weight;
                    }
                }

                if (eligibleCount == 0)
                {
                    SetFail(_def.allowRepeatIfNoEligible
                        ? "No eligible entries in pool (even after allowing repeats)."
                        : "No eligible entries in pool (due to filters and recent-history avoidance).");

                    if (_def.verboseWarnings) Debug.LogWarning($"[StoryStackSO] '{_def.name}': {LastFailureReason}", _def);
                    return null;
                }

                // Perform the weighted random pick.
                float r = UnityEngine.Random.value * Mathf.Max(0.0001f, totalWeight);
                for (int k = 0; k < eligibleCount; k++)
                {
                    var e = pool[indices[k]];
                    r -= e.weight;
                    if (r <= 0f)
                        return e.eventRef;
                }

                // Fallback in the rare case of floating point inaccuracies.
                return pool[indices[eligibleCount - 1]].eventRef;
            }

            /// <summary>
            /// Checks if an entry is valid based on the current game flags.
            /// </summary>
            private static bool PassesFlags(IReadOnlyCollection<string> flags, List<string> required, List<string> forbidden)
            {
                // If no flags provided by runtime, only valid if there are no requirements and no forbids.
                if (flags == null)
                    return (required == null || required.Count == 0) && (forbidden == null || forbidden.Count == 0);

                if (required != null)
                {
                    foreach (var reqFlag in required)
                    {
                        if (string.IsNullOrWhiteSpace(reqFlag)) continue;
                        // Case-insensitive membership test
                        if (!flags.Any(f => string.Equals(f, reqFlag, StringComparison.OrdinalIgnoreCase)))
                            return false;
                    }
                }

                if (forbidden != null)
                {
                    foreach (var fobFlag in forbidden)
                    {
                        if (string.IsNullOrWhiteSpace(fobFlag)) continue;
                        if (flags.Any(f => string.Equals(f, fobFlag, StringComparison.OrdinalIgnoreCase)))
                            return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Gets a safe, non-null ID for an event, preferring its narrative ID over its asset name.
            /// </summary>
            private static string SafeEventId(SimEventSO evt)
            {
                if (evt == null) return null;
                if (!string.IsNullOrWhiteSpace(evt.simEventId)) return evt.simEventId;
                return evt.name;
            }
        }
    }
}
