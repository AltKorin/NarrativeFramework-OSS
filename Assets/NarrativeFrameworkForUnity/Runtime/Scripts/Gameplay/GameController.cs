// Assets/SimulationGame/Runtime/Scripts/Gameplay/GameController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UEObject = UnityEngine.Object;
using SimulationGame.UI;
using SimulationGame.Interfaces;
using SimulationGame.Safety;
using System.Linq;
using SimulationGame.DebugTools;
using SimulationGame.Managers;

namespace SimulationGame
{
    /// <summary>
    /// The central coordinator for the entire game. This class acts as the "brain" of the simulation,
    /// connecting all the different managers (Simulation, UI, Event, etc.) and managing the core game state,
    /// including resources, flags, and relationships.
    /// </summary>
    public partial class GameController : MonoBehaviour, ISaveDataProvider, ISaveDataReceiver
    {
        #region Serialized Fields
        [Header("Systems")]
        [SerializeField] private ActorSO _playerActor;

        [SerializeField] private SimulationManager _simulationSource;
        [SerializeField] private EventSystem _eventSystem;
        [SerializeField] private ChoiceEngine _choiceSource;
        [SerializeField] private UIManager _uiManagerSource;
        [SerializeField] private GlobalResourcesManager _resourceManagerSource;
        [SerializeField] private SaveManager _saveManagerSource;

        [Header("Story Stack (soft continuation)")]
        [Tooltip("A fallback collection of events to use when no other event is available.")]
        [SerializeField] private StoryStackSO _fallbackStoryStack;
        [Tooltip("If true, the Story Stack will be used to find an event when a choice doesn't lead to a specific next event.")]
        [SerializeField] private bool _useStoryStackOnNoNextEvent = true;
        [Tooltip("If true, the Story Stack can provide filler events between random daily events.")]
        [SerializeField] private bool _useStoryStackForRandomBeats = false;

        [Header("Endgame / Summary")]
        [Tooltip("If true, the game ends when any resource reaches its defined minimum value.")]
        [SerializeField] private bool _gameOverIfAnyResourceAtMin = true;
        [Tooltip("If true, the game ends when any resource drops to or below zero.")]
        [SerializeField] private bool _gameOverIfAnyResourceAtOrBelowZero = true;
        [Tooltip("A list of resource IDs to check for game over conditions. If empty, all resources are checked.")]
        [SerializeField] private List<string> _deathResources = new();
        [Tooltip("The default event to show when the game ends due to a resource failure, if no specific override is found.")]
        [SerializeField] private SimEventSO _defaultEndingEvent;
        [Tooltip("The event to show when the player successfully completes the campaign.")]
        [SerializeField] private SimEventSO _campaignCompletedEnding;
        [SerializeField] private bool _isGameRunning = true;


        #endregion

        #region Events
        public static event Action<RunSummary> OnRunEnded;
        public static event Action OnRunRestarted;
        public static event Action<string> OnAchievementAwarded;
        public static event Action<string> OnFeatureUnlocked;
        /// <summary>Fired whenever a flag is added. HC's FlagObserver subscribes here.</summary>
        public static event Action<string> OnFlagAdded;
        /// <summary>Fired whenever a flag is removed.</summary>
        public static event Action<string> OnFlagRemoved;
        #endregion

        #region Properties
        /// <summary>
        /// The singleton instance of the GameController.
        /// </summary>
        public static GameController Instance { get; private set; }

        /// <summary>
        /// The ScriptableObject representing the current player character.
        /// </summary>
        public ActorSO PlayerActor
        {
            get => _playerActor;
            set => _playerActor = value;
        }

        /// <summary>
        /// Provides access to the narrative variable system.
        /// </summary>
        public NarrativeVariableManager VariableManager => _variableManager;

        /// <summary>
        /// A dictionary holding the current values of all game resources.
        /// </summary>
        public Dictionary<string, float> Resources => _resources;

        /// <summary>
        /// An enumerable collection of all currently active game flags.
        /// </summary>
        public IEnumerable<string> Flags => _flags;

        /// <summary>
        /// A dictionary holding the current values of all character relationships.
        /// </summary>
        public Dictionary<string, float> Relationships => _relationships;

        /// <summary>
        /// The current day of the simulation.
        /// </summary>
        public int CurrentDay => _simulation.CurrentTimeUnit;

        /// <summary>
        /// The name of the current save file.
        /// </summary>
        public string SaveName => _saveName;

        /// <summary>
        /// The name of the slot the current save is in.
        /// </summary>
        public string SlotName => _slotName;

        /// <summary>
        /// A dictionary of all float narrative variables.
        /// </summary>
        public Dictionary<string, float> NarrativeVariables =>
            _narrativeVariableManager != null
                ? new Dictionary<string, float>(_narrativeVariableManager.GetFloatVariables())
                : new Dictionary<string, float>();

