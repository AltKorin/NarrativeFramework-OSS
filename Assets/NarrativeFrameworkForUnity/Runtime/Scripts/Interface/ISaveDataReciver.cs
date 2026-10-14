using System.Collections.Generic;

namespace SimulationGame.Interfaces
{
    public interface ISaveDataReceiver
{
    void ApplySaveData(
        int currentDay,
        HashSet<string> flags,
        Dictionary<string, float> resources,
        Dictionary<string, float> relationships,
        Dictionary<string, float> narrativeVariables,
        Dictionary<string, bool> narrativeBooleans,
        List<string> activeStoryArcs,
        string currentEventId,
        string slotName,
        string saveName
    );
}

}
