using System.Collections.Generic;
using SimulationGame.Interfaces;
using UnityEngine;
using UnityEngine.Events;
using SimulationGame.DebugTools;

namespace SimulationGame
{
    public class EventSystem : MonoBehaviour, IEventSystem
    {
        [Header("Game Data")]
        [SerializeField] private GameData gameData;

        // Backing fields for UnityEvents
        [SerializeField] private UnityEvent<SimEventSO> _onMainEventGenerated = new();
        [SerializeField] private UnityEvent<List<SimEventSO>> _onRandomEventsGenerated = new();
        [SerializeField] private UnityEvent<SimEventSO> _onEventCompleted = new();

        // Public accessors required by the interface
        public UnityEvent<SimEventSO> OnMainEventGenerated => _onMainEventGenerated;
        public UnityEvent<List<SimEventSO>> OnRandomEventsGenerated => _onRandomEventsGenerated;
        public UnityEvent<SimEventSO> OnEventCompleted => _onEventCompleted;

        // Internal state
        private HashSet<string> _currentFlags = new();
        private int _currentDay;

        private List<SimEventSO> _completedEvents = new();
        public List<SimEventSO> CompletedEvents => _completedEvents;

        public void Initialize(int day, IEnumerable<string> flags)
        {
            _currentDay = day;
            _currentFlags = new HashSet<string>(flags);
        }

        public void TriggerEventById(string id)
        {
            foreach (var evt in gameData.Events)
            {
                if (evt.Id == id)
                {
                    OnMainEventGenerated?.Invoke(evt);
                    return;
                }
            }

            Log.Warning($"Event with ID '{id}' not found.");
        }

        public void GenerateMainEvent()
        {
            foreach (var evt in gameData.Events)
            {
                if (evt.Type != SimEventSO.EventType.Main) continue;
                if (!evt.IsAvailable(_currentDay, _currentFlags)) continue;
                if (!evt.ShouldOccur()) continue;

                OnMainEventGenerated?.Invoke(evt);
                return;
            }

            Log.Info("No main event occurred.");
        }

        public void GenerateRandomEvents(int count)
        {
            var validEvents = new List<SimEventSO>();

            foreach (var evt in gameData.Events)
            {
                if (evt.Type != SimEventSO.EventType.Random) continue;
                if (!evt.IsAvailable(_currentDay, _currentFlags)) continue;
                if (evt.ShouldOccur())
                {
                    validEvents.Add(evt);
                }

                if (validEvents.Count >= count)
                    break;
            }

            OnRandomEventsGenerated?.Invoke(validEvents);
        }

        public void PresentEvent(SimEventSO evt)
        {
            Log.Info($"Presenting event: {evt.Title}");
        }

        public void CompleteEvent(SimEventSO evt)
        {
            if (!_completedEvents.Contains(evt))
                _completedEvents.Add(evt);

            OnEventCompleted.Invoke(evt);
        }
    }
}
