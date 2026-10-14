// Assets/SimulationGame/Editor/Views/NarrativeLinkerNewEventView.cs
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using SimulationGame.OpenAI;

namespace SimulationGame.Editor
{
    public class NarrativeLinkerNewEventView
    {
        private const string ModuleRoot = "Assets/SimulationGame";
        private const string ResourcesDir = ModuleRoot + "/Resources";
        private const string GameDataAssetPath = ResourcesDir + "/GameData.asset";
        private const string EventsDir = ModuleRoot + "/Data/Events";
        private const string ChoicesDir = ModuleRoot + "/Data/Choices";
        private const string OutcomesDir = ModuleRoot + "/Data/Outcomes";
        private const string IllustrationsDir = ModuleRoot + "/Data/Illustrations";

        private GameData _gameData;

        // --- UI state shared ---
        private int minTimeUnit = 1;
        private int maxTimeUnit = 30;
        private float probability = 1.0f; // 0..1
        private SimEventSO.EventType eventType = SimEventSO.EventType.Main;
        private StoryArcSO selectedStoryArc;
        private readonly List<string> requiredFlags = new();
        private string requiredFlagInput = "";
        private readonly List<string> forbiddenFlags = new();
        private string forbiddenFlagInput = "";
        private readonly List<string> storyTags = new();
        private string storyTagInput = "";
        private readonly List<ActorSO> involvedActors = new();

        // --- Manual only ---
        private string manualTitle = "";
        private string manualDescription = "";
        private Sprite manualIllustration = null;

        // --- AI only ---
        private string generationPrompt = "Describe a dramatic and mysterious narrative event.";
        private string imagePrompt = "A dark snowy forest with a ruined shrine";

        // tabs
        private enum Tab { AI, Manual }
        private Tab _tab = Tab.AI;
        private Vector2 _scroll;

        public void Init(GameData gameData)
        {
            _gameData = gameData;
            if (_gameData != null) NarrativeLinkerContext.SetActive(_gameData); // publikuj do reszty zakładek
        }

        private bool _initialized;    // guard for OnEnable


        public void Draw()
        {
            var current = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(current, _gameData))
                _gameData = current;
            if (_gameData == null)
                _gameData = EnsureGameDataAsset(_gameData); // try to resolve


            // Toolbar (safe, doesn't use Begin/End GUI outside of scope)
            NarrativeLinkerGUI.Toolbar(
                ("AI", () => _tab = Tab.AI),
                ("Manual", () => _tab = Tab.Manual),
                ("Rescan", RescanAndFixGameData)
            );

            NarrativeLinkerGUI.Scroll(ref _scroll, () =>
            {
                GUILayout.Label("Create New Event", NarrativeLinkerGUI.Header);

                DrawSharedMeta();

                switch (_tab)
                {
                    case Tab.AI:
                        DrawAI();
                        break;
                    case Tab.Manual:
                        DrawManual();
                        break;
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "If GameData list seems out of sync, use Rescan or the button above.",
                    MessageType.Info
                );
            });
        }

