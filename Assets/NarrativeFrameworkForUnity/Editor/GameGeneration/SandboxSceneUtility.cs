#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SimulationGame.Editor.GameGeneration
{
    /// <summary>
    /// Creates a minimal sandbox scene for testing generated content.
    /// - Ensures exactly one EventSystem.
    /// - Adds NarrativeBootstrapper (if available) and sets it to non-persistent by default.
    /// - Does not create or duplicate GameData; it uses whatever is in Resources.
    /// </summary>
    public static class SandboxSceneUtility
    {
        public static void CreateSandboxScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Sandbox Scene",
                    "Exit Play Mode before creating a sandbox scene.", "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "NFU_Generated_Sandbox";

            // Ensure one EventSystem
            var existingES = Object.FindFirstObjectByType<EventSystem>();
            if (existingES == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }

            // Add NarrativeBootstrapper if available (optional; won't error if not present)
            var bootstrapperType = System.Type.GetType("SimulationGame.Boot.NarrativeBootstrapper, Assembly-CSharp");
            if (bootstrapperType != null)
            {
                var existing = Object.FindFirstObjectByType(bootstrapperType);
                if (existing == null)
                {
                    var go = new GameObject("NarrativeBootstrapper");
                    var comp = go.AddComponent(bootstrapperType);
                    // try set bool field "makeRootPersistent" to false if present
                    var so = new SerializedObject((Object)comp);
                    var prop = so.FindProperty("makeRootPersistent");
                    if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
                    {
                        prop.boolValue = false;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // Readme hint
            var readme = new GameObject("SandboxReadme");
            var text = readme.AddComponent<SandboxReadme>();
            text.message =
                "This sandbox scene is for testing generated content.\n" +
                "It uses your selected GameData (no duplicates are created).\n" +
                "Press Play to test. You can delete this scene at any time.";

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        [AddComponentMenu("")]
        private sealed class SandboxReadme : MonoBehaviour
        {
            [TextArea(3, 8)] public string message;
        }
    }
}
#endif
