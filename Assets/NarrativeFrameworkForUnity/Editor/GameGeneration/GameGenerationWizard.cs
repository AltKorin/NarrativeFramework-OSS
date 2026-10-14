#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
// UI helpers (your guard-wrapped IMGUI)
using SimulationGame.Editor;                       // NarrativeLinkerGUI
// Wizard helpers (GeneratedDataSetSO, SandboxSceneUtility)
using SimulationGame.Editor.GameGeneration;
// Runtime data types
using SimulationGame;

namespace SimulationGame.Editor.GameGeneration
{
    /// <summary>
    /// Game generation window aligned with Narrative Framework standards and using NarrativeLinkerGUI guards.
    ///
    /// Key points:
    /// - Explicit Target GameData (no second GameData is ever created).
    /// - Two modes: Generate + Wire OR Generate Assets Only.
    /// - Optional Sandbox Scene creation.
    /// - AssetDatabase hygiene (batched saves).
    /// - Designer-first UX with progress and summary.
    ///
    /// Plug-in point:
    ///   ExternalGenerateFunc?.Invoke(settings, prompts) -> GeneratedDataSetSO
    /// If null, placeholder assets are created so designers can still test wiring flow.
    /// </summary>
    public sealed class GameGenerationWizard : EditorWindow
    {
        public static Func<GameGenerationSettingsSO, PromptSettingsSO, GeneratedDataSetSO> ExternalGenerateFunc;

        [SerializeField] private GameGenerationSettingsSO _settings;
        [SerializeField] private PromptSettingsSO _prompts;
        [SerializeField] private GameData _targetGameData;
        [SerializeField] private bool _generateAssetsOnly = false;

        private const string kRoot = "Assets/NarrativeFrameworkForUnity";
        private const string kData = kRoot + "/Data";
        private const string kGen  = kData + "/Generated";
        private const string kEventsDir    = kGen + "/Events";
        private const string kActorsDir    = kGen + "/Actors";
        private const string kResourcesDir = kGen + "/Resources";
        private const string kArcsDir      = kGen + "/StoryArcs";
        private const string kSetsDir      = kGen + "/Sets";

        private Vector2 _scroll;

        [MenuItem("Tools/Narrative Framework/Game Generation Wizard", priority = 420)]
        public static void Open()
        {
            var w = GetWindow<GameGenerationWizard>("Game Generation Wizard");
            w.minSize = new Vector2(720, 520);
            w.Show();
        }

        private void OnEnable()
        {
            EnsureFolders();

            if (_settings == null)
            {
                var guids = AssetDatabase.FindAssets("t:GameGenerationSettingsSO");
                if (guids.Length > 0)
                    _settings = AssetDatabase.LoadAssetAtPath<GameGenerationSettingsSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_prompts == null)
            {
                var guids = AssetDatabase.FindAssets("t:PromptSettingsSO");
                if (guids.Length > 0)
                    _prompts = AssetDatabase.LoadAssetAtPath<PromptSettingsSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_targetGameData == null)
            {
                var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/NarrativeFrameworkForUnity/Resources/GameData.asset");
                if (canonical != null) _targetGameData = canonical;
            }
        }

        private void OnGUI()
        {
            NarrativeLinkerGUI.Toolbar(
                ("Generate", Generate),
                ("Create Sandbox Scene…", () => {
                    if (EditorApplication.isPlaying)
                    {
                        EditorUtility.DisplayDialog("Sandbox Scene", "Exit Play Mode before creating a sandbox scene.", "OK");
                        return;
                    }
                    SandboxSceneUtility.CreateSandboxScene();
                })
            );

            NarrativeLinkerGUI.Scroll(ref _scroll, () =>
            {
                DrawConfigCard();
                GUILayout.Space(6);
                DrawValidationCard();
                GUILayout.Space(6);
                DrawFooterCard();
            });
        }

