#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SimulationGame;        // GameData
using SimulationGame.Editor; // NarrativeLinkerGUI & SingleEventAdapterImporter

namespace SimulationGame
{
    /// <summary>
    /// UI window for importing a SINGLE Event JSON.
    /// - Validates and previews the JSON.
    /// - Wraps it for the Scenario importer via SingleEventAdapterImporter.
    /// - Registers new assets in a chosen GameData (remembered via EditorPrefs).
    /// </summary>
    public class SingleEventAdapterImporterWindow : EditorWindow
    {
        // ─────────────────────────────────────────────────────────────
        // UI state
        // ─────────────────────────────────────────────────────────────
        private TextAsset _jsonAsset;
        [SerializeField, TextArea(6, 20)] private string _jsonText = string.Empty;

        private bool _useTextAsset = true;
        private bool _createPlaceholders = true;
        private bool _defaultChoiceIfEmpty = true;
        private string _placeholdersRoot = "Assets/SimulationGame/Data/Placeholders";

        private GameData _registerInGameData;               // target GameData (optional; will be auto-resolved if null)
        private const string PREF_GAMEDATA_PATH = "NarrativeLinker.PreferredGameDataPath";

        private Vector2 _scroll;
        private string _validationMessage = "";
        private ValidationResult _lastValidation;

        [MenuItem("Tools/Narrative Framework/Import Single Event (UI)")]
        public static void Open() => GetWindow<SingleEventAdapterImporterWindow>("Single Event Import");

        private void OnEnable()
        {
            // Default placeholders folder
            if (string.IsNullOrWhiteSpace(_placeholdersRoot))
                _placeholdersRoot = "Assets/SimulationGame/Data/Placeholders";

            // Try to restore last-picked GameData
            var prefPath = EditorPrefs.GetString(PREF_GAMEDATA_PATH, null);
            if (!string.IsNullOrEmpty(prefPath))
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameData>(prefPath);
                if (pref != null) _registerInGameData = pref;
            }
        }

