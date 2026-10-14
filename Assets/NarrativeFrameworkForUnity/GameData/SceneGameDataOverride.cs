using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Component to override the active GameData instance when a scene is loaded.
    /// </summary>


    [DefaultExecutionOrder(-10000)] // ensure this runs before anything reads GameData.Instance
    public sealed class SceneGameDataOverride : MonoBehaviour
    {
        [SerializeField] private GameData activeGameData;

        private void Awake()
        {
            if (activeGameData != null)
                GameDataRuntime.SetActive(activeGameData); // see helper below
        }
    }
}