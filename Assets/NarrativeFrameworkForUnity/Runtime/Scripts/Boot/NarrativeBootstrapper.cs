using UnityEngine;
using UnityEngine.EventSystems;

namespace SimulationGame.Boot
{
    /// <summary>
    /// Plug-and-play bootstrapper for the Narrative Framework.
    /// - Ensures a single EventSystem exists (optional).
    /// - Instantiates the SimulationGameRoot prefab if not present.
    /// - (Optional) Keeps the root persistent across scene loads.
    ///
    /// Designer flow:
    /// 1) Drop this component on an empty GameObject in any scene.
    /// 2) Assign your SimulationGameRoot prefab (the one that contains GameController).
    /// 3) (Optional) Assign a custom EventSystem prefab, or leave empty to use the built-in default.
    /// </summary>
    [DefaultExecutionOrder(-10000)] // Run before other scripts so GameController can bootstrap on time.
    public sealed class NarrativeBootstrapper : MonoBehaviour
    {
        [Header("Runtime Root (required)")]
        [Tooltip("Prefab that contains the GameController and the runtime stack (SimulationGameRoot).")]
        [SerializeField] private GameObject simulationGameRootPrefab;

        [Header("Event System (optional)")]
        [Tooltip("If null, a minimal EventSystem will be spawned when none is found.")]
        [SerializeField] private EventSystem eventSystemPrefab;

        [Header("Options")]
        [Tooltip("If true, the instantiated root will persist across scene loads (DontDestroyOnLoad). " +
                 "Make sure gameplay scenes do NOT contain another copy of the root prefab.")]
        [SerializeField] private bool makeRootPersistent = false;

        [Tooltip("If true, the bootstrapper will ensure exactly one EventSystem exists in the scene.")]
        [SerializeField] private bool enforceSingleEventSystem = true;

        // Prevent duplicate bootstraps in the same frame/scene.
        private static bool s_BootstrappedThisFrame;

        private void Awake()
        {
            // Guard: prevent multiple bootstraps in the same frame (e.g., duplicate components).
            if (s_BootstrappedThisFrame)
            {
                return;
            }
            s_BootstrappedThisFrame = true;

            if (enforceSingleEventSystem)
            {
                EnsureSingleEventSystem();
            }

            EnsureRuntimeRoot();
        }

        private void EnsureRuntimeRoot()
        {
            // If a GameController already exists, we assume the root is present and skip instantiation.
            var existingController = FindFirstObjectByType<GameController>();
            if (existingController != null)
            {
                return;
            }

            if (simulationGameRootPrefab == null)
            {
                Debug.LogError("[NarrativeBootstrapper] SimulationGameRoot Prefab is not assigned. " +
                               "Assign it in the inspector to enable plug-and-play startup.");
                return;
            }

            var root = Instantiate(simulationGameRootPrefab);
            root.name = simulationGameRootPrefab.name; // keep clean hierarchy name

            if (makeRootPersistent)
            {
                DontDestroyOnLoad(root);
            }

            Debug.Log("[NarrativeBootstrapper] Spawned SimulationGameRoot prefab.");
        }

        private void EnsureSingleEventSystem()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                // Spawn a minimal EventSystem if designer didn't place one.
                if (eventSystemPrefab != null)
                {
                    var spawned = Instantiate(eventSystemPrefab);
                    spawned.name = eventSystemPrefab.name;
                    Debug.Log("[NarrativeBootstrapper] Spawned EventSystem from prefab.");
                }
                else
                {
                    var go = new GameObject("EventSystem (Auto)");
                    go.AddComponent<EventSystem>();
                    go.AddComponent<StandaloneInputModule>();
                    Debug.Log("[NarrativeBootstrapper] Spawned minimal EventSystem.");
                }
                return;
            }

            if (all.Length > 1)
            {
                // Keep the oldest; destroy the newest duplicates to avoid spam and input conflicts.
                for (int i = 1; i < all.Length; i++)
                {
                    var es = all[i];
                    if (es != null)
                    {
                        Debug.LogWarning("[NarrativeBootstrapper] Duplicate EventSystem detected. Destroying extra instance.");
                        Destroy(es.gameObject);
                    }
                }
            }
        }
        
    }
}
