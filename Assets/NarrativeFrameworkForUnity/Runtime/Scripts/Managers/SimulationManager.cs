using System;
using System.Collections.Generic;
using UnityEngine;
using SimulationGame.Interfaces;

namespace SimulationGame
{
    public class SimulationManager : MonoBehaviour, ISimulationManager
    {
        [Header("Simulation Settings")]
        [SerializeField] private int _maxTimeUnits = 30;
        [SerializeField] private int _startingTimeUnit = 1;
        [SerializeField] private string _timeUnitName = "Day";

        [Header("Game Data")]
        [SerializeField] private EventSystem _eventSystem;
        [SerializeField] private GameData _gameData;
        [SerializeField] private GameController _gameController;
        [SerializeField] private MonoBehaviour _resourceManagerSource;

        private IResourceManager _resourceManager;

        private int _currentTimeUnit;

        // Events
        public event Action<int> OnTimeUnitStarted;
        public event Action<int> OnTimeUnitEnded;
        public event Action<int> OnFinalTimeUnitCompleted;
        public event Action OnSimulationCompleted;

        public int CurrentTimeUnit => _currentTimeUnit;
        public int MaxTimeUnits => _maxTimeUnits;
        public string TimeUnitName => _timeUnitName;
        public bool IsLastTimeUnit => _currentTimeUnit >= _maxTimeUnits;
        public int RemainingTimeUnits => _maxTimeUnits - _currentTimeUnit;
        public float Progress => (float)(_currentTimeUnit - 1) / (_maxTimeUnits - 1);

        private void Awake()
        {
            // Ładuj ResourceManager jako interfejs
            _resourceManager = _resourceManagerSource as IResourceManager;
            if (_resourceManager == null)
                UnityEngine.Debug.LogError("[SimulationManager] ResourceManager must implement IResourceManager.");

            if (_eventSystem == null)
                UnityEngine.Debug.LogError("[SimulationManager] EventSystem is not assigned.");

            if (_gameData == null)
                UnityEngine.Debug.LogError("[SimulationManager] GameData is not assigned.");

            Initialize();
        }

        public void Initialize()
        {
            _currentTimeUnit = _startingTimeUnit;
            UnityEngine.Debug.Log($"[SimulationManager] Initialized at {_timeUnitName} {_currentTimeUnit}");
        }

        public void StartTimeUnit()
        {
            UnityEngine.Debug.Log($"[{_timeUnitName}] {_currentTimeUnit} started");
            OnTimeUnitStarted?.Invoke(_currentTimeUnit);
        }

        public bool AdvanceToNextTimeUnit()
        {
            UnityEngine.Debug.Log($"[{_timeUnitName}] {_currentTimeUnit} ended");
            OnTimeUnitEnded?.Invoke(_currentTimeUnit);

            if (IsLastTimeUnit)
            {
                UnityEngine.Debug.Log("[SimulationManager] Simulation completed");
                OnFinalTimeUnitCompleted?.Invoke(_currentTimeUnit);
                OnSimulationCompleted?.Invoke();
                return false;
            }

            _currentTimeUnit++;
            return true;
        }

        public bool SetTimeUnit(int timeUnit)
        {
            if (timeUnit < 1 || timeUnit > _maxTimeUnits)
            {
                UnityEngine.Debug.LogWarning($"[SimulationManager] Invalid {_timeUnitName.ToLower()}: {timeUnit}");
                return false;
            }

            _currentTimeUnit = timeUnit;
            UnityEngine.Debug.Log($"[{_timeUnitName}] set to {_currentTimeUnit}");
            return true;
        }

        public void ResetSimulation()
        {
            _currentTimeUnit = _startingTimeUnit;
            UnityEngine.Debug.Log($"[SimulationManager] Reset to {_timeUnitName.ToLower()} {_currentTimeUnit}");
        }

        public void ConfigureSimulation(int maxUnits, string unitName = null, int startingUnit = 1)
        {
            _maxTimeUnits = Mathf.Max(1, maxUnits);
            _timeUnitName = string.IsNullOrWhiteSpace(unitName) ? _timeUnitName : unitName;
            _startingTimeUnit = Mathf.Clamp(startingUnit, 1, _maxTimeUnits);
            _currentTimeUnit = _startingTimeUnit;

            UnityEngine.Debug.Log($"[SimulationManager] Configured: {_maxTimeUnits} {_timeUnitName}s starting at {_startingTimeUnit}");
        }

        public IEnumerable<string> GetCurrentFlags() =>
            _gameController != null ? _gameController.GetCurrentFlags() : System.Array.Empty<string>();

        public int GetCurrentDay() => _currentTimeUnit;

        public bool HasFlag(string flag) => _gameController?.HasFlag(flag) ?? false;

        public void AddFlag(string flag) => _gameController?.AddFlag(flag);

        public void RemoveFlag(string flag) => _gameController?.RemoveFlag(flag);

        public void TriggerEventById(string targetId)
        {
            if (_eventSystem == null)
            {
                UnityEngine.Debug.LogError("[SimulationManager] EventSystem is null.");
                return;
            }

            _eventSystem.TriggerEventById(targetId);
        }

        public void ModifyResource(string resourceId, float amount)
        {
            _gameController.ModifyResource(resourceId, amount);
        }

        public void Log(string message)
        {
            UnityEngine.Debug.Log($"[Simulation Log] {message}");
        }

        public void SetCurrentTimeUnit(int timeUnit)
        {
            _currentTimeUnit = timeUnit;
        }
        public void AwardAchievement(string achievementId)
        {
            _gameController?.AwardAchievement(achievementId);
        }

        public void UnlockFeature(string featureId)
        {
            _gameController?.UnlockFeature(featureId);
        }

        public void SetPlayerActor(ActorSO actor)
        {
            _gameController?.SetPlayerActor(actor);
        }

        public void SetRelationship(string targetId, ActorSO source, ActorSO target, float value)
        {
            if (_gameController == null) return;
            _gameController.SetRelationship(targetId, source, target, value);
        }

        public void ModifyRelationship(string targetId, ActorSO source, ActorSO target, float delta)
        {
            if (_gameController == null) return;
            _gameController.ModifyRelationship(targetId, source, target, delta);
        }

        public void SetNarrativeVariable(string id, float value)
        {
            _gameController?.SetNarrativeVariable(id, value);
        }

        public void ModifyNarrativeVariable(string id, float delta)
        {
            _gameController?.ModifyNarrativeVariable(id, delta);
        }

        public void SetNarrativeBool(string id, bool value)
        {
            _gameController?.SetNarrativeBool(id, value);
        }

        public void TriggerStoryArc(StoryArcSO arc)
        {
            _gameController?.TriggerStoryArc(arc);
        }


    }


}
