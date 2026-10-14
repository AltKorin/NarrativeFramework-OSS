using UnityEngine;
using SimulationGame;
using System.Reflection;

namespace SimulationGame.Integration
{
    /// <summary>
    /// Example integration bridge. Attach to a scene object and call public methods from UI or triggers.
    /// </summary>
    public sealed class NarrativeFrameWorkIntegration : MonoBehaviour
    {
        [Header("References (optional, auto-resolved if empty)")]
        [SerializeField] private GameController _gameController;
        [SerializeField] private EventSystem _eventSystem;

        [Header("Settings")]
        [Tooltip("Optional: Event to run on Start() when scene loads.")]
        [SerializeField] private SimEventSO _startupEvent;

        [Tooltip("Optional: Run once when scene starts.")]
        [SerializeField] private bool _runStartupEvent = false;

        private void Awake()
        {
            if (_gameController == null)
            {
                _gameController = UnityEngine.Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
                if (_gameController == null)
                    Debug.LogWarning("[NarrativeFrameworkIntegration] GameController not found in scene.");
            }

            if (_eventSystem == null)
            {
                _eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (_eventSystem == null)
                    Debug.LogWarning("[NarrativeFrameworkIntegration] EventSystem not found in scene.");
            }
        }

        private void Start()
        {
            if (_runStartupEvent && _startupEvent != null && _eventSystem != null)
            {
                Debug.Log($"[NarrativeFrameworkIntegration] Running startup event: {_startupEvent.simEventId}");
                _eventSystem.TriggerEventById(_startupEvent.simEventId);
            }
        }

        public void TriggerEvent(SimEventSO evt)
        {
            if (evt == null || _eventSystem == null)
            {
                Debug.LogWarning("[NarrativeFrameworkIntegration] Cannot trigger event. Missing reference.");
                return;
            }
            _eventSystem.TriggerEventById(evt.simEventId);
        }

        public void TriggerEventById(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || _eventSystem == null)
            {
                Debug.LogWarning("[NarrativeFrameworkIntegration] Cannot trigger event. Missing ID or EventSystem.");
                return;
            }
            _eventSystem.TriggerEventById(eventId);
        }

        public void AdvanceDay()
        {
            if (_gameController != null)
                _gameController.AdvanceDay();
        }

        /// <summary>
        /// Tries to reset/restart the run. Supports both ResetCampaign() and RestartRun() if present.
        /// Uses reflection to avoid compile-time dependency on optional methods.
        /// </summary>
        public void ResetCampaign()
        {
            if (_gameController == null) return;

            var t = _gameController.GetType();
            var mReset = t.GetMethod("ResetCampaign", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var mRestart = t.GetMethod("RestartRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (mReset != null)
            {
                mReset.Invoke(_gameController, null);
                Debug.Log("[NarrativeFrameworkIntegration] Called GameController.ResetCampaign() via reflection.");
                return;
            }

            if (mRestart != null)
            {
                mRestart.Invoke(_gameController, null);
                Debug.Log("[NarrativeFrameworkIntegration] Called GameController.RestartRun() via reflection.");
                return;
            }

            Debug.LogWarning("[NarrativeFrameworkIntegration] Neither ResetCampaign() nor RestartRun() found on GameController.");
        }
    }
}
