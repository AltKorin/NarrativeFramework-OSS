using System.Collections.Generic;
using UnityEngine.Events;

namespace SimulationGame.Interfaces
{
    public interface IEventSystem
    {
        UnityEvent<SimEventSO> OnMainEventGenerated { get; }
        UnityEvent<List<SimEventSO>> OnRandomEventsGenerated { get; }
        UnityEvent<SimEventSO> OnEventCompleted { get; }

        List<SimEventSO> CompletedEvents { get; }

        void PresentEvent(SimEventSO simEvent);
        void CompleteEvent(SimEventSO simEvent);
    }
}
