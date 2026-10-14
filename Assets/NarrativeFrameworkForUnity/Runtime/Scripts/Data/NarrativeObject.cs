using UnityEngine;

namespace SimulationGame
{
    // Base class for all narrative objects in the game (SimEvent, NPC, Location, etc.)
    public abstract class NarrativeObject : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
    }
}