        private void DrawConfigCard()
        {
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

                _settings = (GameGenerationSettingsSO)EditorGUILayout.ObjectField(
                    "Generation Settings", _settings, typeof(GameGenerationSettingsSO), false);
                _prompts = (PromptSettingsSO)EditorGUILayout.ObjectField(
                    "Prompt Settings", _prompts, typeof(PromptSettingsSO), false);

                GUILayout.Space(4);
                _targetGameData = (GameData)EditorGUILayout.ObjectField(
                    new GUIContent("Target GameData", "Generated assets will be added to this GameData if 'Generate + Wire' is selected."),
                    _targetGameData, typeof(GameData), false);

                GUILayout.Space(4);
                _generateAssetsOnly = EditorGUILayout.ToggleLeft(
                    new GUIContent("Generate Assets Only (no wiring)",
                        "Creates assets under Data/Generated but does not modify GameData. You can wire later."),
                    _generateAssetsOnly);
            });
        }

        private void DrawValidationCard()
        {
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.LabelField("Pre-flight Checks", EditorStyles.boldLabel);

                bool okSettings = _settings != null;
                bool okPrompts  = _prompts  != null;

                int countInResources = AssetDatabase.FindAssets("t:GameData", new[] { "Assets/NarrativeFrameworkForUnity/Resources" }).Length;
                bool exactlyOneInResources = countInResources == 1;

                Report("Generation Settings", okSettings);
                Report("Prompt Settings",     okPrompts);
                Report("Exactly one GameData in Resources", exactlyOneInResources, warn: true);

                if (!_generateAssetsOnly)
                    Report("Target GameData selected", _targetGameData != null);

                GUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Best practice: keep a single canonical GameData in Resources.\n" +
                    "The wizard never creates a second GameData. Choose a Target GameData explicitly.",
                    MessageType.Info);
            });

            static void Report(string label, bool ok, bool warn = false)
            {
                var c = GUI.color;
                GUI.color = ok ? Color.green : (warn ? new Color(1f, .7f, 0f) : Color.red);
                GUILayout.Label($"• {label}: {(ok ? "OK" : "Check")}");
                GUI.color = c;
            }
        }

        private void DrawFooterCard()
        {
            NarrativeLinkerGUI.VerticalBox(() =>
            {
                EditorGUILayout.HelpBox(
                    "You can plug your own generator pipeline via GameGenerationWizard.ExternalGenerateFunc.\n" +
                    "If not provided, placeholder assets are created so the wiring and UI flow can be tested.",
                    MessageType.None);

                NarrativeLinkerGUI.Horizontal(() =>
                {
                    if (GUILayout.Button("Ping GameData", GUILayout.Width(140)) && _targetGameData)
                        EditorGUIUtility.PingObject(_targetGameData);

                    if (GUILayout.Button("Open Resources Folder", GUILayout.Width(180)))
                        EditorUtility.RevealInFinder(Application.dataPath + "/NarrativeFramework/Resources");

                    if (GUILayout.Button("Open Generated Folder", GUILayout.Width(180)))
                        EditorUtility.RevealInFinder(Application.dataPath + "/NarrativeFramework/Data/Generated");

                    GUILayout.FlexibleSpace();
                });
            });
        }

        private void Generate()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Game Generation", "Exit Play Mode before generating content.", "OK");
                return;
            }

            if (_settings == null || _prompts == null)
            {
                EditorUtility.DisplayDialog("Game Generation", "Assign Generation Settings and Prompt Settings.", "OK");
                return;
            }

            if (!_generateAssetsOnly && _targetGameData == null)
            {
                EditorUtility.DisplayDialog("Game Generation", "Select a Target GameData or switch to 'Assets Only'.", "OK");
                return;
            }

            int gdInResources = AssetDatabase.FindAssets("t:GameData", new[] { "Assets/NarrativeFrameworkForUnity/Resources" }).Length;
            if (gdInResources != 1)
            {
                if (!EditorUtility.DisplayDialog("GameData in Resources",
                    "Exactly one GameData in 'Assets/NarrativeFrameworkForUnity/Resources' is recommended.\nProceed anyway?",
                    "Proceed", "Cancel"))
                    return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Generating", "Creating content…", 0.1f);

                var dataSet = ExternalGenerateFunc != null
                    ? ExternalGenerateFunc.Invoke(_settings, _prompts)
                    : CreatePlaceholderDataSet();

                if (dataSet == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Game Generation", "No data was generated.", "OK");
                    return;
                }

                EnsureFolders();
                var setPath = AssetDatabase.GenerateUniqueAssetPath($"{kSetsDir}/GeneratedDataSet.asset");
                AssetDatabase.CreateAsset(dataSet, setPath);

                EditorUtility.DisplayProgressBar("Generating", "Wiring to GameData…", 0.55f);

                if (!_generateAssetsOnly && _targetGameData != null)
                {
                    WireToGameData(_targetGameData, dataSet);
                }

                EditorUtility.DisplayProgressBar("Generating", "Finalizing…", 0.9f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                int ev = dataSet.events != null ? dataSet.events.Length : 0;
                int ac = dataSet.actors != null ? dataSet.actors.Length : 0;
                int rs = dataSet.resources != null ? dataSet.resources.Length : 0;
                int ar = dataSet.storyArcs != null ? dataSet.storyArcs.Length : 0;

                EditorUtility.DisplayDialog("Generation Complete",
                    $"Created set:\n" +
                    $"- Events: {ev}\n- Actors: {ac}\n- Resources: {rs}\n- Story Arcs: {ar}\n\n" +
                    (_generateAssetsOnly
                        ? "Assets were not wired to GameData (Assets Only mode)."
                        : $"Assets were appended to:\n{AssetDatabase.GetAssetPath(_targetGameData)}"),
                    "OK");

                EditorGUIUtility.PingObject(dataSet);
                Selection.activeObject = dataSet;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameGenerationWizard] Generation failed: {ex.Message}\n{ex}");
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Generation Error", ex.Message, "OK");
            }
        }

        private static void WireToGameData(GameData target, GeneratedDataSetSO dataSet)
        {
            if (target == null || dataSet == null) return;

            var so = new SerializedObject(target);

            var pEvents    = NarrativeLinkerGUI.FindPropAny(so, "_events", "events");
            var pActors    = NarrativeLinkerGUI.FindPropAny(so, "_actors", "actors");
            var pResources = NarrativeLinkerGUI.FindPropAny(so, "_resources", "resources");
            var pArcs      = NarrativeLinkerGUI.FindPropAny(so, "_storyArcs", "storyArcs");

            so.Update();

            AppendAll(pEvents,    dataSet.events);
            AppendAll(pActors,    dataSet.actors);
            AppendAll(pResources, dataSet.resources);
            AppendAll(pArcs,      dataSet.storyArcs);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            static void AppendAll(SerializedProperty arrayProp, UnityEngine.Object[] objs)
            {
                if (arrayProp == null || !arrayProp.isArray || objs == null) return;
                foreach (var obj in objs)
                {
                    int i = arrayProp.arraySize;
                    arrayProp.InsertArrayElementAtIndex(i);
                    arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = obj;
                }
            }
        }

        /// <summary>
        /// Creates a minimal placeholder dataset if no external generator is set.
        /// Uses NarrativeLinkerGUI.CreateAsset and sets common fields via SerializedObject (tolerant).
        /// </summary>
        private GeneratedDataSetSO CreatePlaceholderDataSet()
        {
            EnsureFolders();

            var set = ScriptableObject.CreateInstance<GeneratedDataSetSO>();

            var ev = NarrativeLinkerGUI.CreateAsset<SimEventSO>(kEventsDir, "GEN_Event");
            ev.Title = "Generated Event";
            ev.Description = "This is a placeholder event created by the wizard.";

            var actor = NarrativeLinkerGUI.CreateAsset<ActorSO>(kActorsDir, "GEN_Actor");
            // Tolerant display-name init for ActorSO:
            // Try the public constant first, then fallbacks.
            TrySetStringField(actor, ActorSO.PROP_NAME, "_displayName", "_name", "Generated Actor");

            var res = NarrativeLinkerGUI.CreateAsset<Resource>(kResourcesDir, "GEN_Resource");
            // Resource fields can be edited later in Resources view.

            var arc = NarrativeLinkerGUI.CreateAsset<StoryArcSO>(kArcsDir, "GEN_StoryArc");
            // Tolerant title init for StoryArc
            TrySetStringField(arc, "Title", "_title", "Generated Arc");

            set.events    = new[] { ev };
            set.actors    = new[] { actor };
            set.resources = new[] { res };
            set.storyArcs = new[] { arc };

            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>
        /// Tries to set a string property on an object using SerializedObject by checking
        /// multiple candidate property names. The last argument is the value to assign.
        /// Usage:
        ///   TrySetStringField(obj, "FieldA", "FieldB", "value");
        /// </summary>
        private static void TrySetStringField(UnityEngine.Object obj, params string[] nameThenValue)
        {
            if (obj == null || nameThenValue == null || nameThenValue.Length < 2) return;

            string value = nameThenValue[^1];
            var so = new SerializedObject(obj);
            bool set = false;

            for (int i = 0; i < nameThenValue.Length - 1; i++)
            {
                var p = so.FindProperty(nameThenValue[i]);
                if (p != null && p.propertyType == SerializedPropertyType.String)
                {
                    p.stringValue = value;
                    set = true;
                    break;
                }
            }

            if (set) so.ApplyModifiedProperties();
        }

        private static void EnsureFolders()
        {
            void Ensure(string path)
            {
                if (!AssetDatabase.IsValidFolder(path))
                {
                    var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                    var leaf   = Path.GetFileName(path);
                    if (!AssetDatabase.IsValidFolder(parent))
                        Ensure(parent);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }

            Ensure(kRoot);
            Ensure(kData);
            Ensure(kGen);
            Ensure(kEventsDir);
            Ensure(kActorsDir);
            Ensure(kResourcesDir);
            Ensure(kArcsDir);
            Ensure(kSetsDir);
        }
    }
}
#endif
