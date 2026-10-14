using System.Collections.Generic;
using UnityEngine.Events;

namespace SimulationGame.Interfaces
{
    public interface IChoiceEngine
    {
        UnityEvent<List<Choice>> OnChoicesGenerated { get; }
        UnityEvent<Choice> OnChoiceSelected { get; }
        UnityEvent<List<Outcome>> OnOutcomesGenerated { get; }

        void PresentEvent(SimEventSO simEvent, int currentDay, IEnumerable<string> currentFlags);
        void PresentChoices(IList<Choice> availableChoices, int currentDay, IEnumerable<string> currentFlags);
        SimEventSO ProcessChoice(Choice selectedChoice, ISimulationManager simulationManager);
    }
}
