using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif

namespace SimulationGame.OpenAI
{
    /// <summary>Singleton asset that stores your OpenAI credentials & options.</summary>
    [CreateAssetMenu(
        menuName = "Tools/NarrativeFramework/OpenAI Settings",
        fileName = "OpenAISettings")]
    public class OpenAISettings : ScriptableObject
    {
    #region Inspector fields ────────────────────────────────────────────────────
        [Header("Core")]
        [TextArea(1, 5)] public string apiKey;
        public string model       = "gpt-3.5-turbo";
        [Range(0,2)]   public float  temperature = 0.7f;
        

        [Header("JSON mode")]
        public bool  forceJsonOutput = true;   // sets response_format.type = json_object
        [TextArea(2,4)]
        public string systemPrompt =
            "You are a JSON API. Reply ONLY with valid, minified JSON.";

        [Header("Assistants API (optional)")]
        public bool   useAssistantApi = false;
        public string assistantId;             // obtained in OpenAI web UI
        public string threadId;                // leave blank to create
    #endregion


    #region Helper: Load-or-Create singleton ────────────────────────────────────
        /// <summary>
        /// Finds the first <c>OpenAISettings</c> in project or creates one under
        /// <c>Assets/NarrativeFrameworkForUnity/Resources/OpenAISettings.asset</c>.
        /// Always works in Editor & at runtime (Resources.Load).
        /// </summary>
        public static OpenAISettings LoadOrCreate()
        {
            /* 1. Fast path – already in a Resources folder */
            var settings = Resources.Load<OpenAISettings>("OpenAISettings");
#if UNITY_EDITOR
            /* 2. Not in Resources? Search anywhere inside the project    */
            if (settings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:OpenAISettings");
                if (guids.Length > 0)
                    settings = AssetDatabase.LoadAssetAtPath<OpenAISettings>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            /* 3. Still null? Create a fresh asset inside Resources       */
            if (settings == null)
            {
                const string dir = "Assets/NarrativeFrameworkForUnity/Resources";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets/NarrativeFrameworkForUnity", "Resources");

                settings = CreateInstance<OpenAISettings>();
                AssetDatabase.CreateAsset(settings, $"{dir}/OpenAISettings.asset");
                AssetDatabase.SaveAssets();

                Debug.Log("<b>[OpenAI]</b> Created new OpenAISettings asset at "
                          + $"{dir}/OpenAISettings.asset");
            }
#endif
            return settings;
        }
    #endregion


#if UNITY_EDITOR
    #region Editor-only: keep a single copy & inside Resources ------------------
        [DidReloadScripts]
        private static void ValidateSingletonInstance()
        {
            string[] guids = AssetDatabase.FindAssets("t:OpenAISettings");

            /* none → silently create one so the first run is painless */
            if (guids.Length == 0)
            {
                LoadOrCreate();
                return;
            }

            /* duplicates → warn user */
            if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"<b>[OpenAI]</b> Multiple OpenAISettings assets found " +
                    $"({guids.Length}). The first one will be used – consider " +
                    $"keeping only one to avoid confusion.");
            }

            /* ensure primary asset lives in a Resources folder */
            string firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (!firstPath.Contains("/Resources/"))
            {
                const string dir = "Assets/NarrativeFrameworkForUnity/Resources";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets/NarrativeFrameworkForUnity", "Resources");

                string newPath = $"{dir}/OpenAISettings.asset";
                AssetDatabase.MoveAsset(firstPath, newPath);
                Debug.Log("<b>[OpenAI]</b> Moved OpenAISettings to " + newPath +
                          " so it can be loaded at runtime.");
            }
        }
    #endregion
#endif
    }
}
