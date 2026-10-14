using System;
using System.Collections.Generic;
using System.Linq;
using SimulationGame.UI;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// A lightweight orchestrator that observes the main phases of the game.
    /// It logs key events and interactions without modifying the core game flow,
    /// making it useful for debugging and understanding the sequence of events.
    /// </summary>
    public class GameConductor : MonoBehaviour
    {
        [Header("Core Systems (Assign in inspector or will be found automatically)")]
        [SerializeField] private GameController _gameController;
        [SerializeField] private SimulationManager _simulationManager;
        [SerializeField] private EventSystem _eventSystem;
        [SerializeField] private ChoiceEngine _choiceEngine;
        [SerializeField] private UIManager _uiManager;

        /// <summary>
        /// Represents a single logged phase in the game's lifecycle.
        /// </summary>
        public struct Phase
        {
            public DateTime Time;
            public string Description;
        }

        private readonly List<Phase> _phases = new();

        /// <summary>
        /// A read-only list of all phases logged during the current run.
        /// </summary>
        public IReadOnlyList<Phase> Phases => _phases;

        private void Awake()
        {
            // Fallback resolution: find core systems if they haven't been assigned in the Inspector.
            if (_gameController == null && GameController.Instance != null)
                _gameController = GameController.Instance;
            if (_simulationManager == null)
                _simulationManager = FindFirstObjectByType<SimulationManager>();
            if (_eventSystem == null)
                _eventSystem = FindFirstObjectByType<EventSystem>();
            if (_choiceEngine == null)
                _choiceEngine = FindFirstObjectByType<ChoiceEngine>();
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<UIManager>();

            // Subscribe to events from other systems to log them.
            if (_uiManager != null)
            {
                _uiManager.OnChoiceSelected += OnChoiceSelected;
                _uiManager.OnNextDayClicked += AdvanceDay;
            }

            if (_choiceEngine != null)
            {
                _choiceEngine.OnChoicesGenerated.AddListener(HandleChoicesGenerated);
                _choiceEngine.OnOutcomesGenerated.AddListener(HandleOutcomesGenerated);
            }

            if (_eventSystem != null)
            {
                _eventSystem.OnMainEventGenerated.AddListener(HandleMainEventGenerated);
                _eventSystem.OnRandomEventsGenerated.AddListener(HandleRandomEventsGenerated);
                _eventSystem.OnEventCompleted.AddListener(HandleEventCompleted);
            }
        }

        /// <summary>
        /// The main entry point to start the game, which it does by calling the GameController.
        /// </summary>
        public void StartGame()
        {
            LogPhase("=== START GAME ===");
            // Use the existing logic from GameController to start the game.
            _gameController.StartGame();
        }

        // --- Event Handlers for Logging --- //

        private void OnChoiceSelected(Choice choice)
        {
            LogPhase($"Choice selected: {choice.ChoiceText} (ID: {choice.ChoiceId})");
            _choiceEngine.ProcessChoice(choice, _simulationManager);
        }

        private void HandleChoicesGenerated(IList<Choice> choices)
        {
            LogPhase($"Choices generated: {string.Join(", ", choices.Select(c => c.ChoiceText))}");
        }

        private void HandleOutcomesGenerated(IList<Outcome> outcomes)
        {
            LogPhase($"Outcomes generated: {string.Join(", ", outcomes.Select(o => o.Type.ToString()))}");
        }

        private void HandleMainEventGenerated(SimEventSO evt)
        {
            LogPhase($"Main event presented: {evt.Title}");
        }

        private void HandleRandomEventsGenerated(List<SimEventSO> events)
        {
            LogPhase($"Random events queued: {string.Join(", ", events.Select(e => e.Title))}");
        }

        private void HandleEventCompleted(SimEventSO evt)
        {
            LogPhase($"Event completed: {evt.Title}");
        }

        private void AdvanceDay()
        {
            LogPhase("Advancing day");
            _uiManager.SetNextDayButtonVisible(false);
            _simulationManager.AdvanceToNextTimeUnit();
            _simulationManager.StartTimeUnit();
            LogPhase($"New day: {_simulationManager.CurrentTimeUnit}");
        }

        /// <summary>
        /// Logs a new phase with a description and timestamp.
        /// </summary>
        /// <param name="description">The description of the phase to log.</param>
        public void LogPhase(string description)
        {
            var phase = new Phase { Time = DateTime.UtcNow, Description = description };
            _phases.Add(phase);
            Debug.Log($"[GameConductor] {description}");
        }

        /// <summary>
        /// Dumps all logged phases into a single formatted string.
        /// This can be called from a debug UI to get a report of the current run.
        /// </summary>
        /// <returns>A formatted string of all logged phases.</returns>
        public string DumpPhases()
        {
            return string.Join("\n", _phases.Select(p => $"[{p.Time:HH:mm:ss}] {p.Description}"));
        }
    }
}