        /// <summary>
        /// A dictionary of all boolean narrative variables.
        /// </summary>
        public Dictionary<string, bool> NarrativeBooleans =>
            _narrativeVariableManager != null
                ? new Dictionary<string, bool>(_narrativeVariableManager.GetBoolVariables())
                : new Dictionary<string, bool>();

        /// <summary>
        /// A list of all currently active story arc IDs.
        /// </summary>
        public List<string> ActiveStoryArcs => _storyArcManager?.GetActiveStoryArcIds();

        /// <summary>
        /// The ID of the currently displayed event.
        /// </summary>
        public string CurrentEventId => _currentEvent?.name ?? "";
        IEnumerable<string> ISaveDataProvider.ActiveStoryArcs => ActiveStoryArcs;
        #endregion

        //- Private State
        private ISimulationManager _simulation;
        private IChoiceEngine _choice;
        private IUIManager _uiManager;
        private IResourceManager _resourceManager;
        private ISaveManager _saveManager;
        private INarrativeVariableManager _narrativeVariableManager;

        private Dictionary<string, float> _resources = new();
        private HashSet<string> _flags = new();
        private Dictionary<string, float> _relationships = new();

        private SimEventSO _currentEvent;
        private List<SimEventSO> _currentRandomEvents = new();
        private int _randomEventIndex = 0;
        private int _currentDay;
        private string _slotName;
        private string _saveName;

        private StoryArcManager _storyArcManager;
        private NarrativeVariableManager _variableManager = new();
        private StoryStackSO.StoryStackRuntime _storyStack; // Per-run runtime (not serialized)


        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton pattern implementation.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }

            // Resolve system dependencies, finding them in the scene if not explicitly assigned.
            _simulation = _simulationSource as ISimulationManager ?? UEObject.FindFirstObjectByType<SimulationManager>();
            _uiManager = _uiManagerSource as IUIManager ?? UEObject.FindFirstObjectByType<UIManager>();
            _choice = _choiceSource as IChoiceEngine ?? UEObject.FindFirstObjectByType<ChoiceEngine>();
            _variableManager = new NarrativeVariableManager();
            _narrativeVariableManager = _variableManager;
            _resourceManager = _resourceManagerSource as IResourceManager ?? UEObject.FindFirstObjectByType<GlobalResourcesManager>();
            _saveManager = _saveManagerSource as ISaveManager ?? UEObject.FindFirstObjectByType<SaveManager>();

