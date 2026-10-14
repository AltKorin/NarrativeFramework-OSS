using System.Collections.Generic;

namespace SimulationGame
{
    /// <summary>
    /// Interface for providing save data to the simulation game.
    /// </summary>

    public interface ISaveDataProvider
    {
        int CurrentDay { get; }
        IEnumerable<string> Flags { get; }


        Dictionary<string, float> Resources { get; }
        Dictionary<string, float> Relationships { get; }

        Dictionary<string, float> NarrativeVariables { get; }
        Dictionary<string, bool> NarrativeBooleans { get; }

        IEnumerable<string> ActiveStoryArcs { get; }
        string CurrentEventId { get; }

        string SlotName { get; }
        string SaveName { get; }
    }
}