        private void OnGUI()
        {
            // Top toolbar
            NarrativeLinkerGUI.Toolbar(
                ("Validate", ValidateClicked),
                ("Import", ImportClicked),
                ("Create Sample JSON", CreateSampleJsonClicked),
                ("Clear", ClearClicked)
            );

            NarrativeLinkerGUI.Scroll(ref _scroll, () =>
            {
                // Source block
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    GUILayout.Label("Source", NarrativeLinkerGUI.Header);
                    _useTextAsset = EditorGUILayout.Toggle(new GUIContent("Use TextAsset", "ON: read from the TextAsset below. OFF: use the inline text field."), _useTextAsset);

                    if (_useTextAsset)
                    {
                        var next = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Event JSON (TextAsset)"), _jsonAsset, typeof(TextAsset), false);
                        if (next != _jsonAsset)
                        {
                            _jsonAsset = next;
                            if (_jsonAsset != null && string.IsNullOrWhiteSpace(_jsonText))
                                _jsonText = _jsonAsset.text; // convenience for switching to inline later
                        }
                        EditorGUILayout.HelpBox("Provide a TextAsset (.json) with a single-event JSON.", MessageType.Info);
                    }
                    else
                    {
                        GUILayout.Label("Event JSON (inline text)");
                        _jsonText = EditorGUILayout.TextArea(_jsonText, GUILayout.MinHeight(120));
                        EditorGUILayout.HelpBox("Paste single-event JSON here. Used only when \"Use TextAsset\" is OFF.", MessageType.Info);
                    }
                });

                EditorGUILayout.Space(6);

                // Options block
                NarrativeLinkerGUI.VerticalBox(() =>
                {
                    GUILayout.Label("Options", NarrativeLinkerGUI.Header);

                    _createPlaceholders = EditorGUILayout.Toggle(
                        new GUIContent("Create Placeholders", "Create placeholder ScriptableObjects for missing referenced assets (Resources, Variables, Events, StoryArcs, Actors)."),
                        _createPlaceholders);

                    _defaultChoiceIfEmpty = EditorGUILayout.Toggle(
                        new GUIContent("Default Choice if Empty", "If the event has no choices, add a default \"Continue\" choice."),
                        _defaultChoiceIfEmpty);

                    NarrativeLinkerGUI.Horizontal(() =>
                    {
                        _placeholdersRoot = EditorGUILayout.TextField(
                            new GUIContent("Placeholders Folder", "Folder under Assets/ for placeholder assets (if enabled)."),
                            _placeholdersRoot);

                        if (GUILayout.Button("Pick…", GUILayout.Width(70)))
                        {
                            var picked = EditorUtility.OpenFolderPanel("Pick placeholders folder", Application.dataPath, "");
                            if (!string.IsNullOrEmpty(picked))
                            {
                                var rel = MakeRelativeToAssets(picked);
                                if (rel.StartsWith("Assets", StringComparison.Ordinal))
                                    _placeholdersRoot = rel.Replace("\\", "/");
                                else
                                    EditorUtility.DisplayDialog("Invalid Folder", "Please pick a folder inside your project's Assets/ directory.", "OK");
                            }
                        }
                    });

                    // Target GameData selector (remember last choice)
                    var chosen = (GameData)EditorGUILayout.ObjectField(
                        new GUIContent("Register in GameData", "If set, any newly-created assets (placeholders etc.) are appended to this GameData. If left empty, the importer will auto-resolve (last-picked → canonical → any)."),
                        _registerInGameData, typeof(GameData), false);

                    if (chosen != _registerInGameData)
                    {
                        _registerInGameData = chosen;
                        var path = AssetDatabase.GetAssetPath(_registerInGameData);
                        if (!string.IsNullOrEmpty(path))
                            EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
                    }
                });

                EditorGUILayout.Space(6);

                // Validation messages
                if (!string.IsNullOrWhiteSpace(_validationMessage))
                {
                    var type = (_lastValidation != null && _lastValidation.IsValid) ? MessageType.Info : MessageType.Warning;
                    EditorGUILayout.HelpBox(_validationMessage, type);
                }

                // Preview block
                if (_lastValidation != null)
                {
                    NarrativeLinkerGUI.VerticalBox(() =>
                    {
                        GUILayout.Label("Parsed Preview", NarrativeLinkerGUI.Header);
                        DrawPreview(_lastValidation);
                    });
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Actions
        // ─────────────────────────────────────────────────────────────
        private void ValidateClicked()
        {
            try
            {
                var json = GetSourceJsonOrThrow();
                _lastValidation = ValidateJson(json);
                _validationMessage = _lastValidation.IsValid
                    ? "Validation OK. The event can be wrapped and imported."
                    : "Validation found issues. You can still import, but fixes are recommended.";
            }
            catch (Exception ex)
            {
                _lastValidation = null;
                _validationMessage = $"Validation failed: {ex.Message}";
            }
        }

        private void ImportClicked()
        {
            try
            {
                var json = GetSourceJsonOrThrow();

                // Validate first (non-blocking if user decides to continue)
                _lastValidation = ValidateJson(json);
                if (!_lastValidation.IsValid)
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "Import with Warnings?",
                        "Validation found issues. Do you want to continue with import?",
                        "Import Anyway", "Cancel");
                    if (!proceed) return;
                }

                // Resolve GameData if none chosen
                var targetGd = _registerInGameData != null ? _registerInGameData : ResolveGameDataPreferPrefsOrCanonical();
                if (targetGd == null)
                    throw new Exception("Failed to resolve a GameData. Create or pick one first.");

                // Remember chosen GD
                var path = AssetDatabase.GetAssetPath(targetGd);
                if (!string.IsNullOrEmpty(path))
                    EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);

                // Delegate to adapter (which wraps and calls Scenario importer)
                SingleEventAdapterImporter.ImportSingleEventJson(
                    json,
                    createPlaceholders: _createPlaceholders,
                    createDefaultChoiceIfEmpty: _defaultChoiceIfEmpty,
                    placeholdersRoot: _placeholdersRoot,
                    registerNewAssetsInGameData: targetGd
                );

                EditorUtility.DisplayDialog("Import Complete", $"Single event imported into GameData '{targetGd.name}'.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
            }
        }

        private void CreateSampleJsonClicked()
        {
            try
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Create Sample Single-Event JSON",
                    "SampleEvent.json",
                    "json",
                    "Choose a location under Assets/ to create a sample JSON."
                );
                if (string.IsNullOrEmpty(path)) return;

                string json = GenerateSampleJson();
                WriteTextAsset(path, json);

                AssetDatabase.ImportAsset(path);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Selection.activeObject = asset;

                // Also show it inline for quick edits if user wants to toggle off Use TextAsset
                _jsonAsset = asset;
                _jsonText = json;