        // ---------- shared meta used by both tabs ----------
        private void DrawSharedMeta()
        {
            EditorGUILayout.Space();
            minTimeUnit = EditorGUILayout.IntField("Min Time Unit", minTimeUnit);
            maxTimeUnit = Mathf.Max(minTimeUnit, EditorGUILayout.IntField("Max Time Unit", maxTimeUnit));
            probability = EditorGUILayout.Slider("Probability", probability, 0f, 1f);
            eventType = (SimEventSO.EventType)EditorGUILayout.EnumPopup("Event Type", eventType);
            selectedStoryArc = (StoryArcSO)EditorGUILayout.ObjectField("Story Arc", selectedStoryArc, typeof(StoryArcSO), false);

            // Flags
            EditorGUILayout.LabelField("Required Flags", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                requiredFlagInput = EditorGUILayout.TextField(requiredFlagInput);
                if (GUILayout.Button("+", GUILayout.Width(26)) && !string.IsNullOrEmpty(requiredFlagInput))
                {
                    if (!requiredFlags.Contains(requiredFlagInput)) requiredFlags.Add(requiredFlagInput);
                    requiredFlagInput = "";
                }
            }
            foreach (var f in requiredFlags) EditorGUILayout.LabelField("• " + f);

            EditorGUILayout.LabelField("Forbidden Flags", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                forbiddenFlagInput = EditorGUILayout.TextField(forbiddenFlagInput);
                if (GUILayout.Button("+", GUILayout.Width(26)) && !string.IsNullOrEmpty(forbiddenFlagInput))
                {
                    if (!forbiddenFlags.Contains(forbiddenFlagInput)) forbiddenFlags.Add(forbiddenFlagInput);
                    forbiddenFlagInput = "";
                }
            }
            foreach (var f in forbiddenFlags) EditorGUILayout.LabelField("• " + f);

            // Tags
            EditorGUILayout.LabelField("Story Tags", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                storyTagInput = EditorGUILayout.TextField(storyTagInput);
                if (GUILayout.Button("+", GUILayout.Width(26)) && !string.IsNullOrEmpty(storyTagInput))
                {
                    if (!storyTags.Contains(storyTagInput)) storyTags.Add(storyTagInput);
                    storyTagInput = "";
                }
            }
            foreach (var t in storyTags) EditorGUILayout.LabelField("• " + t);

            // Actors
            EditorGUILayout.LabelField("Involved Actors", EditorStyles.boldLabel);
            if (_gameData != null && _gameData.Actors != null)
            {
                foreach (var actor in _gameData.Actors)
                {
                    bool selected = involvedActors.Contains(actor);
                    bool toggle = EditorGUILayout.ToggleLeft(actor.ActorName, selected);
                    if (toggle && !selected) involvedActors.Add(actor);
                    if (!toggle && selected) involvedActors.Remove(actor);
                }
            }
        }

        // ---------- AI tab ----------
        private void DrawAI()
        {
            EditorGUILayout.Space();
            generationPrompt = EditorGUILayout.TextField("AI Prompt (Description)", generationPrompt);
            imagePrompt = EditorGUILayout.TextField("AI Prompt (Illustration)", imagePrompt);

            if (GUILayout.Button("✨ One-Click Generate Full Event"))
                _ = GenerateFullEventAsync();
        }

