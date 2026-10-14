#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using SimulationGame.Editor.GameGeneration;


namespace SimulationGame.Editor
{

    [CustomEditor(typeof(GameGenerationSettingsSO))]
    public sealed class GameGenerationSettingsEditor : UnityEditor.Editor
    {
        // Default path for "Create Defaults"
        private const string kDefaultDir  = "Assets/SimulationGame/Data";
        private const string kDefaultPath = kDefaultDir + "/GameGenerationSettings.asset";

        public override void OnInspectorGUI()
        {
            // Safe, consistent layout with your guards
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Game Generation Settings", EditorStyles.boldLabel);

                // Top actions
                NarrativeLinkerGUI.Horizontal(() =>
                {
                    if (GUILayout.Button("Open Wizard", GUILayout.Height(22)))
                    {
                        GameGenerationWizard.Open();
                    }

                    if (GUILayout.Button("Create Defaults", GUILayout.Height(22)))
                    {
                        EnsureDefaultsExist();
                    }

                    GUILayout.FlexibleSpace();
                });
            });

            var so = serializedObject;
            so.Update();

            // ----- THEME ----------------------------------------------------
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Game Theme", EditorStyles.boldLabel);

                // game theme could be string, SO or anything; try typical names
                var pTheme = FindAny(so, "GameTheme", "gameTheme", "_gameTheme", "theme", "_theme");
                if (pTheme != null)
                {
                    EditorGUILayout.PropertyField(pTheme, new GUIContent("Game Theme"));
                }
                else
                {
                    EditorGUILayout.HelpBox("Theme field not found (GameTheme).", MessageType.Info);
                }
            });

            // ----- COUNTS ---------------------------------------------------
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Content Generation Counts", EditorStyles.boldLabel);

                PropInt(so, "NumberOfMainEvents",       "numberOfMainEvents",       "_numberOfMainEvents",       "Number Of Main Events");
                PropInt(so, "NumberOfRandomEvents",     "numberOfRandomEvents",     "_numberOfRandomEvents",     "Number Of Random Events");
                PropInt(so, "NumberOfCriticalEvents",   "numberOfCriticalEvents",   "_numberOfCriticalEvents",   "Number Of Critical Events");
                PropInt(so, "NumberOfEndingEvents",     "numberOfEndingEvents",     "_numberOfEndingEvents",     "Number Of Ending Events");
                PropInt(so, "NumberOfStoryArcs",        "numberOfStoryArcs",        "_numberOfStoryArcs",        "Number Of Story Arcs");
                PropInt(so, "NumberOfMainCharacters",   "numberOfMainCharacters",   "_numberOfMainCharacters",   "Number Of Main Characters");
                PropInt(so, "NumberOfResources",        "numberOfResources",        "_numberOfResources",        "Number Of Resources");
            });

            // ----- OPENAI ---------------------------------------------------
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("OpenAI Settings", EditorStyles.boldLabel);

                var pModel = FindAny(so, "OpenAIModel", "openAIModel", "openAiModel", "_openAIModel");
                if (pModel != null)
                    EditorGUILayout.PropertyField(pModel, new GUIContent("OpenAI Model"));
                else
                    EditorGUILayout.HelpBox("Model field not found (OpenAIModel).", MessageType.None);

                var pTemp = FindAny(so, "OpenAITemperature", "openAITemperature", "temperature", "_openAITemperature");
                if (pTemp != null)
                    EditorGUILayout.Slider(pTemp, 0f, 1f, new GUIContent("OpenAI Temperature"));
            });

            // ----- ASSET NAMING ---------------------------------------------
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Asset Naming", EditorStyles.boldLabel);

                PropString(so, "EventAssetPrefix",     "eventAssetPrefix",     "_eventAssetPrefix",     "Event Asset Prefix");
                PropString(so, "StoryArcAssetPrefix",  "storyArcAssetPrefix",  "_storyArcAssetPrefix",  "Story Arc Asset Prefix");
                PropString(so, "ActorAssetPrefix",     "actorAssetPrefix",     "_actorAssetPrefix",     "Actor Asset Prefix");
                PropString(so, "ResourceAssetPrefix",  "resourceAssetPrefix",  "_resourceAssetPrefix",  "Resource Asset Prefix");
            });

            so.ApplyModifiedProperties();

            // Subtle footer
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "This inspector mirrors the Game Generation Wizard configuration.\n" +
                "Use \"Open Wizard\" to run generation or create a Sandbox Scene.", MessageType.None);
        }

        // --------------------- helpers --------------------------------------

        private static SerializedProperty FindAny(SerializedObject so, params string[] names)
        {
            if (so == null) return null;
            foreach (var n in names)
            {
                var p = so.FindProperty(n);
                if (p != null) return p;
            }
            return null;
        }

        private static void PropInt(SerializedObject so, string n1, string n2, string n3, string label)
        {
            var p = FindAny(so, n1, n2, n3);
            if (p != null) EditorGUILayout.IntSlider(p, 0, 100, new GUIContent(label));
        }

        private static void PropString(SerializedObject so, string n1, string n2, string n3, string label)
        {
            var p = FindAny(so, n1, n2, n3);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
        }

        private static void EnsureDefaultsExist()
        {
            if (!AssetDatabase.IsValidFolder(kDefaultDir))
            {
                var parent = Path.GetDirectoryName(kDefaultDir).Replace('\\', '/');
                var leaf   = Path.GetFileName(kDefaultDir);
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets", "SimulationGame");
                AssetDatabase.CreateFolder(parent, leaf);
            }

            var settings = AssetDatabase.LoadAssetAtPath<GameGenerationSettingsSO>(kDefaultPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameGenerationSettingsSO>();
                AssetDatabase.CreateAsset(settings, kDefaultPath);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Game Generation Settings",
                    "Default GameGenerationSettings asset created:\n" + kDefaultPath, "OK");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
#endif
