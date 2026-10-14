using UnityEngine;
using System.Collections.Generic;
using SimulationGame.Interfaces;
using UnityEngine.Events;
using System;
using SimulationGame.DebugTools;

namespace SimulationGame
{
    public class ChoiceEngine : MonoBehaviour, IChoiceEngine
    {
        [SerializeField] private int maxChoicesPerEvent = 5;

        // Events to notify listeners
        public UnityEvent<List<Choice>> OnChoicesGenerated { get; private set; } = new UnityEvent<List<Choice>>();
        public UnityEvent<Choice> OnChoiceSelected { get; private set; } = new UnityEvent<Choice>();
        public UnityEvent<List<Outcome>> OnOutcomesGenerated { get; private set; } = new UnityEvent<List<Outcome>>();


        UnityEvent<List<Choice>> IChoiceEngine.OnChoicesGenerated => OnChoicesGenerated;

        UnityEvent<Choice> IChoiceEngine.OnChoiceSelected => OnChoiceSelected;

        UnityEvent<List<Outcome>> IChoiceEngine.OnOutcomesGenerated => OnOutcomesGenerated;

        // Present a specific event and filter available choices based on flags and conditions
        public void PresentEvent(SimEventSO simEvent, int currentDay, IEnumerable<string> currentFlags)
        {
            if (simEvent != null)
            {
                // Ensure we only pass the required flags
                PresentChoices(simEvent.Choices, currentDay, currentFlags);
            }
            else
            {
                Log.Error("SimEventSO is null!");
            }
        }

        // Generate and filter the choices based on flags and the max allowed per event
        public void PresentChoices(IList<Choice> availableChoices, int currentDay, IEnumerable<string> currentFlags)
        {
            List<Choice> filteredChoices = new List<Choice>();

            foreach (Choice choice in availableChoices)
            {
                if (choice.IsAvailable(currentDay, currentFlags) && filteredChoices.Count < maxChoicesPerEvent)
                {
                    filteredChoices.Add(choice);
                }
            }

            // Trigger the event
            OnChoicesGenerated?.Invoke(filteredChoices);
            Log.Info($"Presented {filteredChoices.Count} choices to player");
        }

        // Process the selected choice and generate associated outcomes
        public SimEventSO ProcessChoice(Choice selectedChoice, ISimulationManager simulationManager)
        {
            if (selectedChoice == null)
            {
                Log.Error("Attempted to process a null choice");
                return null;
            }

            OnChoiceSelected?.Invoke(selectedChoice);

            List<Outcome> actualOutcomes = new List<Outcome>();
            foreach (Outcome outcome in selectedChoice.Outcomes)
            {
                if (outcome.ShouldOccur())
                {
                    actualOutcomes.Add(outcome);
                }
            }

            OnOutcomesGenerated?.Invoke(actualOutcomes);

            return selectedChoice.NextEvent;
        }
    }
}
