using System;
using System.Collections.Generic;

namespace SimulationGame
{
    /// <summary>
    /// Represents the save data for the simulation game.
    /// </summary>

    [Serializable]
    public class SaveData
    {
        public int Version = 1;

        public string SlotName;
        public string SaveName;

        public int CurrentDay;
        public List<string> Flags = new();
        public string PlayerActorId;


        public Dictionary<string, float> Resources = new();
        public Dictionary<string, float> Relationships = new();
        public Dictionary<string, float> NarrativeVariables = new();
        public Dictionary<string, bool> NarrativeBooleans = new();

        public List<string> ActiveStoryArcs = new();
        public string CurrentEventId;
        public List<string> CompletedEventIds = new();

    }
}