            _storyArcManager = new StoryArcManager(GameData.Instance);


        }

        private void OnEnable() => SubscribeToEvents();
        private void OnDisable() => UnsubscribeFromEvents();

        private void Start()
        {
            _uiManager.SetNextDayButtonVisible(false);
            InitializeResourcesFromGameData();

            // If a save name is set, attempt to load it. Otherwise, start a new game.
            if (!string.IsNullOrEmpty(_saveName) && _saveManager != null && _saveManager.HasSave(_saveName))
                _saveManager.LoadGame(this);
            else
                StartGame();
        }
        #endregion

        #region Event Subscription
        /// <summary>
        /// Subscribes to events from all core systems.
        /// </summary>
        private void SubscribeToEvents()
        {
            try
            {
                if (_simulation != null)
                {
                    _simulation.OnTimeUnitStarted += HandleDayStarted;
                    _simulation.OnTimeUnitEnded += HandleDayEnded;
                    _simulation.OnSimulationCompleted += HandleSimulationCompleted;
                }
                else Log.Warning("[GameController] Simulation manager is null");

                if (_eventSystem != null)
                {
                    _eventSystem.OnMainEventGenerated.AddListener(HandleMainEventGenerated);
                    _eventSystem.OnRandomEventsGenerated.AddListener(HandleRandomEventsGenerated);
                    _eventSystem.OnEventCompleted.AddListener(HandleEventCompleted);
                }
                else Log.Warning("[GameController] Event system is null");

                if (_choice != null)
                {
                    _choice.OnChoicesGenerated.AddListener(HandleChoicesGenerated);
                    _choice.OnOutcomesGenerated.AddListener(HandleOutcomesGenerated);
                }
                else Log.Warning("[GameController] Choice engine is null");

                if (_uiManager != null)
                {
                    _uiManager.OnChoiceSelected += OnChoiceSelected;
                    _uiManager.OnNextDayClicked += AdvanceDay;
                    _uiManager.OnRestartRequested += HandleRestart;
                }
                else Log.Warning("[GameController] UI manager is null");
            }
            catch (Exception e)
            {
                Log.Error($"[GameController] Error subscribing to events: {e.Message}");
            }
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            try
            {
                if (_simulation != null)
                {
                    _simulation.OnTimeUnitStarted -= HandleDayStarted;
                    _simulation.OnTimeUnitEnded -= HandleDayEnded;
                    _simulation.OnSimulationCompleted -= HandleSimulationCompleted;
                }
                if (_eventSystem != null)
                {
                    _eventSystem.OnMainEventGenerated.RemoveListener(HandleMainEventGenerated);
                    _eventSystem.OnRandomEventsGenerated.RemoveListener(HandleRandomEventsGenerated);
                    _eventSystem.OnEventCompleted.RemoveListener(HandleEventCompleted);
                }
                if (_choice != null)
                {
                    _choice.OnChoicesGenerated.RemoveListener(HandleChoicesGenerated);
                    _choice.OnOutcomesGenerated.RemoveListener(HandleOutcomesGenerated);
                }
                if (_uiManager != null)
                {
                    _uiManager.OnChoiceSelected -= OnChoiceSelected;
                    _uiManager.OnNextDayClicked -= AdvanceDay;
                    _uiManager.OnRestartRequested -= HandleRestart;
                }

                OnAchievementAwarded = null;
                OnFeatureUnlocked = null;
                OnFlagAdded = null;
                OnFlagRemoved = null;
            }
            catch (Exception e)
            {
                Log.Error($"[GameController] Error unsubscribing from events: {e.Message}");
            }
        }
        #endregion

        #region Game Flow
        /// <summary>
        /// Starts a new game run, initializing state and triggering the first event.
        /// </summary>
        public void StartGame()
        {
            _isGameRunning = true;
            _uiManager?.HideGameOver();
            InitializeNarrativeVariables();
            _simulation.Initialize();
            _simulation.StartTimeUnit();
            UpdateUI();

            if (GameData.Instance?.StartingEvent != null)
            {
                SimEventSO startEvent = GameData.Instance.StartingEvent;
                Log.Info($"[GameController] Starting with event: {startEvent.Title}");

                // Initialize the Story Stack runtime instance.
                _storyStack = _fallbackStoryStack != null ? new StoryStackSO.StoryStackRuntime(_fallbackStoryStack) : null;

                PresentEvent(startEvent);
            }
            else
            {
                Log.Warning("No StartingEvent assigned in GameData!");
            }
        }

        /// <summary>
        /// Advances the simulation to the next day.
        /// </summary>
        public void AdvanceDay()
        {
            _uiManager.SetNextDayButtonVisible(false);
            _simulation.AdvanceToNextTimeUnit();
            _simulation.StartTimeUnit();
        }

        /// <summary>
        /// Restarts the entire run from day 1.
        /// </summary>
        public void RestartRun()
        {
            Log.Info("[GameController] Restarting run…");

            // Reset all game state.
            _flags = new HashSet<string>();
            _relationships = new Dictionary<string, float>();
            _resources = new Dictionary<string, float>();
            _variableManager = new NarrativeVariableManager();
            _narrativeVariableManager = _variableManager;

            _simulation?.ResetSimulation();

            InitializeResourcesFromGameData();
            InitializeNarrativeVariables();

            _rewind?.Clear();          // if you have the rewind stack
            _eventHistory?.Clear();    // if you keep a short visited-events list
            _storyStack = _fallbackStoryStack != null
                ? new StoryStackSO.StoryStackRuntime(_fallbackStoryStack)
                : null;

            StartGame();

            try { OnRunRestarted?.Invoke(); } catch { /* swallow */ }
        }

        /// <summary>
        /// Ends the current run and produces a summary + shows the Game Over UI.
        /// Single source of truth: all end paths should call this method.
        /// </summary>
        /// <param name="reason">Human-readable reason (e.g., "Campaign completed", "Resource fail").</param>
        /// <param name="endingOverride">Optional specific ending event to display.</param>
        private void EndRun(string reason, SimEventSO endingOverride = null)
        {
            // Decide which ending event to display (override or default).
            SimEventSO endingToShow = endingOverride != null ? endingOverride : _defaultEndingEvent;

            // Build and emit run summary (keeps your existing analytics/save hooks intact).
            var summary = BuildRunSummary(reason, endingToShow);
            Log.Info($"[GameController] RUN ENDED — day {summary.day}, reason: {reason}, ending: {endingToShow?.simEventId ?? "(none)"}");

            // ✅ Single entry point to actually finish the run and show the UI.
            TriggerGameOver(endingToShow);

            // Notify listeners and persist after we’ve decided the ending.
            try { OnRunEnded?.Invoke(summary); } catch { /* no-op */ }
            _saveManager?.SaveGame(this);
        }

        #endregion

        #region Event Handlers
        private void HandleDayStarted(int day)
        {
            Log.Info($"Day {day} started");
            _randomEventIndex = 0;
            _currentRandomEvents.Clear();

            // Autosave at the start of each day.
            _saveManager?.SaveGame(this);

            CheckAndMaybeEndRunAfterStateChanges();
        }

        private void HandleDayEnded(int day)
        {
            Log.Info($"Day {day} ended");
        }

        private void HandleSimulationCompleted()
        {
            EndRun("Campaign completed", _campaignCompletedEnding);
            Log.Info("Simulation completed");
        }

        private void HandleMainEventGenerated(SimEventSO evt)
        {
            PresentEvent(evt);
        }

        private void HandleRandomEventsGenerated(List<SimEventSO> events)
        {
            _currentRandomEvents = events;
        }

        private void HandleEventCompleted(SimEventSO evt)
        {
            // Try to show the next random event if available.
            if (_currentRandomEvents != null && _randomEventIndex < _currentRandomEvents.Count)
            {
                SimEventSO next = _currentRandomEvents[_randomEventIndex++];
                PresentEvent(next);
            }
            else
            {
                // If no more random events, try to get a filler event from the Story Stack.
                if (_useStoryStackForRandomBeats && TryGetStoryStackNext(out var stackEvt))
                {
                    Log.Info($"[GameController] StoryStack inserted a beat after randoms: {stackEvt?.name}");
                    PresentEvent(stackEvt);
                }
                else
                {
                    // If no other events are available, show the "Next Day" button.
                    _uiManager.SetNextDayButtonVisible(true);
                }
            }
        }

        private void HandleChoicesGenerated(IList<Choice> choices)
        {
            _uiManager.DisplayChoices(choices);
        }

        private void HandleOutcomesGenerated(IList<Outcome> outcomes)
        {
            ProcessOutcomes(outcomes);
            if (CheckAndMaybeEndRunAfterStateChanges()) return; // Stop if the game ended.

            _eventSystem.CompleteEvent(_currentEvent);

            // Check if any story arcs were triggered by the state change.
            var triggeredEvent = _storyArcManager.CheckAndActivateImmediateStoryArc(
                _simulation.CurrentTimeUnit,
                _flags,
                new HashSet<SimEventSO>(_eventSystem.CompletedEvents)
            );

            if (triggeredEvent != null)
            {
                Log.Info($"[GameController] Triggered Story Arc Event: {triggeredEvent.name}");
                PresentEvent(triggeredEvent);
                _saveManager?.SaveGame(this);
                UpdateUI();
                return;
            }

            _saveManager?.SaveGame(this);
            UpdateUI();
        }

        private void OnChoiceSelected(Choice choice)
        {
            PushRewindSnapshot();
            SimEventSO nextEvent = _choice.ProcessChoice(choice, _simulation);

            if (nextEvent != null)
            {
                // The choice directly leads to another event.
                Log.Info($"Triggered next event directly from choice: {nextEvent.name}");
                PresentEvent(nextEvent);
            }
            else if (choice.GoToNextDay)
            {
                // The choice ends the current day.
                Log.Info("Choice triggered end of day.");
                _eventSystem.CompleteEvent(_currentEvent);
            }
            else
            {
                // No explicit next event, try the Story Stack as a fallback.
                if (_useStoryStackOnNoNextEvent && TryGetStoryStackNext(out var stackEvt))
                {
                    Log.Info($"[GameController] StoryStack provided next event: {stackEvt?.name}");
                    PresentEvent(stackEvt);
                    return;
                }

                // If all else fails, complete the event and wait for the player to advance the day.
                _eventSystem.CompleteEvent(_currentEvent);
            }
        }

        private void HandleRestart()
        {
            Log.Info("[GameController] Restart requested");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        private void HandleQuit()
        {
            Log.Info("[GameController] Quit requested");
            // W edytorze:
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
        }

        private void HandleGameOver()
        {
            Log.Info("[GameController] Game Over requested");
            _uiManager.ShowGameOver("Game Over", "You have failed your mission.");
        }

        #endregion

        #region State Management
        /// <summary>
        /// Sets the current player character.
        /// </summary>
        public void SetPlayerActor(ActorSO actor)
        {
            _playerActor = actor;
            Log.Info($"[GameController] Player actor set to: {_playerActor?.ActorName ?? "null"}");
        }

        /// <summary>
        /// Checks game over conditions and ends the run if necessary.
        /// </summary>
        /// <returns>True if the run was ended, false otherwise.</returns>
        private bool CheckAndMaybeEndRunAfterStateChanges()
        {
            // Check for resource-based game over conditions.
            var (ko, reason) = CheckResourceGameOver();
            if (ko)
            {
                // Check for a specific ending defined in the new ResourceFailRules.
                if (TryResolvePerResourceEnding(out var perResourceEnding, out var reasonOverride))
                {
                    EndRun(reasonOverride ?? reason, perResourceEnding ?? GetDefaultGameOverEnding());
                    return true;
                }

                // Fallback to the default game over event.
                EndRun(reason, GetDefaultGameOverEnding());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Applies a list of outcomes to the current game state.
        /// </summary>
        private void ProcessOutcomes(IList<Outcome> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                Log.Info("[GameController] No outcomes to process.");
                return;
            }

            int processed = 0;
            foreach (var outcome in outcomes)
            {
                if (outcome == null) continue;
                try
                {
                    // The Outcome ScriptableObject contains its own logic for applying its effects.
                    outcome.Apply(_simulation);
                    processed++;
                }
                catch (Exception ex)
                {
                    Log.Error($"[GameController] Outcome processing error: {ex.Message}");
                }
            }

            if (processed > 0)
            {
                _uiManager?.UpdateResources(_resources);
                Log.Info($"[GameController] Processed {processed} outcomes.");
            }
        }

        /// <summary>
        /// Adds a flag and fires OnFlagAdded if the flag was not already present.
        /// Single source of truth for flag mutation — all code paths go through here.
        /// </summary>
        public void AddFlag(string flag)
        {
            if (_flags.Add(flag))
                OnFlagAdded?.Invoke(flag);
        }

        /// <summary>
        /// Removes a flag and fires OnFlagRemoved if it was present.
        /// </summary>
        public void RemoveFlag(string flag)
        {
            if (_flags.Remove(flag))
                OnFlagRemoved?.Invoke(flag);
        }

        /// <summary>Returns all currently active flags.</summary>
        public IEnumerable<string> GetCurrentFlags() => _flags;

        /// <summary>
        /// Checks if a given flag is currently set.
        /// </summary>
        public bool HasFlag(string flagId) => _flags.Contains(flagId);

        /// <summary>
        /// Modifies a resource by a given amount, clamping it within its min/max values.
        /// </summary>
        public void ModifyResource(string id, float amount)
        {
            if (string.IsNullOrEmpty(id))
            {
                Log.Warning("[GameController] ModifyResource called with empty id.");
                return;
            }

            float before = Resources.ContainsKey(id) ? Resources[id] : 0f;
            float after = before + amount;

            var def = GameData.Instance?.Resources?.FirstOrDefault(r => r.Id == id);
            if (def != null)
                after = Mathf.Clamp(after, def.MinValue, def.MaxValue);

            Resources[id] = after;

            Log.Info($"[GameController] Resource '{id}' {before} -> {after} (Δ {amount})");
            _uiManager.UpdateResources(Resources);
        }

        /// <summary>
        /// Sets the value of a relationship between two actors.
        /// </summary>
        public void SetRelationship(string relationshipId, ActorSO source, ActorSO target, float value)
        {
            if (source != null && target != null && GameData.Instance != null)
            {
                var rel = GameData.Instance.Relationships
                    .FirstOrDefault(r => r.Source == source && r.Target == target);

                if (rel != null)
                {
                    rel.Strength = value;
                    Log.Info($"[GameController] Relationship set: {source.Title} → {target.Title} = {value}");
                }
                else
                {
                    var newRel = new Relationship
                    {
                        Source = source,
                        Target = target,
                        Strength = value,
                        RelationshipType = "Undefined"
                    };
                    GameData.Instance.AddRelationship(newRel);
                    Log.Info($"[GameController] Relationship created: {source.Title} → {target.Title} = {value}");
                }

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(GameData.Instance);
#endif
                return;
            }

            if (!string.IsNullOrEmpty(relationshipId))
            {
                _relationships[relationshipId] = value;
                Log.Info($"[GameController] Relationship(id='{relationshipId}') set to {value}");
            }
        }

        /// <summary>
        /// Modifies the value of a relationship between two actors.
        /// </summary>
        public void ModifyRelationship(string relationshipId, ActorSO source, ActorSO target, float delta)
        {
            if (source != null && target != null && GameData.Instance != null)
            {
                var rel = GameData.Instance.Relationships
                    .FirstOrDefault(r => r.Source == source && r.Target == target);

                if (rel != null)
                {
                    rel.Strength += delta;
                    Log.Info($"[GameController] Relationship modified: {source.Title} → {target.Title} += {delta} → {rel.Strength}");
                }
                else
                {
                    var newRel = new Relationship
                    {
                        Source = source,
                        Target = target,
                        Strength = delta,
                        RelationshipType = "Undefined"
                    };
                    GameData.Instance.AddRelationship(newRel);
                    Log.Info($"[GameController] Relationship created (delta): {source.Title} → {target.Title} = {delta}");
                }

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(GameData.Instance);
#endif
                return;
            }

            if (!string.IsNullOrEmpty(relationshipId))
            {
                if (!_relationships.TryGetValue(relationshipId, out var current))
                    current = 0f;

                _relationships[relationshipId] = current + delta;
                Log.Info($"[GameController] Relationship(id='{relationshipId}') modified by {delta} → {_relationships[relationshipId]}");
            }
        }

        /// <summary>
        /// Gets the value of a float narrative variable.
        /// </summary>
        public float GetNarrativeVariable(string id) => _variableManager.GetValue(id);

        /// <summary>
        /// Sets the value of a float narrative variable.
        /// </summary>
        public void SetNarrativeVariable(string id, float value) => _variableManager.SetValue(id, value);

        /// <summary>
        /// Modifies the value of a float narrative variable.
        /// </summary>
        public void ModifyNarrativeVariable(string id, float delta) => _variableManager.ModifyVariable(id, delta);

        /// <summary>
        /// Gets the value of a boolean narrative variable.
        /// </summary>
        public bool GetNarrativeBool(string id) => _variableManager.GetBool(id);

        /// <summary>
        /// Sets the value of a boolean narrative variable.
        /// </summary>
        public void SetNarrativeBool(string id, bool value) => _variableManager.SetBool(id, value);

        /// <summary>
        /// Awards an achievement and adds a corresponding flag.
        /// </summary>
        public void AwardAchievement(string achievementId)
        {
            Log.Info($"[GameController] Achievement unlocked: {achievementId}");
            AddFlag($"achievement_{achievementId}");
            OnAchievementAwarded?.Invoke(achievementId);
        }

        /// <summary>
        /// Unlocks a feature and adds a corresponding flag.
        /// </summary>
        public void UnlockFeature(string featureId)
        {
            Log.Info($"[GameController] Feature unlocked: {featureId}");
            AddFlag($"feature_{featureId}");
            OnFeatureUnlocked?.Invoke(featureId);
        }

        public event System.Action<SimEventSO> OnGameOver;

        /// <summary>
        /// Centralized Game Over trigger. Guards against re-entry, resolves title/body,
        /// and delegates to the UI manager to show the Game Over panel.
        /// </summary>
        private void TriggerGameOver(SimEventSO endingEvent)
        {
            if (!_isGameRunning) return;
            _isGameRunning = false;
            
            Log.Info($"[GameController] TriggerGameOver: {endingEvent?.name}");
            
            // ✅ ADDED: Invoke the event for any listeners
            OnGameOver?.Invoke(endingEvent);
            
            string title = (endingEvent != null && !string.IsNullOrEmpty(endingEvent.simEventTitle))
                ? endingEvent.simEventTitle
                : "Game Over";
                
            string body = (endingEvent != null && !string.IsNullOrEmpty(endingEvent.simEventDescription))
                ? endingEvent.simEventDescription
                : "The run has ended.";
                
            _uiManager?.ShowGameOver(title, body);
        }




        #endregion

        #region Save/Load
        /// <summary>
        /// Applies loaded save data to the GameController's state.
        /// </summary>
        public void ApplySaveData(
            int day,
            HashSet<string> flags,
            Dictionary<string, float> resources,
            Dictionary<string, float> relationships,
            Dictionary<string, float> narrativeVariables,
            Dictionary<string, bool> narrativeBooleans,
            List<string> activeStoryArcs,
            string currentEventId,
            string slotName,
            string saveName)
        {
            _currentDay = day;
            _flags = flags ?? new HashSet<string>();

            _resources = resources != null ? new Dictionary<string, float>(resources) : new Dictionary<string, float>();
            _resourceManager?.Apply(_resources);

            _relationships = relationships != null
                ? new Dictionary<string, float>(relationships)
                : new Dictionary<string, float>();

            _narrativeVariableManager?.Apply(narrativeVariables, narrativeBooleans);

            _slotName = slotName;
            _saveName = saveName;

            _currentEvent = GameData.Instance.Events.FirstOrDefault(e => e.name == currentEventId);

            _uiManager?.UpdateResources(_resources);
        }
        #endregion

        #region UI and Previews
        /// <summary>
        /// Updates the main UI elements with the current game state.
        /// </summary>
        private void UpdateUI()
        {
            _uiManager.SetDayText($"{_simulation.TimeUnitName} {_simulation.CurrentTimeUnit} of {_simulation.MaxTimeUnits}");
            _uiManager.UpdateResources(_resources);
        }

        /// <summary>
        /// Generates a list of human-readable strings to preview the outcomes of a choice.
        /// </summary>
        /// <param name="choice">The choice to preview.</param>
        /// <returns>A list of preview items for the UI.</returns>
        public List<OutcomePreviewItem> GetChoicePreview(Choice choice)
        {
            var list = new List<OutcomePreviewItem>();
            if (choice == null) return list;

            foreach (var outcome in choice.Outcomes)
            {
                if (outcome == null) continue;

                var actions = outcome.Actions;
                if (actions != null && actions.Count > 0)
                {
                    // Use the new multi-action system if available.
                    foreach (var a in actions)
                        TryAddPreviewFromAction(a, list);
                }
                else
                {
                    // Fallback to the legacy single-action system.
                    TryAddPreviewFromLegacy(outcome, list);
                }
            }

            // Add previews for navigation outcomes.
            if (choice.GoToNextDay)
            {
                list.Add(new OutcomePreviewItem
                {
                    label = "End of day",
                    id = "goto_next_day",
                    probability = 1f,
                    category = PreviewCategory.Event,
                });
            }
            else if (choice.NextEvent != null)
            {
                string title = string.IsNullOrWhiteSpace(choice.NextEvent.simEventTitle)
                    ? choice.NextEvent.Title : choice.NextEvent.simEventTitle;

                list.Add(new OutcomePreviewItem
                {
                    label = $"Leads to: {title}",
                    id = choice.NextEvent.simEventId,
                    probability = 1f,
                    category = PreviewCategory.Event,
                });
            }

            return list;
        }

        /// <summary>
        /// Tries to generate a preview item from a modern OutcomeAction.
        /// </summary>
        private void TryAddPreviewFromAction(OutcomeAction a, List<OutcomePreviewItem> list)
        {
            if (a == null) return;
            float p = Mathf.Clamp01(a.Probability);

            switch (a.Type)
            {
                case OutcomeType.ModifyResource:
                    {
                        string id = a.TargetResource != null ? a.TargetResource.Id : a.TargetId;
                        if (string.IsNullOrEmpty(id)) break;

                        var (nice, icon) = ResolveResourceLabelAndIcon(id);
                        string sign = a.Value >= 0 ? "+" : "";
                        list.Add(new OutcomePreviewItem
                        {
                            label = $"{nice} {sign}{a.Value} ({Mathf.RoundToInt(p * 100)}%)",
                            id = id,
                            delta = a.Value,
                            probability = p,
                            category = PreviewCategory.Resource,
                            icon = icon
                        });
                        break;
                    }
                    // ... other cases ...
            }
        }

        /// <summary>
        /// Tries to generate a preview item from a legacy Outcome.
        /// </summary>
        private void TryAddPreviewFromLegacy(Outcome o, List<OutcomePreviewItem> list)
        {
            float p = Mathf.Clamp01(o.Probability);
            switch (o.Type)
            {
                case OutcomeType.ModifyResource:
                    {
                        string id = !string.IsNullOrEmpty(o.TargetId) ? o.TargetId : o.TargetResourceRef?.Id;
                        if (string.IsNullOrEmpty(id)) break;
                        var (nice, icon) = ResolveResourceLabelAndIcon(id);
                        string sign = o.Value >= 0 ? "+" : "";
                        list.Add(new OutcomePreviewItem
                        {
                            label = $"{nice} {sign}{o.Value} ({Mathf.RoundToInt(p * 100)}%)",
                            id = id,
                            delta = o.Value,
                            probability = p,
                            category = PreviewCategory.Resource,
                            icon = icon
                        });
                        break;
                    }
                    // ... other cases ...
            }
        }

        /// <summary>
        /// Finds the display name and icon for a resource from GameData.
        /// </summary>
        private (string label, Sprite icon) ResolveResourceLabelAndIcon(string id)
        {
            var gd = GameData.Instance;
            if (gd != null && gd.Resources != null)
            {
                foreach (var r in gd.Resources)
                {
                    if (r != null && r.Id == id)
                        return (string.IsNullOrWhiteSpace(r.DisplayName) ? r.Id : r.DisplayName, r.Icon);
                }
            }
            return (id, null);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Initializes resources based on the definitions in GameData.
        /// </summary>
        private void InitializeResourcesFromGameData()
        {
            if (GameData.Instance == null) return;

            foreach (var res in GameData.Instance.Resources)
            {
                if (!_resources.ContainsKey(res.Id))
                    _resources.Add(res.Id, res.InitialValue);

                _resourceManager?.SetValue(res.Id, res.InitialValue);
            }

            _uiManager.CreateResourceDisplays(GameData.Instance.Resources.ToList());
            _uiManager.UpdateResources(_resources);
        }

        /// <summary>
        /// Initializes narrative variables based on the definitions in GameData.
        /// </summary>
        private void InitializeNarrativeVariables()
        {
            if (GameData.Instance == null) return;
            _variableManager.Initialize(GameData.Instance.NarrativeVariables);
        }

        /// <summary>
        /// Checks if any resource has fallen below its game over threshold.
        /// </summary>
        private (bool triggered, string reason) CheckResourceGameOver()
        {
            if (_resources == null || _resources.Count == 0) return (false, null);

            HashSet<string> idsToCheck = new HashSet<string>();
            if (_deathResources != null && _deathResources.Count > 0)
            {
                foreach (var id in _deathResources)
                    if (!string.IsNullOrWhiteSpace(id)) idsToCheck.Add(id);
            }
            else
            {
                var defs = GameData.Instance?.Resources;
                if (defs != null)
                    foreach (var r in defs) if (r != null && !string.IsNullOrWhiteSpace(r.Id)) idsToCheck.Add(r.Id);
            }

            foreach (var id in idsToCheck)
            {
                if (!_resources.TryGetValue(id, out var value)) continue;

                float min = 0f;
                var def = GameData.Instance?.Resources?.FirstOrDefault(r => r != null && r.Id == id);
                if (def != null) min = def.MinValue;

                if (_gameOverIfAnyResourceAtMin && value <= min)
                    return (true, $"Resource reached minimum: {id}");
                if (_gameOverIfAnyResourceAtOrBelowZero && value <= 0f)
                    return (true, $"Resource reached zero: {id}");
            }

            return (false, null);
        }

        /// <summary>
        /// Creates a summary of the completed run.
        /// </summary>
        private RunSummary BuildRunSummary(string reason, SimEventSO ending)
        {
            var summary = new RunSummary
            {
                day = _simulation != null ? _simulation.CurrentTimeUnit : _currentDay,
                reason = reason,
                endingEventId = ending != null ? ending.simEventId : null,
                resources = new Dictionary<string, float>(_resources ?? new Dictionary<string, float>()),
                flags = new List<string>(_flags ?? new HashSet<string>()),
                relationships = new Dictionary<string, float>(_relationships ?? new Dictionary<string, float>()),
                variables = NarrativeVariables,
            };
            return summary;
        }

        /// <summary>
        /// Resolves which ending to use based on the ResourceFailRules in GameData.
        /// </summary>
        /// <returns>True if a rule was tripped, even if the resulting ending is null.</returns>
        private bool TryResolvePerResourceEnding(out SimEventSO ending, out string reasonOverride)
        {
            ending = null;
            reasonOverride = null;

            var gd = GameData.Instance;
            if (gd == null || !gd.EnableResourceFailStates) return false;

            var rules = gd.ResourceFailRules;
            if (rules == null || rules.Count == 0) return false;

            foreach (var rule in rules)
            {
                if (rule == null || !rule.Enabled || rule.Resource == null) continue;

                var resId = rule.Resource.Id;
                if (string.IsNullOrWhiteSpace(resId)) continue;

                if (_resources != null && _resources.TryGetValue(resId, out var current))
                {
                    if (current <= rule.Threshold)
                    {
                        ending = rule.SpecificEnding; // Can be null, fallback will be used later.
                        reasonOverride = string.IsNullOrWhiteSpace(rule.MessageOverride)
                            ? $"Resource '{resId}' reached threshold {rule.Threshold}"
                            : rule.MessageOverride;

                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the default game over ending, checking GameData first, then the local field.
        /// </summary>
        private SimEventSO GetDefaultGameOverEnding()
        {
            var gd = GameData.Instance;
            if (gd != null && gd.DefaultGameOverEvent != null)
                return gd.DefaultGameOverEvent;
            return _defaultEndingEvent;
        }

        /// <summary>
        /// A centralized method to present a new event to the player.
        /// </summary>
        private void PresentEvent(SimEventSO evt)
        {
            if (evt == null) return;
            if (evt != null && evt.Type == SimEventSO.EventType.Ending)
            {
                EndRun("Ending event", evt);
                return; // stop normal flow; do not present choices or advance
            }

            _currentEvent = evt;
            if (_eventHistory.Count == 0 || _eventHistory[_eventHistory.Count - 1] != evt.name)
                _eventHistory.Add(evt.name);
            _uiManager.DisplayEvent(evt);
            _eventSystem.PresentEvent(evt);
            _choice.PresentEvent(evt, _simulation.CurrentTimeUnit, _flags);

            _storyStack?.NotifyOccurred(evt);
        }

        /// <summary>
        /// Gets the next event from the story stack if available.
        /// </summary>
        private bool TryGetStoryStackNext(out SimEventSO next)
        {
            next = null;
            if (_storyStack == null) return false;
            next = _storyStack.Next(new List<string>(_flags));
            return next != null;
        }
        #endregion

    }
}