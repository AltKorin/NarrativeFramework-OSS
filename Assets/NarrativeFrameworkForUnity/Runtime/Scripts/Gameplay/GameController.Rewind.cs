// Assets/SimulationGame/Runtime/Scripts/Gameplay/GameController.Rewind.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimulationGame
{
    public partial class GameController
    {
        // --- REWIND -------------------------------------------------------------
        #region Rewind

        [Header("Rewind")]
        [SerializeField] private int _rewindHistoryDepth = 3;   // How many recent events to re-notify into StoryStack after a rewind

        private readonly Stack<RewindSnapshot> _rewind = new(); // in-RAM stack, no file I/O
        private readonly List<string> _eventHistory = new();    // Short FIFO of visited events (evt.name)

        [Serializable]
        private struct RewindSnapshot
        {
            public string currentEventId;
            public int day;
            public Dictionary<string, float> resources;
            public HashSet<string> flags;
            public Dictionary<string, float> relationships;
            public Dictionary<string, float> variables; // float narrative vars
            public Dictionary<string, bool>  bools;     // bool narrative vars
            public List<string> eventHistory;
        }

        /// <summary>
        /// Capture a deep copy of the current run state BEFORE applying a choice.
        /// </summary>
        private void PushRewindSnapshot()
        {
            var snap = new RewindSnapshot
            {
                currentEventId = _currentEvent != null ? _currentEvent.name : "",
                day            = _simulation != null ? _simulation.CurrentTimeUnit : _currentDay,
                resources      = new Dictionary<string, float>(_resources ?? new()),
                flags          = new HashSet<string>(_flags ?? new()),
                relationships  = new Dictionary<string, float>(_relationships ?? new()),
                variables      = NarrativeVariables,   // uses existing safe copies
                bools          = NarrativeBooleans,    // uses existing safe copies
                eventHistory   = new List<string>(_eventHistory)
            };
            _rewind.Push(snap);
        }

        /// <summary>
        /// Restore the last snapshot and present the previous event again.
        /// </summary>
        public bool TryRewind()
        {
            if (_rewind.Count == 0) return false;
            var s = _rewind.Pop();

            // Restore state
            _resources      = new Dictionary<string, float>(s.resources ?? new());
            _resourceManager?.Apply(_resources);
            _flags         = new HashSet<string>(s.flags ?? new());
            _relationships = new Dictionary<string, float>(s.relationships ?? new());
            _narrativeVariableManager?.Apply(s.variables ?? new(), s.bools ?? new());

            _eventHistory.Clear();
            _eventHistory.AddRange(s.eventHistory ?? new List<string>());

            // Reset StoryStack runtime and rebuild its short-term memory
            _storyStack = _fallbackStoryStack != null ? new StoryStackSO.StoryStackRuntime(_fallbackStoryStack) : null;
            if (_storyStack != null && _eventHistory.Count > 0)
            {
                int start = Mathf.Max(0, _eventHistory.Count - _rewindHistoryDepth);
                for (int i = start; i < _eventHistory.Count; i++)
                {
                    var ev = GameData.Instance.Events.FirstOrDefault(e => e != null && e.name == _eventHistory[i]);
                    if (ev != null) _storyStack.NotifyOccurred(ev);
                }
            }

            // Reload the previous event
            var prev = GameData.Instance.Events.FirstOrDefault(e => e != null && e.name == s.currentEventId);
            if (prev != null) PresentEvent(prev);

            // Refresh UI
            _uiManager?.UpdateResources(_resources);
            _uiManager?.SetNextDayButtonVisible(false);
            return true;
        }

        /// <summary>
        /// Public entry for UI/debug. Leaves core logic in TryRewind().
        /// </summary>
        public bool RequestRewind()
        {
            // If game was ended, allow UI to come back to interactive state.
            _isGameRunning = true;
            _uiManager?.HideGameOver();

            bool ok = TryRewind();
            if (!ok) Debug.LogWarning("[GameController] Rewind failed (no snapshots).");
            return ok;
        }

        /// <summary>Utility to clear local rewind state (used on Start/Restart).</summary>
        private void ClearRewindHistory()
        {
            _rewind.Clear();
            _eventHistory.Clear();
        }

        #endregion
    }
}