                EditorUtility.DisplayDialog("Sample Created", $"Created: {path}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Create Sample Failed", ex.Message, "OK");
            }
        }

        private void ClearClicked()
        {
            _jsonAsset = null;
            _jsonText = "";
            _validationMessage = "";
            _lastValidation = null;
        }

        // ─────────────────────────────────────────────────────────────
        // Validation & Preview
        // ─────────────────────────────────────────────────────────────
        private string GetSourceJsonOrThrow()
        {
            if (_useTextAsset)
            {
                if (_jsonAsset == null) throw new Exception("Please assign a TextAsset with single-event JSON.");
                if (string.IsNullOrWhiteSpace(_jsonAsset.text)) throw new Exception("The assigned TextAsset is empty.");
                return _jsonAsset.text;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_jsonText)) throw new Exception("Please paste single-event JSON.");
                return _jsonText;
            }
        }

        private ValidationResult ValidateJson(string json)
        {
            var result = new ValidationResult();

            SingleEventJson data = null;
            try
            {
                data = JsonUtility.FromJson<SingleEventJson>(json);
                if (data == null) throw new Exception("JSON is not a valid single-event shape.");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add("Parse error: " + ex.Message);
                return result;
            }

            data.choices ??= new List<ChoiceJsonIn>();
            foreach (var ch in data.choices) ch.outcomes ??= new List<OutcomeJsonIn>();

            if (string.IsNullOrWhiteSpace(data.id) && string.IsNullOrWhiteSpace(data.title) && string.IsNullOrWhiteSpace(data.titleKey))
                result.Warnings.Add("No 'id' or 'title' provided. An id will be derived from title or defaulted.");

            result.EventId   = !string.IsNullOrWhiteSpace(data.id) ? data.id : "(will be derived)";
            result.TitleText = !string.IsNullOrWhiteSpace(data.title) ? data.title : (data.titleKey ?? "(empty)");
            result.DescriptionText = !string.IsNullOrWhiteSpace(data.description) ? Truncate(data.description, 160)
                                           : (data.descriptionKey ?? "(empty)");

            result.ChoiceCount = data.choices.Count;
            if (data.choices.Count == 0)
            {
                if (_defaultChoiceIfEmpty) result.Infos.Add("No choices found. A default \"Continue\" will be added.");
                else result.Warnings.Add("No choices found. Consider enabling \"Default Choice if Empty\" or add choices to avoid a dead-end event.");
            }

            var nextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outcomeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in data.choices)
            {
                if (!string.IsNullOrWhiteSpace(ch.nextEventId))
                    nextIds.Add(ch.nextEventId);

                foreach (var o in ch.outcomes)
                {
                    var t = (o.type ?? "").Trim();
                    if (!string.IsNullOrEmpty(t)) outcomeTypes.Add(t);
                    if (string.Equals(t, "TriggerEvent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(o.targetId))
                        nextIds.Add(o.targetId);
                }
            }

            result.ReferencedEventIds = nextIds.ToList();
            result.OutcomeTypes = outcomeTypes.ToList();
            result.IsValid = result.Errors.Count == 0;

            return result;
        }

        private void DrawPreview(ValidationResult vr)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Event Id", vr.EventId ?? "");
                EditorGUILayout.TextField("Title", vr.TitleText ?? "");
                EditorGUILayout.TextField("Description (preview)", vr.DescriptionText ?? "");
                EditorGUILayout.IntField("Choices", vr.ChoiceCount);
            }

            if (vr.OutcomeTypes != null && vr.OutcomeTypes.Count > 0)
            {
                GUILayout.Label("Outcome Types:");
                foreach (var t in vr.OutcomeTypes) EditorGUILayout.LabelField("• " + t);
            }

            if (vr.ReferencedEventIds != null && vr.ReferencedEventIds.Count > 0)
            {
                GUILayout.Label("Referenced Event Ids:");
                foreach (var id in vr.ReferencedEventIds) EditorGUILayout.LabelField("• " + id);
            }

            if (vr.Errors.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(string.Join("\n", vr.Errors), MessageType.Error);
            }
            if (vr.Warnings.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(string.Join("\n", vr.Warnings), MessageType.Warning);
            }
            if (vr.Infos.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(string.Join("\n", vr.Infos), MessageType.Info);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Sample JSON generation
        // ─────────────────────────────────────────────────────────────
        private string GenerateSampleJson()
        {
            // Keep it compatible with adapter + Scenario importer:
            // - string outcome types: ModifyResource / AddFlag / RemoveFlag / TriggerEvent
            // - choices/outcomes arrays present (can be empty)
            return
@"{
  ""id"": ""MOONLIT_BALCONY"",
  ""title"": ""Moonlit Balcony"",
  ""description"": ""You step into the quiet night; city lights blink below. Three figures wait—calm, fire, and silver poise. Your choice will echo."",
  ""choices"": [
    {
      ""text"": ""Thank Aurelius for his calm guidance."",
      ""goToNextDay"": false,
      ""nextEventId"": """",
      ""outcomes"": [
        { ""type"": ""ModifyResource"", ""targetId"": ""REL_AURELIUS"", ""value"": 1,  ""probability"": 1.0, ""description"": ""Aurelius appreciates your trust."" },
        { ""type"": ""ModifyResource"", ""targetId"": ""REL_KAEL"",     ""value"": -1, ""probability"": 1.0, ""description"": ""Kael feels sidelined."" }
      ]
    },
    {
      ""text"": ""Laugh with Kael and chase the moment."",
      ""goToNextDay"": false,
      ""nextEventId"": """",
      ""outcomes"": [
        { ""type"": ""ModifyResource"", ""targetId"": ""REL_KAEL"",     ""value"": 1,  ""probability"": 1.0, ""description"": ""Kael lights up."" },
        { ""type"": ""ModifyResource"", ""targetId"": ""REL_AURELIUS"",  ""value"": -1, ""probability"": 1.0, ""description"": ""Aurelius grows distant."" }
      ]
    },
    {
      ""text"": ""Take Lucien’s hand and whisper, 'Lead the way.'"",
      ""goToNextDay"": false,
      ""nextEventId"": ""LUC1_INTRO"",
      ""outcomes"": [
        { ""type"": ""TriggerEvent"", ""targetId"": ""LUC1_INTRO"", ""value"": 0, ""probability"": 1.0, ""description"": ""A new path opens."" }
      ]
    }
  ]
}";
        }

        private static void WriteTextAsset(string assetPath, string content)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) throw new Exception("Invalid asset path.");
            var projRoot = Directory.GetCurrentDirectory().Replace('\\','/');
            var fullPath = Path.Combine(projRoot, assetPath).Replace('\\','/');
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir)) throw new Exception("Invalid directory for asset.");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private static string MakeRelativeToAssets(string abs)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            abs = abs.Replace('\\', '/');
            if (abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + abs.Substring(dataPath.Length);
            return abs;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        /// <summary>
        /// Try to resolve a GameData if user didn’t assign one:
        /// last-picked (EditorPrefs) → canonical → any in project.
        /// Returns null if nothing can be resolved.
        /// </summary>
        private static GameData ResolveGameDataPreferPrefsOrCanonical()
        {
            // 1) Last-picked in NarrativeLinker/other views
            var prefPath = EditorPrefs.GetString(PREF_GAMEDATA_PATH, null);
            if (!string.IsNullOrEmpty(prefPath))
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameData>(prefPath);
                if (pref != null) return pref;
            }

            // 2) Canonical path
            var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/SimulationGame/Resources/GameData.asset");
            if (canonical != null) return canonical;

            // 3) Any in project
            foreach (var guid in AssetDatabase.FindAssets("t:GameData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var gd = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (gd != null) return gd;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // Local DTOs for validation/preview
        // ─────────────────────────────────────────────────────────────
        [Serializable] private class SingleEventJson
        {
            public string id;
            public string type;              // optional: "Ending" etc. (adapter/Scenario importer may ignore or use)
            public string title;
            public string description;
            public string titleKey;          // accepted for mapping convenience
            public string descriptionKey;
            public List<ChoiceJsonIn> choices;
        }

        [Serializable] private class ChoiceJsonIn
        {
            public string text;
            public bool goToNextDay;
            public string nextEventId;
            public List<OutcomeJsonIn> outcomes;
        }

        [Serializable] private class OutcomeJsonIn
        {
            public string type;              // "ModifyResource" | "AddFlag" | "RemoveFlag" | "TriggerEvent"
            public string targetId;
            public float value;
            public float probability;
            public string description;
        }

        private class ValidationResult
        {
            public bool IsValid = true;
            public string EventId;
            public string TitleText;
            public string DescriptionText;
            public int ChoiceCount;
            public List<string> ReferencedEventIds = new();
            public List<string> OutcomeTypes = new();
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
            public readonly List<string> Infos = new();
        }
    }
}
#endif
