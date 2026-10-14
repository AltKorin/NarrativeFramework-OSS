using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    [CreateAssetMenu(fileName = "StoryArc", menuName = "Narrative Framework/Story Arc")]
    public class StoryArc : ScriptableObject
    {
        public string ArcId;
        public List<SimEventSO> Events;
        public List<string> RequiredFlags;
        public List<string> CompletionFlags;

        public bool IsActive(IEnumerable<string> currentFlags)
        {
            var flagSet = new HashSet<string>(currentFlags);
            foreach (var flag in RequiredFlags)
            {
                if (!flagSet.Contains(flag))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsCompleted(IEnumerable<string> currentFlags)
        {
            var flagSet = new HashSet<string>(currentFlags);
            foreach (var flag in CompletionFlags)
            {
                if (!flagSet.Contains(flag))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
