// GameController.Debug.cs
using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Non-invasive debug surface for the DeveloperConsole overlay.
    /// Exposes readonly state and a few safe setters that reuse your existing managers.
    /// </summary>
    public partial class GameController : MonoBehaviour
    {
        // --- Read-only state for display ---
        public string   Debug_CurrentEventName => _currentEvent ? _currentEvent.name : "(none)";
        public int      Debug_CurrentDay      => _simulation != null ? _simulation.CurrentTimeUnit : _currentDay;
        public IReadOnlyDictionary<string, float> Debug_Resources      => _resources;
        public IReadOnlyDictionary<string, float> Debug_Relationships  => _relationships;
        public IReadOnlyCollection<string>        Debug_Flags          => _flags;

        // --- Safe mutators (reuse existing apply/update flows) ---
        public void Debug_SetResource(string id, float value)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _resources[id] = value;
            _resourceManager?.Apply(_resources);
            _uiManager?.UpdateResources(_resources);
        }

        public void Debug_SetRelationship(string id, float value)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _relationships[id] = value;
        }

        public void Debug_AddFlag(string f)
        {
            if (string.IsNullOrWhiteSpace(f)) return;
            _flags.Add(f);
        }

        public void Debug_RemoveFlag(string f)
        {
            if (string.IsNullOrWhiteSpace(f)) return;
            _flags.Remove(f);
        }

        /// <summary>Jump to an event and present it using your normal flow.</summary>
        public void Debug_JumpToEvent(SimEventSO ev)
        {
            if (ev != null) PresentEvent(ev);
        }

        /// <summary>Convenience wrappers around your rewind / restart.</summary>
        public bool Debug_RewindOne() => RequestRewind();
        public void Debug_Restart()   => RestartRun();

        /// <summary>Advance day using your normal path (if exposed).</summary>
        public void Debug_NextDay()
        {
            // If you have a public AdvanceDay() use it. If it's private, add a slim wrapper here.
            AdvanceDay();
        }
    }
}
