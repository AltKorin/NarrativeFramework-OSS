#if UNITY_EDITOR
using UnityEngine;

namespace SimulationGame.Editor.GameGeneration
{
    /// <summary>
    /// Editor-only ScriptableObject listing all generated assets from a single pass.
    /// Useful for "Generate Assets Only" mode and for review/wiring later.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Narrative Framework/Generated Data Set",
        fileName = "GeneratedDataSet")]
    public sealed class GeneratedDataSetSO : ScriptableObject
    {
        public SimEventSO[] events;
        public ActorSO[] actors;
        public Resource[] resources;
        public StoryArcSO[] storyArcs;
    }
}
#endif
