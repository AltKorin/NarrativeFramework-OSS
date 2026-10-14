using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame.Runtime
{
    /// <summary>
    /// Collects telemetry data for the simulation game, tracking events, choices, and flags.
    /// </summary>

    public class TelemetryCollector : MonoBehaviour
    {
        private Dictionary<string, int> eventFrequency = new();
        private Dictionary<string, int> choiceFrequency = new();
        private Dictionary<string, int> flagStats = new();

        public void LogEvent(string eventId)
        {
            if (!eventFrequency.ContainsKey(eventId)) eventFrequency[eventId] = 0;
            eventFrequency[eventId]++;
        }

        public void LogChoice(string choiceId)
        {
            if (!choiceFrequency.ContainsKey(choiceId)) choiceFrequency[choiceId] = 0;
            choiceFrequency[choiceId]++;
        }

        public void LogFlag(string flagId)
        {
            if (!flagStats.ContainsKey(flagId)) flagStats[flagId] = 0;
            flagStats[flagId]++;
        }

        public void PrintSummary()
        {
            Debug.Log("Telemetry Summary:");
            foreach (var e in eventFrequency)
                Debug.Log($"Event: {e.Key}, Count: {e.Value}");

            foreach (var c in choiceFrequency)
                Debug.Log($"Choice: {c.Key}, Count: {c.Value}");

            foreach (var f in flagStats)
                Debug.Log($"Flag used: {f.Key}, Count: {f.Value}");
        }

        public void Clear()
        {
            eventFrequency.Clear();
            choiceFrequency.Clear();
            flagStats.Clear();
        }
    }
}