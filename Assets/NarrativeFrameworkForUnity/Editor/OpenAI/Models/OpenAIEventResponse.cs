namespace SimulationGame.Editor
{
    using UnityEngine;
    using UnityEditor;

    /// <summary>
    /// Represents a response from an OpenAI event, containing a title and description.
    /// </summary>

    [System.Serializable]
    public class OpenAIEventResponse
    {
        public string title;
        public string description;
    }
}
