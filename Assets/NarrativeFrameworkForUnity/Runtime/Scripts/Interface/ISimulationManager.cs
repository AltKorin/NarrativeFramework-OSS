using System;
using System.Collections.Generic;
using SimulationGame;

namespace SimulationGame.Interfaces
{
    /// <summary>
    /// Controls the simulation's time progression (days / phases / turns).
    /// Consumers that only need to tick time should depend on this interface.
    /// </summary>
    public interface ITimeSimulation
    {
        int CurrentTimeUnit { get; }
        int MaxTimeUnits { get; }
        string TimeUnitName { get; }
        bool IsLastTimeUnit { get; }
        int RemainingTimeUnits { get; }
        float Progress { get; }

        event Action<int> OnTimeUnitStarted;
        event Action<int> OnTimeUnitEnded;
        event Action<int> OnFinalTimeUnitCompleted;
        event Action OnSimulationCompleted;

        void Initialize();
        void StartTimeUnit();
        bool AdvanceToNextTimeUnit();
        void SetCurrentTimeUnit(int timeUnit);
        bool SetTimeUnit(int timeUnit);
        void ResetSimulation();
        void ConfigureSimulation(int maxUnits, string unitName = null, int startingUnit = 1);
        int GetCurrentDay();
    }

    /// <summary>
    /// Mutates narrative game state: flags, resources, variables, relationships.
    /// This is the primary extension point for the Asset Store — implement IGameContext
    /// in your existing game systems and pass it to BaseOutcome.Execute() to plug
    /// NarrativeFramework outcomes into any codebase without depending on GameController.
    /// </summary>
    public interface IGameContext
    {
        void AddFlag(string flag);
        void RemoveFlag(string flag);
        bool HasFlag(string flag);
        IEnumerable<string> GetCurrentFlags();

        void ModifyResource(string resourceId, float amount);
        void Log(string message);
        void TriggerEventById(string eventId);

        void SetRelationship(string targetId, ActorSO source, ActorSO target, float value);
        void ModifyRelationship(string targetId, ActorSO source, ActorSO target, float delta);

        void SetNarrativeVariable(string id, float value);
        void ModifyNarrativeVariable(string id, float delta);
        void SetNarrativeBool(string id, bool value);

        void AwardAchievement(string achievementId);
        void UnlockFeature(string featureId);
        void SetPlayerActor(ActorSO actor);
        void TriggerStoryArc(StoryArcSO arc);
    }

    /// <summary>
    /// Full simulation manager — combines time control with game-state mutation.
    /// Backward-compatible: any ISimulationManager can be used wherever IGameContext
    /// or ITimeSimulation is expected.
    /// </summary>
    public interface ISimulationManager : ITimeSimulation, IGameContext { }
}
