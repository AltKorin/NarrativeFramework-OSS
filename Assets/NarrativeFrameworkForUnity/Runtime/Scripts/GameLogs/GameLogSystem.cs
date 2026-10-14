using System.Collections.Generic;
using SimulationGame;
using UnityEngine;

namespace SimulationGame.Runtime
{
    /// <summary>
    /// Manages the game log, storing all log entries.
    /// </summary>
    public class GameLogSystem : MonoBehaviour
    {
        private List<GameLogEntry> _entries = new();

        public IReadOnlyList<GameLogEntry> Entries => _entries;

        public void LogDecision(int day, SimEventSO simEvent, Choice choice, List<Outcome> appliedOutcomes)
        {
            _entries.Add(new GameLogEntry
            {
                Day = day,
                EventId = simEvent.Id,
                EventTitle = simEvent.Title,
                ChoiceId = choice.Id,
                ChoiceText = choice.ChoiceText,
                OutcomesDescriptions = appliedOutcomes.ConvertAll(o => o.Description)
            });
        }

        public void Clear() => _entries.Clear();
    }
}