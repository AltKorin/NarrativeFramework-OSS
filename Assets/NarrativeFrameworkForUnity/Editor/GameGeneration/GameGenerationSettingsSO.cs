using UnityEngine;

namespace SimulationGame.Editor.GameGeneration
{
    [CreateAssetMenu(fileName = "GameGenerationSettings", menuName = "Narrative Framework/Game Generation Settings")]
    public class GameGenerationSettingsSO : ScriptableObject
    {
        [Header("Game Theme")]
        [Tooltip("Overall theme or genre of the game (e.g., 'post-apocalyptic survival', 'fantasy adventure').")]
        public string gameTheme;

        [Header("Content Generation Counts")]
        [Tooltip("Number of main events to generate.")]
        [Range(1, 50)]
        public int numberOfMainEvents = 10;

        [Tooltip("Number of random events to generate.")]
        [Range(0, 100)]
        public int numberOfRandomEvents = 20;

        [Tooltip("Number of critical events to generate.")]
        [Range(0, 10)]
        public int numberOfCriticalEvents = 3;

        [Tooltip("Number of ending events to generate.")]
        [Range(0, 5)]
        public int numberOfEndingEvents = 1;

        [Tooltip("Number of story arcs to generate.")]
        [Range(1, 20)]
        public int numberOfStoryArcs = 5;

        [Tooltip("Number of main characters to generate.")]
        [Range(1, 10)]
        public int numberOfMainCharacters = 3;

        [Tooltip("Number of resources to generate.")]
        [Range(1, 10)]
        public int numberOfResources = 5;

        [Header("OpenAI Settings")]
        [Tooltip("The OpenAI model to use for content generation (e.g., 'gpt-3.5-turbo', 'gpt-4').")]
        public string openAIModel = "gpt-3.5-turbo";

        [Tooltip("The temperature for OpenAI text generation. Higher values mean more random output.")]
        [Range(0.1f, 1.0f)]
        public float openAITemperature = 0.7f;

        [Header("Asset Naming")]
        [Tooltip("Prefix for generated event assets (e.g., 'GEN_Event_').")]
        public string eventAssetPrefix = "GEN_Event_";

        [Tooltip("Prefix for generated story arc assets (e.g., 'GEN_StoryArc_').")]
        public string storyArcAssetPrefix = "GEN_StoryArc_";

        [Tooltip("Prefix for generated actor assets (e.g., 'GEN_Actor_').")]
        public string actorAssetPrefix = "GEN_Actor_";

        [Tooltip("Prefix for generated resource assets (e.g., 'GEN_Resource_').")]
        public string resourceAssetPrefix = "GEN_Resource_";
    }
}