        // ---------- Manual tab ----------
        private void DrawManual()
        {
            EditorGUILayout.Space();
            manualTitle = EditorGUILayout.TextField("Title", manualTitle);
            manualDescription = EditorGUILayout.TextField("Description", manualDescription);

            // NEW: manual illustration picker
            manualIllustration = (Sprite)EditorGUILayout.ObjectField(
                "Main Illustration (optional)",
                manualIllustration,
                typeof(Sprite),
                false
            );

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(manualTitle)))
            {
                if (GUILayout.Button("📦 Create Event from Manual Input"))
                {
                    var draft = MakeDraft(
                        title: manualTitle,
                        description: manualDescription,
                        illustration: manualIllustration     // <— pass it through
                    );
                    CreateFromDraft(draft);

                    // reset
                    manualTitle = manualDescription = "";
                    manualIllustration = null;             // <— reset
                }
            }
        }


        // ---------- Draft model shared by both flows ----------
        private sealed class EventDraft
        {
            public string title;
            public string description;
            public Sprite illustration;

            public int minTimeUnit;
            public int maxTimeUnit;
            public float probability;
            public SimEventSO.EventType eventType;
            public StoryArcSO storyArc;
            public List<string> requiredFlags;
            public List<string> forbiddenFlags;
            public List<string> storyTags;
            public List<ActorSO> involvedActors;
        }

        private EventDraft MakeDraft(string title, string description, Sprite illustration)
        {
            return new EventDraft
            {
                title = title,
                description = description,
                illustration = illustration,

                minTimeUnit = minTimeUnit,
                maxTimeUnit = maxTimeUnit,
                probability = probability,
                eventType = eventType,
                storyArc = selectedStoryArc,
                requiredFlags = new List<string>(requiredFlags),
                forbiddenFlags = new List<string>(forbiddenFlags),
                storyTags = new List<string>(storyTags),
                involvedActors = new List<ActorSO>(involvedActors)
            };
        }

        // ---------- AI path fills the same draft and calls CreateFromDraft ----------
        private async Task GenerateFullEventAsync()
        {
            string sys =
                $"Generate a JSON with a title and description of a narrative event. " +
                $"Example: {{\"title\":\"Snowfall\",\"description\":\"A blizzard hits the camp...\"}}. " +
                $"Prompt: {generationPrompt}";

            var response = await OpenAIGenerator.GenerateEvent(sys);
            if (string.IsNullOrEmpty(response)) return;

            int s = response.IndexOf('{');
            int e = response.LastIndexOf('}');
            if (s < 0 || e <= s) return;

            var parsed = JsonUtility.FromJson<OpenAIEventResponse>(response.Substring(s, e - s + 1));
            if (parsed == null) return;

            // optional image
            Sprite illustration = null;
            string imageUrl = await OpenAIGenerator.GenerateImage(imagePrompt);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                using var wc = new System.Net.WebClient();
                byte[] bytes = wc.DownloadData(imageUrl);
                Directory.CreateDirectory(IllustrationsDir);
                string path = Path.Combine(IllustrationsDir, $"generated_{System.Guid.NewGuid()}.png");
                File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.SaveAndReimport();
                }
                illustration = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var draft = MakeDraft(parsed.title, parsed.description, illustration);
            CreateFromDraft(draft);
        }

        // ---------- Single creation path used by both tabs ----------
        private void CreateFromDraft(EventDraft d)
        {
            Directory.CreateDirectory(EventsDir);
            Directory.CreateDirectory(ChoicesDir);
            Directory.CreateDirectory(OutcomesDir);

            // 1) Default Outcome (jak było)
            var defaultOutcome = ScriptableObject.CreateInstance<Outcome>();
            defaultOutcome.name = $"Outcome_{NarrativeLinkerGUI.Sanitize(d.title)}";
            defaultOutcome.Initialize(OutcomeType.TriggerEvent, "", 0.0f, 1.0f, "");
            AssetDatabase.CreateAsset(defaultOutcome, Path.Combine(OutcomesDir, defaultOutcome.name + ".asset"));
            EditorUtility.SetDirty(defaultOutcome);

            // 2) Default Choice (jak było)
            var defaultChoice = ScriptableObject.CreateInstance<Choice>();
            defaultChoice.name = $"Choice_{NarrativeLinkerGUI.Sanitize(d.title)}";
            defaultChoice.ChoiceText = "Proceed";
            defaultChoice.GoToNextDay = true;
            defaultChoice.AddOutcome(defaultOutcome);
            AssetDatabase.CreateAsset(defaultChoice, Path.Combine(ChoicesDir, defaultChoice.name + ".asset"));
            EditorUtility.SetDirty(defaultChoice);

            // 3) SimEvent (różnica: bezpieczne ID z GameData)
            var newEvent = ScriptableObject.CreateInstance<SimEventSO>();
            newEvent.name = NarrativeLinkerGUI.Sanitize(d.title);
            newEvent.simEventTitle = d.title;
            newEvent.simEventDescription = d.description;
            newEvent.mainIllustration = d.illustration;

            _gameData = _gameData ?? NarrativeLinkerContext.GetOrResolve() ?? EnsureGameDataAsset(_gameData);

            if (_gameData != null)
            {
                int next = _gameData.IncrementEventId();
                newEvent.simEventId = $"EVT_{next}";
                EditorUtility.SetDirty(_gameData);
            }
            else
            {
                newEvent.simEventId = System.Guid.NewGuid().ToString();
            }

            // meta
            newEvent.MinTimeUnit = d.minTimeUnit;
            newEvent.MaxTimeUnit = Mathf.Max(d.minTimeUnit, d.maxTimeUnit);
            newEvent.Probability = d.probability;
            newEvent.Type = d.eventType;
            newEvent.StoryArc = d.storyArc;
            newEvent.requiredFlags = d.requiredFlags;
            newEvent.ForbiddenFlags = d.forbiddenFlags;
            newEvent.storyTags = d.storyTags;
            newEvent.choices = new List<Choice> { defaultChoice };
            newEvent.LinkedEvents = new List<SimEventSO>();
            newEvent.InvolvedActors = d.involvedActors;
            newEvent.Outcomes = new List<Outcome>();

            string eventPath = Path.Combine(EventsDir, newEvent.name + ".asset");
            AssetDatabase.CreateAsset(newEvent, eventPath);
            EditorUtility.SetDirty(newEvent);

            // 4) Rejestracja w GameData (jak było)
            if (_gameData != null)
            {
                _gameData.AddEvent(newEvent);
                EditorUtility.SetDirty(_gameData);
            }
            else
            {
                Debug.LogWarning("GameData not found. Event created but not registered.");
            }

#if UNITY_EDITOR
            // 5) Story Arc link (as it was)
            if (d.storyArc != null)
            {
                d.storyArc.AddEventToArc(newEvent);
                if (d.storyArc.TriggeredEvent == null)
                    d.storyArc.SetTriggeredEvent(newEvent);
                EditorUtility.SetDirty(d.storyArc);
            }
#endif
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = newEvent;
            EditorGUIUtility.PingObject(newEvent);
            Debug.Log($"✅ New event created. simEventId = {newEvent.simEventId}");
        }

        // ---------- Utils ----------
        private static GameData EnsureGameDataAsset(GameData current)
        {
            if (current != null && AssetDatabase.Contains(current)) return current;

            var canonical = AssetDatabase.LoadAssetAtPath<GameData>(GameDataAssetPath);
            if (canonical != null) return canonical;

            string[] guids = AssetDatabase.FindAssets("t:GameData");
            GameData any = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<GameData>(p);
                if (a == null) continue;
                if (p.Contains("/SimulationGame/")) return a;
                any ??= a;
            }
            if (any != null) return any;

            Directory.CreateDirectory(ResourcesDir);
            var created = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(created, GameDataAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"[Editor] Created new GameData at {GameDataAssetPath}");
            return created;
        }

        private void RescanAndFixGameData()
        {
            _gameData = _gameData ?? NarrativeLinkerContext.GetOrResolve() ?? EnsureGameDataAsset(_gameData);
            if (_gameData == null) return;

            string[] guids = AssetDatabase.FindAssets("t:SimEventSO");
            int added = 0;

            Undo.RecordObject(_gameData, "Rescan Events");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var evt = AssetDatabase.LoadAssetAtPath<SimEventSO>(path);
                if (evt != null && !_gameData.Events.Any(e => e == evt))
                {
                    _gameData.AddEvent(evt);
                    added++;
                }
            }
            EditorUtility.SetDirty(_gameData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Rescan finished. Added {added} missing events to GameData.");
        }
        public void OnEnable()
        {
            if (_initialized) return;
            _gameData = NarrativeLinkerContext.GetOrResolve();
            NarrativeLinkerContext.Changed += OnActiveGameDataChanged;
            _initialized = true;
        }

        public void OnDisable()
        {
            NarrativeLinkerContext.Changed -= OnActiveGameDataChanged;
        }

        private void OnActiveGameDataChanged(GameData gd)
        {
            if (ReferenceEquals(_gameData, gd)) return;
            _gameData = gd;
            InternalEditorUtility.RepaintAllViews();
        }

    }
}
