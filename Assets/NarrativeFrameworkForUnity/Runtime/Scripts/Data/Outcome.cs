using ISimulationManager = SimulationGame.Interfaces.ISimulationManager;
using IGameContext = SimulationGame.Interfaces.IGameContext;
using UnityEngine;
using SimulationGame.DebugTools;
using System.Collections.Generic;
using System.Linq;

namespace SimulationGame
{
    public enum OutcomeType
    {
        CustomOutcome,
        SetPlayerActor,
        TriggerStoryArc,
        AddFlag,
        RemoveFlag,
        ModifyResource,
        TriggerEvent,
        SetRelationship,
        ModifyNarrativeVariable,
        SetNarrativeBoolTrue,
        SetNarrativeBoolFalse,
        AwardAchievement,
        UnlockFeature
    }

    /// <summary>
    /// Backwards-compatible Outcome (legacy single-type + optional multi-action).
    /// - Legacy mode: works exactly as before, when Actions list is empty.
    /// - Multi-action mode: when Actions has at least one item, only actions are executed (legacy fields are ignored).
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "Outcome", menuName = "Narrative Framework/Outcome")]
    public class Outcome : NarrativeObject
    {
        /*───────────────────────────────────────────────────────────────────────────
         * Legacy (single-type) fields — preserved for backward compatibility
         *───────────────────────────────────────────────────────────────────────────*/
        [SerializeField] public OutcomeType _type;
        [SerializeField] private string _targetEventId;
        [SerializeField] private float _value;
        [SerializeField] private float _probability = 1f;                // 0..1
        [SerializeField] private string _outcomeDescription;

        // Linked ScriptableObjects (legacy)
        [SerializeField] private SimEventSO _targetEvent;
        [SerializeField] private Resource _targetResource;
        [SerializeField] private NarrativeVariableSO _targetVariable;
        [SerializeField] private ActorSO _sourceActor;
        [SerializeField] private ActorSO _targetActor;
        [SerializeField] private ActorSO _playerActor;                    // legacy fallback
        [SerializeField] private StoryArcSO _storyArc;

        [SerializeField] private BaseOutcome _customLogic;
        [SerializeField] private List<string> _flagsToAdd = new();
        [SerializeField] private List<string> _flagsToRemove = new();

        /*───────────────────────────────────────────────────────────────────────────
         * New multi-action mode — optional, non-breaking
         *───────────────────────────────────────────────────────────────────────────*/
        [SerializeField] private List<OutcomeAction> _actions = new();    // when not empty, used instead of legacy fields

        /*───────────────────────────────────────────────────────────────────────────
         * Public accessors (legacy API stays intact)
         *───────────────────────────────────────────────────────────────────────────*/
        public OutcomeType Type => _type;

        /// <summary>
        /// Target identifier (legacy convenience): tries to return a meaningful ID depending on legacy Type.
        /// </summary>
        public string TargetId
        {
            get
            {
                // keep legacy behavior
                if (_type == OutcomeType.ModifyNarrativeVariable && _targetVariable != null)
                    return _targetVariable.Id;
                if (_targetResource != null)
                    return _targetResource.Id;
                return _targetEventId;
            }
        }

        public string TargetEventId => _targetEventId;
        public float Value => _value;

        /// <summary>Legacy outcome probability (0..1). Multi-action items have their own probabilities.</summary>
        public float Probability
        {
            get => _probability;
            set => _probability = value;
        }

        public string OutcomeDescription => _outcomeDescription;
        public IReadOnlyList<string> FlagsToAdd => _flagsToAdd;
        public IReadOnlyList<string> FlagsToRemove => _flagsToRemove;

        // Expose refs (some views expect them)
        public ActorSO TargetActorRef => _targetActor;
        public StoryArcSO StoryArcRef => _storyArc;
        public Resource TargetResourceRef => _targetResource;
        public NarrativeVariableSO TargetVariableRef => _targetVariable;

        /// <summary>
        /// Actor to be used by SetPlayerActor legacy outcomes.
        /// Prefer the explicit _targetActor; fall back to legacy _playerActor if present.
        /// </summary>
        public ActorSO TargetActor => _targetActor != null ? _targetActor : _playerActor;

        /// <summary>Story arc to be triggered by legacy TriggerStoryArc outcomes.</summary>
        public StoryArcSO StoryArc => _storyArc;

        /// <summary>New multi-action API (read-only list for external code).</summary>
        public IReadOnlyList<OutcomeAction> Actions => _actions;

        /*───────────────────────────────────────────────────────────────────────────
         * Legacy initializer & mutators — preserved
         *───────────────────────────────────────────────────────────────────────────*/
        public void Initialize(OutcomeType type, string targetId, float value, float probability, string outcomeDescription)
        {
            _type = type;
            _targetEventId = targetId; // For flags, events, etc.
            _value = value;
            _probability = probability;
            _outcomeDescription = outcomeDescription;
        }

        // Methods to set linked ScriptableObjects after creation (legacy)
        public void SetTargetEvent(SimEventSO eventSO) => _targetEvent = eventSO;
        public void SetTargetResource(Resource resourceSO) => _targetResource = resourceSO;
        public void SetTargetVariable(NarrativeVariableSO variableSO) => _targetVariable = variableSO;
        public void SetSourceActor(ActorSO actorSO) => _sourceActor = actorSO;
        public void SetTargetActor(ActorSO actorSO) => _targetActor = actorSO;
        public void SetCustomLogic(BaseOutcome customLogicSO) => _customLogic = customLogicSO;
        public void AddFlagToAdd(string flag) => _flagsToAdd.Add(flag);
        public void AddFlagToRemove(string flag) => _flagsToRemove.Add(flag);

        /*───────────────────────────────────────────────────────────────────────────
         * Execution
         *───────────────────────────────────────────────────────────────────────────*/
        public bool ShouldOccur() => UnityEngine.Random.value <= Probability;

        public virtual void Apply(ISimulationManager simulationManager)
        {
            // NEW MODE: use actions when present (does not break old assets)
            if (_actions != null && _actions.Count > 0)
            {
                foreach (var action in _actions)
                {
                    if (action == null) continue;
                    if (!action.ShouldOccur()) continue;
                    ExecuteAction(simulationManager, action);
                }
                return;
            }

            // LEGACY MODE: preserved behavior
            if (!ShouldOccur()) return;

            switch (Type)
            {
                case OutcomeType.AddFlag:
                    simulationManager.AddFlag(TargetId);
                    break;

                case OutcomeType.RemoveFlag:
                    simulationManager.RemoveFlag(TargetId);
                    break;

                case OutcomeType.ModifyResource:
                    simulationManager.ModifyResource(TargetId, Value);
                    break;

                case OutcomeType.TriggerEvent:
                    simulationManager.TriggerEventById(TargetId);
                    break;

                case OutcomeType.TriggerStoryArc:
                {
                    StoryArcSO arc = _storyArc;
                    if (arc == null && !string.IsNullOrEmpty(TargetId))
                        arc = GameData.Instance.StoryArcs.FirstOrDefault(a => a.Id == TargetId || a.name == TargetId);

                    if (arc != null)
                        simulationManager.TriggerStoryArc(arc);
                    else
                        Log.Warning($"[Outcome] TriggerStoryArc: no StoryArc found for '{TargetId}'.");
                    break;
                }

                case OutcomeType.SetRelationship:
                    if (_sourceActor != null && _targetActor != null)
                        simulationManager.SetRelationship(TargetId, _sourceActor, _targetActor, Value);
                    else
                        Log.Warning("[Outcome] SetRelationship: Source or Target actor not assigned.");
                    break;

                case OutcomeType.ModifyNarrativeVariable:
                    simulationManager.ModifyNarrativeVariable(TargetId, Value);
                    break;

                case OutcomeType.SetNarrativeBoolTrue:
                    simulationManager.SetNarrativeBool(TargetId, true);
                    break;

                case OutcomeType.SetNarrativeBoolFalse:
                    simulationManager.SetNarrativeBool(TargetId, false);
                    break;

                case OutcomeType.AwardAchievement:
                    simulationManager.AwardAchievement(TargetId);
                    break;

                case OutcomeType.UnlockFeature:
                    simulationManager.UnlockFeature(TargetId);
                    break;

                case OutcomeType.CustomOutcome:
                    ApplyCustomLogic(simulationManager, _customLogic, TargetId);
                    break;

                case OutcomeType.SetPlayerActor:
                    if (_targetActor != null)
                        simulationManager.SetPlayerActor(_targetActor);
                    else
                        Log.Warning("[Outcome] SetPlayerActor chosen but TargetActor is null.");
                    break;
            }
        }

        /*───────────────────────────────────────────────────────────────────────────
         * New Action execution — IGameContext is sufficient; no clock needed here
         *───────────────────────────────────────────────────────────────────────────*/
        private void ExecuteAction(IGameContext sim, OutcomeAction a)
        {
            switch (a.Type)
            {
                case OutcomeType.AddFlag:
                    foreach (var f in a.FlagsToAdd) sim.AddFlag(f);
                    break;

                case OutcomeType.RemoveFlag:
                    foreach (var f in a.FlagsToRemove) sim.RemoveFlag(f);
                    break;

                case OutcomeType.ModifyResource:
                {
                    string id = a.TargetResource != null ? a.TargetResource.Id : a.TargetId;
                    if (!string.IsNullOrEmpty(id))
                        sim.ModifyResource(id, a.Value);
                    break;
                }

                case OutcomeType.TriggerEvent:
                {
                    if (a.TargetEvent != null)
                        sim.TriggerEventById(a.TargetEvent.simEventId);
                    else if (!string.IsNullOrEmpty(a.TargetId))
                        sim.TriggerEventById(a.TargetId);
                    break;
                }

                case OutcomeType.TriggerStoryArc:
                {
                    StoryArcSO arc = a.StoryArc;
                    if (arc == null && !string.IsNullOrEmpty(a.TargetId))
                        arc = GameData.Instance.StoryArcs.FirstOrDefault(s => s.Id == a.TargetId || s.name == a.TargetId);
                    if (arc != null)
                        sim.TriggerStoryArc(arc);
                    else
                        Log.Warning($"[OutcomeAction] TriggerStoryArc: no StoryArc found for '{a.TargetId}'.");
                    break;
                }

                case OutcomeType.SetRelationship:
                    if (a.SourceActor != null && a.TargetActor != null)
                        sim.SetRelationship(a.TargetId, a.SourceActor, a.TargetActor, a.Value);
                    else
                        Log.Warning("[OutcomeAction] SetRelationship: Source or Target actor not assigned.");
                    break;

                case OutcomeType.ModifyNarrativeVariable:
                {
                    string id = a.TargetVariable != null ? a.TargetVariable.Id : a.TargetId;
                    if (!string.IsNullOrEmpty(id))
                        sim.ModifyNarrativeVariable(id, a.Value);
                    break;
                }

                case OutcomeType.SetNarrativeBoolTrue:
                    sim.SetNarrativeBool(a.TargetIdOrVariableId, true);
                    break;

                case OutcomeType.SetNarrativeBoolFalse:
                    sim.SetNarrativeBool(a.TargetIdOrVariableId, false);
                    break;

                case OutcomeType.AwardAchievement:
                    sim.AwardAchievement(a.TargetId);
                    break;

                case OutcomeType.UnlockFeature:
                    sim.UnlockFeature(a.TargetId);
                    break;

                case OutcomeType.CustomOutcome:
                    ApplyCustomLogic(sim, a.CustomLogic, a.TargetId);
                    break;

                case OutcomeType.SetPlayerActor:
                    if (a.TargetActor != null)
                        sim.SetPlayerActor(a.TargetActor);
                    else
                        Log.Warning("[OutcomeAction] SetPlayerActor chosen but TargetActor is null.");
                    break;
            }
        }

        /*───────────────────────────────────────────────────────────────────────────
         * Shared helper
         *───────────────────────────────────────────────────────────────────────────*/
        private static void ApplyCustomLogic(IGameContext sim, BaseOutcome customLogic, string targetIdFallback)
        {
            if (customLogic != null)
            {
                customLogic.Execute(sim);
                Log.Info("[Outcome] Executed custom outcome logic.");
                return;
            }

            var co = GameData.Instance.CustomOutcomes.FirstOrDefault(x => x.name == targetIdFallback);
            if (co != null)
                co.Execute(sim);
            else
                Log.Warning("[Outcome] CustomOutcome type selected but no customLogic assigned.");
        }
    }

    /*───────────────────────────────────────────────────────────────────────────
     * New per-action structure for multi-action mode
     *───────────────────────────────────────────────────────────────────────────*/
    [System.Serializable]
    public class OutcomeAction
    {
        [SerializeField] private OutcomeType _type = OutcomeType.ModifyResource;

        // Optional per-action probability (0..1). Defaults to 1.0 (always).
        [Range(0f, 1f)]
        [SerializeField] private float _probability = 1f;

        // Generic scalar when needed (resource delta, relationship delta, etc.)
        [SerializeField] private float _value = 0f;

        // Generic string id fallback (event id, resource id, variable id, flag id, etc.)
        [SerializeField] private string _targetId;

        // Rich references (prefer these in the editor when available)
        [SerializeField] private SimEventSO _targetEvent;
        [SerializeField] private Resource _targetResource;
        [SerializeField] private NarrativeVariableSO _targetVariable;
        [SerializeField] private ActorSO _sourceActor;
        [SerializeField] private ActorSO _targetActor;
        [SerializeField] private StoryArcSO _storyArc;

        // Flags per action
        [SerializeField] private List<string> _flagsToAdd = new();
        [SerializeField] private List<string> _flagsToRemove = new();

        // Optional custom logic per action
        [SerializeField] private BaseOutcome _customLogic;

        // Accessors
        public OutcomeType Type => _type;
        public float Probability
        {
            get => _probability;
            set => _probability = Mathf.Clamp01(value);
        }
        public float Value => _value;
        public string TargetId => _targetId;

        public SimEventSO TargetEvent => _targetEvent;
        public Resource TargetResource => _targetResource;
        public NarrativeVariableSO TargetVariable => _targetVariable;
        public ActorSO SourceActor => _sourceActor;
        public ActorSO TargetActor => _targetActor;
        public StoryArcSO StoryArc => _storyArc;
        public BaseOutcome CustomLogic => _customLogic;

        public IReadOnlyList<string> FlagsToAdd => _flagsToAdd;
        public IReadOnlyList<string> FlagsToRemove => _flagsToRemove;

        /// <summary>Convenience for bool setters (variable id fallback).</summary>
        public string TargetIdOrVariableId => _targetVariable != null ? _targetVariable.Id : _targetId;

        public bool ShouldOccur() => UnityEngine.Random.value <= _probability;
    }

    /*───────────────────────────────────────────────────────────────────────────
     * BaseOutcome — the primary extension point for custom game logic.
     * Implement Execute(IGameContext) to add any effect without depending on
     * GameController or any concrete NF runtime type.
     *───────────────────────────────────────────────────────────────────────────*/
    public abstract class BaseOutcome : ScriptableObject
    {
        public abstract void Execute(IGameContext context);
    }

    public class AddFlagOutcome : BaseOutcome
    {
        public string FlagId;

        public override void Execute(IGameContext context)
        {
            context.AddFlag(FlagId);
            Log.Info($"[BaseOutcome] Flag added: {FlagId}");
        }
    }

    public class ModifyResourceOutcome : BaseOutcome
    {
        public string ResourceId;
        public float Amount;

        public override void Execute(IGameContext context)
        {
            context.ModifyResource(ResourceId, Amount);
            Log.Info($"[BaseOutcome] Resource {ResourceId} modified by {Amount}");
        }
    }
}
