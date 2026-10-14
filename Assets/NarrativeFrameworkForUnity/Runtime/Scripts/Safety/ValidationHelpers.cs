using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame.Safety
{
    public static class ValidationHelpers
    {
        public static int SafeProcessOutcomes(IList<Outcome> outcomes, System.Action<Outcome> processor, string context = "Unknown")
        {
            if (outcomes == null)
            {
                Log.Warning($"[{context}] ProcessOutcomes called with null outcomes list");
                return 0;
            }

            if (processor == null)
            {
                Log.Error($"[{context}] ProcessOutcomes called with null processor function");
                return 0;
            }

            int processedCount = 0;

            for (int i = 0; i < outcomes.Count; i++)
            {
                var outcome = outcomes[i];

                if (outcome == null)
                {
                    Log.Warning($"[{context}] Null outcome found at index {i} in outcomes list");
                    continue;
                }

                if (string.IsNullOrEmpty(outcome.TargetId))
                {
                    Log.Warning($"[{context}] Outcome {outcome.Type} at index {i} has null/empty TargetId");
                    continue;
                }

                try
                {
                    processor(outcome);
                    processedCount++;
                }
                catch (System.Exception e)
                {
                    Log.Error($"[{context}] Failed to process outcome {outcome.Type} (index {i}): {e.Message}");
                }
            }

            return processedCount;
        }

        public static SimEventSO SafeGetEventById(string eventId, GameData gameData = null, string context = "Unknown")
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Log.Warning($"[{context}] SafeGetEventById called with null/empty eventId");
                return null;
            }

            gameData = gameData ?? GameData.Instance;
            if (gameData == null)
            {
                Log.Error($"[{context}] SafeGetEventById: GameData instance is null");
                return null;
            }

            if (gameData.Events == null)
            {
                Log.Error($"[{context}] SafeGetEventById: GameData.Events list is null");
                return null;
            }

            try
            {
                var foundEvent = gameData.Events.FirstOrDefault(e => e != null && e.name == eventId);

                if (foundEvent == null)
                {
                    Log.Warning($"[{context}] Event with ID '{eventId}' not found in GameData");
                }

                return foundEvent;
            }
            catch (System.Exception e)
            {
                Log.Error($"[{context}] Error searching for event '{eventId}': {e.Message}");
                return null;
            }
        }

        public static List<Choice> SafeValidateChoices(IList<Choice> choices, int currentDay, IEnumerable<string> flags, string context = "Unknown")
        {
            var validChoices = new List<Choice>();

            if (choices == null)
            {
                Log.Warning($"[{context}] SafeValidateChoices called with null choices list");
                return validChoices;
            }

            var flagSet = flags != null ? new HashSet<string>(flags) : new HashSet<string>();

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];

                if (choice == null)
                {
                    Log.Warning($"[{context}] Null choice found at index {i}");
                    continue;
                }

                if (string.IsNullOrEmpty(choice.ChoiceText))
                {
                    Log.Warning($"[{context}] Choice at index {i} has null/empty text");
                    continue;
                }

                try
                {
                    if (choice.IsAvailable(currentDay, flagSet))
                    {
                        validChoices.Add(choice);
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error($"[{context}] Error checking availability for choice at index {i}: {e.Message}");
                }
            }

            return validChoices;
        }

        public static bool SafeCanTriggerStoryArc(StoryArcSO storyArc, int currentDay, HashSet<string> flags, HashSet<SimEventSO> completedEvents, string context = "Unknown")
        {
            if (storyArc == null)
            {
                Log.Warning($"[{context}] SafeCanTriggerStoryArc called with null storyArc");
                return false;
            }

            if (storyArc.TriggeredEvent == null)
            {
                Log.Warning($"[{context}] StoryArc '{storyArc.name}' has null TriggerEvent");
                return false;
            }

            try
            {
                var safeFlags = flags ?? new HashSet<string>();
                var safeCompletedEvents = completedEvents ?? new HashSet<SimEventSO>();

                return storyArc.IsAvailable(currentDay, safeFlags, safeCompletedEvents);
            }
            catch (System.Exception e)
            {
                Log.Error($"[{context}] Error checking StoryArc '{storyArc.name}' trigger condition: {e.Message}");
                return false;
            }
        }

        public static bool SafeModifyResource(Dictionary<string, float> resources, string resourceId, float value, string context = "Unknown")
        {
            if (resources == null)
            {
                Log.Error($"[{context}] SafeModifyResource called with null resources dictionary");
                return false;
            }

            if (string.IsNullOrEmpty(resourceId))
            {
                Log.Warning($"[{context}] SafeModifyResource called with null/empty resourceId");
                return false;
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Log.Error($"[{context}] SafeModifyResource called with invalid value: {value} for resource '{resourceId}'");
                return false;
            }

            try
            {
                if (resources.ContainsKey(resourceId))
                {
                    float newValue = resources[resourceId] + value;

                    if (float.IsNaN(newValue) || float.IsInfinity(newValue))
                    {
                        Log.Error($"[{context}] Resource '{resourceId}' modification would result in invalid value: {newValue}");
                        return false;
                    }

                    resources[resourceId] = newValue;
                    Log.Info($"[{context}] Modified resource '{resourceId}' by {value} (new value: {newValue})");
                }
                else
                {
                    resources[resourceId] = value;
                    Log.Info($"[{context}] Created new resource '{resourceId}' with value {value}");
                }

                return true;
            }
            catch (System.Exception e)
            {
                Log.Error($"[{context}] Error modifying resource '{resourceId}': {e.Message}");
                return false;
            }
        }

        public static bool SafeModifyFlag(HashSet<string> flags, string flagId, bool add, string context = "Unknown")
        {
            if (flags == null)
            {
                Log.Error($"[{context}] SafeModifyFlag called with null flags set");
                return false;
            }

            if (string.IsNullOrEmpty(flagId))
            {
                Log.Warning($"[{context}] SafeModifyFlag called with null/empty flagId");
                return false;
            }

            try
            {
                if (add)
                {
                    bool wasAdded = flags.Add(flagId);
                    Log.Info($"[{context}] {(wasAdded ? "Added" : "Already present")} flag: {flagId}");
                }
                else
                {
                    bool wasRemoved = flags.Remove(flagId);
                    Log.Info($"[{context}] {(wasRemoved ? "Removed" : "Not present to remove")} flag: {flagId}");
                }

                return true;
            }
            catch (System.Exception e)
            {
                Log.Error($"[{context}] Error modifying flag '{flagId}': {e.Message}");
                return false;
            }
        }
    }
}
