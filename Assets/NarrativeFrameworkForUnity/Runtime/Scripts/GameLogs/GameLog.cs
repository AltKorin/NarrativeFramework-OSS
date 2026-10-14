using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Represents a single entry in the game log.
    /// </summary>
    [System.Serializable]
    public class GameLog
    {
        public List<GameLogEntry> Entries = new List<GameLogEntry>();

        public void AddEntry(int day, string eventId, string eventTitle, string choiceId, string choiceText, List<string> outcomesDescriptions)
        {
            Entries.Add(new GameLogEntry
            {
                Day = day,
                EventId = eventId,
                EventTitle = eventTitle,
                ChoiceId = choiceId,
                ChoiceText = choiceText,
                OutcomesDescriptions = outcomesDescriptions
            });
        }
    }

    [System.Serializable]
    public class GameLogEntry
    {
        public int Day;
        public string EventId;
        public string EventTitle;
        public string ChoiceId;
        public string ChoiceText;
        public List<string> OutcomesDescriptions;
    }
}
