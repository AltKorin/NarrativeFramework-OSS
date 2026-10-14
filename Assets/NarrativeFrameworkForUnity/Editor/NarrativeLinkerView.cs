// Assets/SimulationGame/Editor/NarrativeLinkerView.cs
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Main Narrative Linker window that hosts multiple authoring tabs.
    /// Centralizes the active GameData and pushes it to all views.
    /// </summary>
    public class NarrativeLinkerWindow : EditorWindow
    {
        private enum ViewTab
        {
            GameData,
            EventsTree,
            NewEvent,
            Events,
            StoryStacks,
            Outcomes,
            CustomOutcomes,
            StoryArcs,
            Variables,
            Actors,
            Relationships,
            Endings,
            Resources
        }

        private ViewTab currentTab = ViewTab.GameData;

        // Views (keep your existing construction)
        private NarrativeLinkerGameDataView gameDataView;
        private NarrativeLinkerEventView eventView;
         private NarrativeLinkerNewEventView newEventView = new NarrativeLinkerNewEventView();

        private NarrativeLinkerOutcomeView outcomeView = new NarrativeLinkerOutcomeView();
        private NarrativeLinkerStoryArcView storyArcView;
        private NarrativeLinkerVariableView variableView = new NarrativeLinkerVariableView();
        private NarrativeLinkerActorView actorView = new NarrativeLinkerActorView();
        private NarrativeLinkerRelationshipView relationshipView = new NarrativeLinkerRelationshipView();
        private NarrativeLinkerEndingView endingView = new NarrativeLinkerEndingView();
        private NarrativeLinkerCustomOutcomeView customOutcomeView = new NarrativeLinkerCustomOutcomeView();
        private NarrativeLinkerStoryStackView storyStackView = new NarrativeLinkerStoryStackView();
        private NarrativeLinkerEventTreeView eventTreeView = new NarrativeLinkerEventTreeView();

        // Active GameData state
        private const string PREF_GAMEDATA_PATH = "NarrativeLinker.PreferredGameDataPath";
        private GameData _activeGameData;
        private string  _activeGameDataPathCache;

        // Singleton-ish handle so other code can call UpdateAllViews safely
        private static NarrativeLinkerWindow _instance;
        private System.Action<GameData> _contextHandler;

        [MenuItem("Tools/Narrative Framework/Narrative Linker")]
        public static void ShowWindow()
        {
            GetWindow<NarrativeLinkerWindow>("Narrative Linker");
        }

        private void OnEnable()
        {
            _instance = this;

            // Initialize your existing views
            gameDataView = new NarrativeLinkerGameDataView();
            newEventView = new NarrativeLinkerNewEventView();
            eventView = new NarrativeLinkerEventView();
            outcomeView = new NarrativeLinkerOutcomeView(); outcomeView.OnEnable();
            storyArcView = new NarrativeLinkerStoryArcView();
            variableView = new NarrativeLinkerVariableView();
            actorView = new NarrativeLinkerActorView(); actorView.OnEnable();
            relationshipView = new NarrativeLinkerRelationshipView(); relationshipView.OnEnable();
            endingView = new NarrativeLinkerEndingView();
            customOutcomeView = new NarrativeLinkerCustomOutcomeView();
            storyStackView = new NarrativeLinkerStoryStackView(); storyStackView.OnEnable();
            eventTreeView = new NarrativeLinkerEventTreeView();

            // Subscribe to the shared context so any SetActive(...) refreshes all tabs
            _contextHandler = gd => UpdateAllViews(gd);
            NarrativeLinkerContext.Changed += _contextHandler;

            // Seed with current context or resolve a default
            var initial = NarrativeLinkerContext.GetOrResolve();
            UpdateAllViews(initial);
        }

        private void OnDisable()
        {
            if (_contextHandler != null)
                NarrativeLinkerContext.Changed -= _contextHandler;

            if (_instance == this)
                _instance = null;
        }

        private void OnGUI()
        {
            // Safety: if context changed outside the window, sync once here.
            var ctx = NarrativeLinkerContext.GetOrResolve();
            if (!ReferenceEquals(ctx, _activeGameData))
                UpdateAllViews(ctx);

            DrawToolbar();

            EditorGUILayout.Space();

            switch (currentTab)
            {
                case ViewTab.GameData:
                    gameDataView.Draw();
                    break;
                case ViewTab.NewEvent:
                    newEventView.Draw();
                    break;
                case ViewTab.Events:
                    eventView.Draw();
                    break;
                case ViewTab.Outcomes:
                    outcomeView.Draw();
                    break;
                case ViewTab.StoryArcs:
                    storyArcView.Draw();
                    break;
                case ViewTab.Variables:
                    variableView.Draw();
                    break;
                case ViewTab.Actors:
                    actorView.Draw();
                    break;
                case ViewTab.Relationships:
                    relationshipView.Draw();
                    break;
                case ViewTab.Endings:
                    endingView.Draw();
                    break;
                case ViewTab.StoryStacks:
                    storyStackView.Draw();
                    break;
                case ViewTab.EventsTree:
                    eventTreeView.Draw();
                    break;
                case ViewTab.Resources:
                    if (GUILayout.Button("Open Resources Manager", GUILayout.Height(28)))
                        NarrativeLinkerResourcesView.Open();

                    EditorGUILayout.HelpBox(
                        "The Resources Manager lets you create/edit Resource assets and automatically add them to GameData.\n" +
                        "Click the button above to open it. This keeps the main Linker window lightweight and avoids breaking existing tabs.",
                        MessageType.Info);
                    break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(currentTab == ViewTab.GameData, "Game Data", EditorStyles.toolbarButton))
                currentTab = ViewTab.GameData;
            if (GUILayout.Toggle(currentTab == ViewTab.EventsTree, "Event Tree", EditorStyles.toolbarButton))
                currentTab = ViewTab.EventsTree;
            if (GUILayout.Toggle(currentTab == ViewTab.Resources, "Resources", EditorStyles.toolbarButton))
                currentTab = ViewTab.Resources;
            if (GUILayout.Toggle(currentTab == ViewTab.NewEvent, "New Event", EditorStyles.toolbarButton))
                currentTab = ViewTab.NewEvent;
            if (GUILayout.Toggle(currentTab == ViewTab.Events, "Event's Choices", EditorStyles.toolbarButton))
                currentTab = ViewTab.Events;
            if (GUILayout.Toggle(currentTab == ViewTab.Outcomes, "Outcomes", EditorStyles.toolbarButton))
                currentTab = ViewTab.Outcomes;
            if (GUILayout.Toggle(currentTab == ViewTab.StoryArcs, "Story Arcs", EditorStyles.toolbarButton))
                currentTab = ViewTab.StoryArcs;
            if (GUILayout.Toggle(currentTab == ViewTab.Variables, "Variables", EditorStyles.toolbarButton))
                currentTab = ViewTab.Variables;
            if (GUILayout.Toggle(currentTab == ViewTab.Actors, "Actors", EditorStyles.toolbarButton))
                currentTab = ViewTab.Actors;
            if (GUILayout.Toggle(currentTab == ViewTab.Relationships, "Relationships", EditorStyles.toolbarButton))
                currentTab = ViewTab.Relationships;
            if (GUILayout.Toggle(currentTab == ViewTab.Endings, "Endings", EditorStyles.toolbarButton))
                currentTab = ViewTab.Endings;
            
            if (GUILayout.Toggle(currentTab == ViewTab.StoryStacks, "Story Stacks", EditorStyles.toolbarButton))
                currentTab = ViewTab.StoryStacks;

            GUILayout.FlexibleSpace();

            // Status: show which GameData is active + Ping
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Label(_activeGameData ? _activeGameData.name : "(no GameData)", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_activeGameData == null))
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(_activeGameData);

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────────────
        // Public hub: update all views (callable from anywhere)
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Single entry-point to broadcast a new GameData to all tabs and repaint the window.
        /// Safe to call from any editor script.
        /// </summary>
        public static void UpdateAllViews(GameData gd)
        {
            if (_instance == null) return;

            // Avoid redundant work
            var path = gd ? AssetDatabase.GetAssetPath(gd) : null;
            if (ReferenceEquals(_instance._activeGameData, gd) &&
                _instance._activeGameDataPathCache == path)
            {
                return;
            }

            _instance._activeGameData = gd;
            _instance._activeGameDataPathCache = path;

            _instance.AssignGameDataToAllViews(gd);
            _instance.Repaint();
        }

        // ─────────────────────────────────────────────────────────────
        // Central GameData injection
        // ─────────────────────────────────────────────────────────────
        private void AssignGameDataToAllViews(GameData gd)
        {
            InjectGameData(gameDataView, gd);
            InjectGameData(newEventView, gd);
            InjectGameData(eventView, gd);
            InjectGameData(outcomeView, gd);
            InjectGameData(storyArcView, gd);
            InjectGameData(variableView, gd);
            InjectGameData(actorView, gd);
            InjectGameData(relationshipView, gd);
            InjectGameData(endingView, gd);
            InjectGameData(customOutcomeView, gd);
            InjectGameData(storyStackView, gd);
            InjectGameData(eventTreeView, gd);
        }

        private static void InjectGameData(object viewInstance, GameData gd)
        {
            if (viewInstance == null) return;
            var t = viewInstance.GetType();

            // 1) Preferred: property GameData
            var prop = t.GetProperty("GameData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(viewInstance, gd);
                InvokeChangedHook(t, viewInstance, gd);
                return;
            }

            // 2) Or method SetGameData(GameData)
            var set = t.GetMethod("SetGameData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (set != null)
            {
                set.Invoke(viewInstance, new object[] { gd });
                InvokeChangedHook(t, viewInstance, gd);
                return;
            }

            // 3) Or common fields
            var f =
                t.GetField("_gameData", BindingFlags.NonPublic | BindingFlags.Instance) ??
                t.GetField("gameData",  BindingFlags.NonPublic | BindingFlags.Instance) ??
                t.GetField("GameData",  BindingFlags.Public    | BindingFlags.Instance);
            if (f != null)
            {
                f.SetValue(viewInstance, gd);
                InvokeChangedHook(t, viewInstance, gd);
                return;
            }

            // 4) Last resort: just call hook if the view rebuilds from context internally
            InvokeChangedHook(t, viewInstance, gd);
        }

        /// <summary>If the view exposes OnActiveGameDataChanged(GameData), call it.</summary>
        private static void InvokeChangedHook(System.Type t, object viewInstance, GameData gd)
        {
            var hook = t.GetMethod("OnActiveGameDataChanged", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (hook != null)
                hook.Invoke(viewInstance, new object[] { gd });
        }
    }
}
