using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimulationGame;
using SimulationGame.Interfaces;
using Object = UnityEngine.Object;

namespace SimulationGame.Tests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pure C# IGameContext implementation — no MonoBehaviour, no Unity lifecycle.
    /// Used to verify interface contract and Outcome routing without any scene setup.
    /// </summary>
    internal class FakeGameContext : IGameContext
    {
        private readonly HashSet<string> _flags = new();
        public readonly List<string> ResourceLog = new();
        public readonly List<string> AchievementLog = new();

        public void AddFlag(string flag) => _flags.Add(flag);
        public void RemoveFlag(string flag) => _flags.Remove(flag);
        public bool HasFlag(string flag) => _flags.Contains(flag);
        public IEnumerable<string> GetCurrentFlags() => _flags;

        public void ModifyResource(string id, float a) => ResourceLog.Add($"{id}:{a}");
        public void Log(string msg) { }
        public void TriggerEventById(string id) { }
        public void SetRelationship(string t, ActorSO s, ActorSO tgt, float v) { }
        public void ModifyRelationship(string t, ActorSO s, ActorSO tgt, float d) { }
        public void SetNarrativeVariable(string id, float v) { }
        public void ModifyNarrativeVariable(string id, float d) { }
        public void SetNarrativeBool(string id, bool v) { }
        public void AwardAchievement(string id) => AchievementLog.Add(id);
        public void UnlockFeature(string id) { }
        public void SetPlayerActor(ActorSO a) { }
        public void TriggerStoryArc(StoryArcSO arc) { }
    }

    /// <summary>
    /// Minimal ISimulationManager that delegates all IGameContext operations to a
    /// backing FakeGameContext. Time-control methods are stubs — Outcome.Apply
    /// only needs the mutation half.
    /// </summary>
    internal class FakeSimulationManager : ISimulationManager
    {
        public readonly FakeGameContext Context;
        public FakeSimulationManager(FakeGameContext ctx) => Context = ctx;

        // ── ITimeSimulation stubs ──────────────────────────────────────────────
        public int CurrentTimeUnit => 1;
        public int MaxTimeUnits => 30;
        public string TimeUnitName => "Day";
        public bool IsLastTimeUnit => false;
        public int RemainingTimeUnits => 29;
        public float Progress => 0f;
        public event Action<int> OnTimeUnitStarted = delegate { };
        public event Action<int> OnTimeUnitEnded = delegate { };
        public event Action<int> OnFinalTimeUnitCompleted = delegate { };
        public event Action OnSimulationCompleted = delegate { };
        public void Initialize() { }
        public void StartTimeUnit() { }
        public bool AdvanceToNextTimeUnit() => false;
        public void SetCurrentTimeUnit(int t) { }
        public bool SetTimeUnit(int t) => true;
        public void ResetSimulation() { }
        public void ConfigureSimulation(int max, string name = null, int start = 1) { }
        public int GetCurrentDay() => 1;

        // ── IGameContext — delegate to backing context ─────────────────────────
        public void AddFlag(string flag) => Context.AddFlag(flag);
        public void RemoveFlag(string flag) => Context.RemoveFlag(flag);
        public bool HasFlag(string flag) => Context.HasFlag(flag);
        public IEnumerable<string> GetCurrentFlags() => Context.GetCurrentFlags();
        public void ModifyResource(string id, float a) => Context.ModifyResource(id, a);
        public void Log(string msg) => Context.Log(msg);
        public void TriggerEventById(string id) => Context.TriggerEventById(id);
        public void SetRelationship(string t, ActorSO s, ActorSO tgt, float v) => Context.SetRelationship(t, s, tgt, v);
        public void ModifyRelationship(string t, ActorSO s, ActorSO tgt, float d) => Context.ModifyRelationship(t, s, tgt, d);
        public void SetNarrativeVariable(string id, float v) => Context.SetNarrativeVariable(id, v);
        public void ModifyNarrativeVariable(string id, float d) => Context.ModifyNarrativeVariable(id, d);
        public void SetNarrativeBool(string id, bool v) => Context.SetNarrativeBool(id, v);
        public void AwardAchievement(string id) => Context.AwardAchievement(id);
        public void UnlockFeature(string id) => Context.UnlockFeature(id);
        public void SetPlayerActor(ActorSO a) => Context.SetPlayerActor(a);
        public void TriggerStoryArc(StoryArcSO arc) => Context.TriggerStoryArc(arc);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group 1 — IGameContext interface contract (pure C#, no Unity lifecycle)
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class IGameContextContractTests
    {
        [Test]
        public void AddFlag_IsRetrievableWithHasFlag()
        {
            var ctx = new FakeGameContext();
            ctx.AddFlag("test_flag");
            Assert.IsTrue(ctx.HasFlag("test_flag"));
        }

        [Test]
        public void HasFlag_ReturnsFalse_ForUnknownFlag()
        {
            var ctx = new FakeGameContext();
            Assert.IsFalse(ctx.HasFlag("nonexistent"));
        }

        [Test]
        public void RemoveFlag_RemovesExistingFlag()
        {
            var ctx = new FakeGameContext();
            ctx.AddFlag("temp");
            ctx.RemoveFlag("temp");
            Assert.IsFalse(ctx.HasFlag("temp"));
        }

        [Test]
        public void AddFlag_IsDeduplicated()
        {
            var ctx = new FakeGameContext();
            ctx.AddFlag("dup");
            ctx.AddFlag("dup");
            int count = 0;
            foreach (var f in ctx.GetCurrentFlags())
                if (f == "dup") count++;
            Assert.AreEqual(1, count, "Same flag added twice should only appear once.");
        }

        [Test]
        public void ISimulationManager_IsAssignableFrom_IGameContext()
        {
            // Compile-time proof: ISimulationManager : IGameContext
            ISimulationManager sim = new FakeSimulationManager(new FakeGameContext());
            Assert.IsInstanceOf<IGameContext>(sim,
                "ISimulationManager must be usable wherever IGameContext is expected.");
        }

        [Test]
        public void ISimulationManager_IsAssignableFrom_ITimeSimulation()
        {
            ISimulationManager sim = new FakeSimulationManager(new FakeGameContext());
            Assert.IsInstanceOf<ITimeSimulation>(sim,
                "ISimulationManager must be usable wherever ITimeSimulation is expected.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group 2 — BaseOutcome extension point
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class BaseOutcomeTests
    {
        [Test]
        public void AddFlagOutcome_Execute_AddsFlag_ToContext()
        {
            var ctx = new FakeGameContext();
            var outcome = ScriptableObject.CreateInstance<AddFlagOutcome>();
            outcome.FlagId = "hero_flag";

            outcome.Execute(ctx);

            Assert.IsTrue(ctx.HasFlag("hero_flag"),
                "AddFlagOutcome.Execute should call context.AddFlag.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void ModifyResourceOutcome_Execute_LogsResource()
        {
            var ctx = new FakeGameContext();
            var outcome = ScriptableObject.CreateInstance<ModifyResourceOutcome>();
            outcome.ResourceId = "coin";
            outcome.Amount = 5f;

            outcome.Execute(ctx);

            Assert.Contains("coin:5", ctx.ResourceLog,
                "ModifyResourceOutcome.Execute should call context.ModifyResource.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void BaseOutcome_Execute_AcceptsIGameContext_NotISimulationManager()
        {
            // If this compiles, BaseOutcome.Execute(IGameContext) is the correct signature.
            // The test verifies the runtime contract is also satisfied.
            IGameContext ctx = new FakeGameContext();
            var outcome = ScriptableObject.CreateInstance<AddFlagOutcome>();
            outcome.FlagId = "compile_check";

            Assert.DoesNotThrow(() => outcome.Execute(ctx),
                "BaseOutcome.Execute must accept any IGameContext, not just ISimulationManager.");

            Object.DestroyImmediate(outcome);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group 3 — Outcome.Apply routing through ISimulationManager / IGameContext
    //           Core BUG-1 + BUG-2 verification
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class OutcomeApplyRoutingTests
    {
        private Outcome CreateOutcome(OutcomeType type, string targetId)
        {
            var o = ScriptableObject.CreateInstance<Outcome>();
            o.Initialize(type, targetId, 0f, 1f, "");
            return o;
        }

        [Test]
        public void Outcome_AddFlag_Apply_SetsFlag_OnContext()
        {
            // BUG-1 test: Outcome(AddFlag) → Apply(simulationManager) → context.HasFlag = true
            var ctx = new FakeGameContext();
            var sim = new FakeSimulationManager(ctx);
            var outcome = CreateOutcome(OutcomeType.AddFlag, "fedTheRegiment");

            outcome.Apply(sim);

            Assert.IsTrue(ctx.HasFlag("fedTheRegiment"),
                "Flag set via Outcome.Apply must be readable from the backing IGameContext.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void Outcome_RemoveFlag_Apply_RemovesFlag_FromContext()
        {
            var ctx = new FakeGameContext();
            ctx.AddFlag("temp_flag");
            var sim = new FakeSimulationManager(ctx);
            var outcome = CreateOutcome(OutcomeType.RemoveFlag, "temp_flag");

            outcome.Apply(sim);

            Assert.IsFalse(ctx.HasFlag("temp_flag"),
                "RemoveFlag outcome must remove the flag from the backing context.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void Outcome_ModifyResource_Apply_RoutesThrough_Context()
        {
            var ctx = new FakeGameContext();
            var sim = new FakeSimulationManager(ctx);
            var outcome = ScriptableObject.CreateInstance<Outcome>();
            outcome.Initialize(OutcomeType.ModifyResource, "coin", 3f, 1f, "");

            outcome.Apply(sim);

            Assert.Contains("coin:3", ctx.ResourceLog,
                "ModifyResource outcome must route through context.ModifyResource.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void Outcome_AwardAchievement_Apply_RoutesThrough_Context_Not_GameControllerInstance()
        {
            // BUG-2 test: AwardAchievement must not call GameController.Instance
            var ctx = new FakeGameContext();
            var sim = new FakeSimulationManager(ctx);
            var outcome = CreateOutcome(OutcomeType.AwardAchievement, "first_battle");

            // If this still calls GameController.Instance (null in tests), it would throw.
            Assert.DoesNotThrow(() => outcome.Apply(sim),
                "AwardAchievement must not depend on GameController.Instance.");

            Assert.Contains("first_battle", ctx.AchievementLog,
                "AwardAchievement should reach context.AwardAchievement.");

            Object.DestroyImmediate(outcome);
        }

        [Test]
        public void Outcome_SetPlayerActor_Apply_RoutesThrough_Sim_Not_GameControllerInstance()
        {
            // BUG-2 test: SetPlayerActor with null actor should warn, not throw on null singleton
            var ctx = new FakeGameContext();
            var sim = new FakeSimulationManager(ctx);
            var outcome = CreateOutcome(OutcomeType.SetPlayerActor, "");
            // TargetActor is null — should log warning, not NullRef on GameController.Instance

            Assert.DoesNotThrow(() => outcome.Apply(sim),
                "SetPlayerActor with null actor must not throw via GameController.Instance.");

            Object.DestroyImmediate(outcome);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group 4 — GameController flag mechanics (real MonoBehaviour)
    //           Fix 4: OnFlagAdded/OnFlagRemoved events
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class GameControllerFlagTests
    {
        private GameObject _go;
        private GameController _gc;

        [SetUp]
        public void SetUp()
        {
            // Suppress null-ref log errors from Awake (missing EventSystem, UIManager etc.)
            LogAssert.ignoreFailingMessages = true;

            _go = new GameObject("TestGameController");
            _gc = _go.AddComponent<GameController>();
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate triggers OnDisable → UnsubscribeFromEvents which nulls
            // the static events from within GameController (only legal from inside the class).
            Object.DestroyImmediate(_go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AddFlag_HasFlag_ReturnsTrue()
        {
            _gc.AddFlag("battle_pending:czarne");
            Assert.IsTrue(_gc.HasFlag("battle_pending:czarne"));
        }

        [Test]
        public void AddFlag_Fires_OnFlagAdded_Event()
        {
            string received = null;
            GameController.OnFlagAdded += f => received = f;

            _gc.AddFlag("test_event_flag");

            Assert.AreEqual("test_event_flag", received,
                "OnFlagAdded must fire with the flag name when AddFlag is called.");
        }

        [Test]
        public void RemoveFlag_Fires_OnFlagRemoved_Event()
        {
            _gc.AddFlag("to_remove");
            string received = null;
            GameController.OnFlagRemoved += f => received = f;

            _gc.RemoveFlag("to_remove");

            Assert.AreEqual("to_remove", received,
                "OnFlagRemoved must fire with the flag name when RemoveFlag is called.");
        }

        [Test]
        public void AddFlag_Deduplicates_OnFlagAdded_FiresOnce()
        {
            int callCount = 0;
            GameController.OnFlagAdded += _ => callCount++;

            _gc.AddFlag("dup");
            _gc.AddFlag("dup");

            Assert.AreEqual(1, callCount,
                "OnFlagAdded must fire only once for duplicate AddFlag calls.");
        }

        [Test]
        public void RemoveFlag_OnAbsentFlag_DoesNotFireEvent()
        {
            int callCount = 0;
            GameController.OnFlagRemoved += _ => callCount++;

            _gc.RemoveFlag("nonexistent");

            Assert.AreEqual(0, callCount,
                "OnFlagRemoved must not fire when the flag was not present.");
        }

        [Test]
        public void HasFlag_ReturnsFalse_ForUnknownFlag()
        {
            Assert.IsFalse(_gc.HasFlag("never_set"));
        }

        [Test]
        public void GetCurrentFlags_ReflectsAddedFlags()
        {
            _gc.AddFlag("f1");
            _gc.AddFlag("f2");

            var flags = new HashSet<string>(_gc.GetCurrentFlags());
            Assert.IsTrue(flags.Contains("f1") && flags.Contains("f2"),
                "GetCurrentFlags must return all added flags.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group 5 — Integration: SimulationManager → GameController flag delegation
    //           Fix 1: single source of truth
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class SimulationManagerFlagDelegationTests
    {
        private static readonly FieldInfo GameControllerField =
            typeof(SimulationManager).GetField("_gameController",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject _gcGo;
        private GameController _gc;
        private GameObject _simGo;
        private SimulationManager _sim;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _gcGo = new GameObject("TestGC");
            _gc = _gcGo.AddComponent<GameController>();

            _simGo = new GameObject("TestSim");
            _sim = _simGo.AddComponent<SimulationManager>();

            Assert.IsNotNull(GameControllerField,
                "Reflection could not find _gameController field — field was renamed or removed.");

            GameControllerField.SetValue(_sim, _gc);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gcGo);   // triggers OnDisable → clears static events
            Object.DestroyImmediate(_simGo);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SimulationManager_AddFlag_IsVisibleOn_GameController()
        {
            // THE core BUG-1 test:
            // Outcome calls simulationManager.AddFlag → must be visible via gameController.HasFlag
            _sim.AddFlag("fedTheRegiment");

            Assert.IsTrue(_gc.HasFlag("fedTheRegiment"),
                "Flag added through SimulationManager must be readable from GameController.");
        }

        [Test]
        public void SimulationManager_HasFlag_ReflectsGameController_State()
        {
            _gc.AddFlag("set_on_gc");

            Assert.IsTrue(_sim.HasFlag("set_on_gc"),
                "SimulationManager.HasFlag must delegate to GameController.");
        }

        [Test]
        public void SimulationManager_RemoveFlag_RemovesFrom_GameController()
        {
            _sim.AddFlag("to_remove");
            _sim.RemoveFlag("to_remove");

            Assert.IsFalse(_gc.HasFlag("to_remove"),
                "RemoveFlag through SimulationManager must remove from GameController._flags.");
        }

        [Test]
        public void SimulationManager_AddFlag_Fires_OnFlagAdded_Event()
        {
            // Fix 4 + Fix 1 combined: routing through SimulationManager still fires GameController event
            string received = null;
            GameController.OnFlagAdded += f => received = f;

            _sim.AddFlag("battle_pending:czarne");

            Assert.AreEqual("battle_pending:czarne", received,
                "GameController.OnFlagAdded must fire even when AddFlag is called via SimulationManager.");
        }

        [Test]
        public void Outcome_AddFlag_Apply_Via_SimulationManager_ReachesGameController()
        {
            // Full end-to-end BUG-1 verification:
            // Outcome.Apply(simulationManager) → simulationManager.AddFlag → gameController.HasFlag
            var outcome = ScriptableObject.CreateInstance<Outcome>();
            outcome.Initialize(OutcomeType.AddFlag, "introChoice_fedTheRegiment", 0f, 1f, "");

            outcome.Apply(_sim);

            Assert.IsTrue(_gc.HasFlag("introChoice_fedTheRegiment"),
                "Outcome.Apply(simulationManager) must result in gameController.HasFlag returning true.");

            Object.DestroyImmediate(outcome);
        }
    }
}
