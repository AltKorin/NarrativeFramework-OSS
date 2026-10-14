using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using SimulationGame;                // GameController, GameData
using DBG = SimulationGame.DebugTools.Log; // w pełni kwalifikowany logger

namespace SimulationGame
{
    /// <summary>
    /// Runtime debugging tool for narrative system with OnGUI interface (no core changes).
    /// Uses reflection to read private state safely in Editor/Dev builds.
    /// </summary>
    public class NarrativeDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        public KeyCode toggleKey = KeyCode.F12;
        public bool startOpenInEditor = true;
        public bool onlyInDevelopmentBuilds = true;

        private Rect _windowRect = new Rect(20, 20, 640, 720);
        private bool _visible;

        // Tabs
        private int _tab = 0;
        private readonly string[] _tabs = { "State", "Events", "Performance", "Tools", "Logs" };
        private Vector2 _svState, _svEvents, _svTools, _svLogs;

        // Cached refs
        private GameController _gc;

        // GUI styles
        [SerializeField] private Color _headerColor = new(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color _okColor     = new(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color _warnColor   = new(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _errorColor  = new(1f, 0.3f, 0.3f, 1f);
        private GUIStyle _h, _b;

        // Events tab pickers
        private string _evFilter = "";
        private int _evIndex = 0;

        // Logs
        private readonly Queue<LogEntry> _log = new();
        private const int MAX_LOG = 120;
        private string _cmd = "";

        public class LogEntry { public float t; public string type, msg; public Color c;
            public LogEntry(string ty, string m, Color col){ t = Time.time; type=ty; msg=m; c=col; } }

        private void Awake()
        {
            if (onlyInDevelopmentBuilds && !Application.isEditor && !Debug.isDebugBuild)
            { enabled = false; return; }

#if UNITY_EDITOR
            _visible = startOpenInEditor;
#else
            _visible = false;
#endif
        }

        private void Start()
        {
            EnsureGC();
            _h = new GUIStyle(GUI.skin.label){ fontSize=16, fontStyle=FontStyle.Bold, normal={ textColor=_headerColor } };
            _b = new GUIStyle(GUI.skin.label){ fontStyle=FontStyle.Bold };
            Log("INFO", "NarrativeDebugger started", _okColor);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
            if (!_gc) EnsureGC();
        }

        private void EnsureGC()
        {
            _gc = FindFirstObjectByType<GameController>();
            if (!_gc) DBG.Warning("[NarrativeDebugger] GameController not found");
        }

        private void OnGUI()
        {
            if (!_visible) return;

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Narrative Debugger",
                GUILayout.Width(640), GUILayout.Height(720));
        }

        private void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(_windowRect.width-22, 2, 20, 20), "×")) { _visible = false; return; }

            GUILayout.Space(4);
            _tab = GUILayout.Toolbar(_tab, _tabs);
            GUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawState(); break;
                case 1: DrawEvents(); break;
                case 2: DrawPerf(); break;
                case 3: DrawTools(); break;
                case 4: DrawLogs(); break;
            }

            GUI.DragWindow(new Rect(0,0,10000,24));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tabs
        // ─────────────────────────────────────────────────────────────────────

        private void DrawState()
        {
            _svState = GUILayout.BeginScrollView(_svState, false, true);
            GUILayout.Label("STATE", _h); GUILayout.Space(6);

            if (!_gc) { GUILayout.Label("GameController not found!", GUI.skin.box); GUILayout.EndScrollView(); return; }

            GUILayout.Label($"Current Day:   {GetCurrentDay()}");
            GUILayout.Label($"Current Event: {GetCurrentEventName()}");
            GUILayout.Label($"Save Name:     {GetSaveName()}");
            GUILayout.Space(8);

            GUILayout.Label("Resources:", _b);
            var res = GetResources();
            if (res != null && res.Count > 0) foreach (var kv in res) GUILayout.Label($"  {kv.Key}: {kv.Value:0.###}");
            else GUILayout.Label("  (none)");

            GUILayout.Space(8);

            GUILayout.Label("Flags:", _b);
            var flags = GetFlags();
            if (flags != null && flags.Count > 0) foreach (var f in flags) GUILayout.Label($"  • {f}");
            else GUILayout.Label("  (none)");

            GUILayout.Space(8);

            GUILayout.Label("Narrative Variables:", _b);
            var nvars = GetNarrativeVariables();
            if (nvars != null && nvars.Count > 0) foreach (var kv in nvars) GUILayout.Label($"  {kv.Key}: {kv.Value:0.###}");
            else GUILayout.Label("  (none)");

            GUILayout.Space(8);

            GUILayout.Label("Narrative Booleans:", _b);
            var nbools = GetNarrativeBooleans();
            if (nbools != null && nbools.Count > 0) foreach (var kv in nbools) GUILayout.Label($"  [{(kv.Value ? "✔":" ")}] {kv.Key}");
            else GUILayout.Label("  (none)");

            GUILayout.EndScrollView();
        }

        private void DrawEvents()
        {
            _svEvents = GUILayout.BeginScrollView(_svEvents, false, true);
            GUILayout.Label("EVENTS", _h); GUILayout.Space(6);

            GUILayout.Label("Jump to Event:", _b);

            if (GameData.Instance == null || GameData.Instance.Events == null || GameData.Instance.Events.Count == 0)
            { GUILayout.Label("No GameData/Events found."); GUILayout.EndScrollView(); return; }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(48));
            _evFilter = GUILayout.TextField(_evFilter ?? "", GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var list = string.IsNullOrWhiteSpace(_evFilter)
                ? GameData.Instance.Events.Where(e => e).ToList()
                : GameData.Instance.Events.Where(e => e && e.name.ToLowerInvariant().Contains(_evFilter.ToLowerInvariant())).ToList();

            if (list.Count > 0)
            {
                _evIndex = Mathf.Clamp(_evIndex, 0, list.Count-1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(24))) _evIndex = (_evIndex - 1 + list.Count) % list.Count;
                GUILayout.Label(list[_evIndex].name, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(">", GUILayout.Width(24))) _evIndex = (_evIndex + 1) % list.Count;
                if (GUILayout.Button("Present", GUILayout.Width(96)))
                {
                    if (!TryPresentEvent(list[_evIndex])) DBG.Warning("[NarrativeDebugger] PresentEvent not found.");
                    else Log("ACTION", $"Present Event: {list[_evIndex].name}", _okColor);
                }
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("No events match filter.");

            GUILayout.EndScrollView();
        }

        private void DrawPerf()
        {
            GUILayout.Label("PERFORMANCE", _h); GUILayout.Space(6);
            GUILayout.Label("• (Optional) integrate custom metrics here", GUI.skin.box);
        }

        private void DrawTools()
        {
            _svTools = GUILayout.BeginScrollView(_svTools, false, true);
            GUILayout.Label("TOOLS", _h); GUILayout.Space(6);

            if (!_gc) { GUILayout.Label("GameController not found!", GUI.skin.box); GUILayout.EndScrollView(); return; }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rewind One")) { if (!TryCallPublic("RequestRewind")) DBG.Warning("RequestRewind() not found"); }
            if (GUILayout.Button("Restart Run")) { if (!TryCallPublic("RestartRun"))    DBG.Warning("RestartRun() not found"); }
            if (GUILayout.Button("Next Day"))    { if (!TryCallPublic("AdvanceDay"))    DBG.Warning("AdvanceDay() not found"); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.Label("Current Choices:", _b);
            var choices = GetCurrentChoices();
            if (choices == null || choices.Count == 0) GUILayout.Label("(no choices)");
            else
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.IsNullOrEmpty(choices[i]) ? $"Choice {i}" : choices[i], GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Select", GUILayout.Width(80)))
                    {
                        if (!TrySelectChoice(i)) DBG.Warning($"SelectChoice({i}) failed");
                        else Log("ACTION", $"Selected choice #{i}: {choices[i]}", _okColor);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Export", _b);
            if (GUILayout.Button("Export State (JSON)")) ExportState();

            GUILayout.EndScrollView();
        }

        private void DrawLogs()
        {
            _svLogs = GUILayout.BeginScrollView(_svLogs, false, true);
            GUILayout.Label("LOGS", _h); GUILayout.Space(6);

            foreach (var e in _log.ToArray())
            {
                var old = GUI.color; GUI.color = e.c;
                GUILayout.Label($"[{e.t:0.00}] {e.type}: {e.msg}");
                GUI.color = old;
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            _cmd = GUILayout.TextField(_cmd ?? "", GUILayout.MinWidth(200));
            if (GUILayout.Button("Run", GUILayout.Width(64)))
            {
                RunCmd(_cmd); _cmd = "";
            }
            if (GUILayout.Button("Clear", GUILayout.Width(64))) _log.Clear();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reflection-based state readers (no core changes)
        // ─────────────────────────────────────────────────────────────────────

        private static FieldInfo  F(object o, string n) => o?.GetType().GetField(n, BindingFlags.Instance|BindingFlags.NonPublic);
        private static PropertyInfo P(object o, string n) => o?.GetType().GetProperty(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static MethodInfo M(object o, string n, params System.Type[] sig) =>
            o?.GetType().GetMethod(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic, null, sig, null);

        private int GetCurrentDay()
        {
            // prefer simulation.CurrentTimeUnit, fallback to _currentDay
            var sim = F(_gc, "_simulation")?.GetValue(_gc);
            if (sim != null)
            {
                var p = P(sim, "CurrentTimeUnit");
                if (p != null && p.PropertyType == typeof(int))
                    return (int)p.GetValue(sim);
            }
            var dayField = F(_gc, "_currentDay");
            if (dayField != null && dayField.FieldType == typeof(int)) return (int)dayField.GetValue(_gc);
            return 0;
        }

        private string GetCurrentEventName()
        {
            var ev = F(_gc, "_currentEvent")?.GetValue(_gc) as Object;
            return ev ? ev.name : "(none)";
        }

        private string GetSaveName()
        {
            var sm = F(_gc, "_saveManager")?.GetValue(_gc);
            if (sm != null)
            {
                var p = P(sm, "CurrentSaveName");
                if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(sm);
            }
            return "(unsaved)";
        }

        private Dictionary<string, float> GetResources()
        {
            var f = F(_gc, "_resources");
            return f?.GetValue(_gc) as Dictionary<string, float>;
        }

        private HashSet<string> GetFlags()
        {
            var f = F(_gc, "_flags");
            return f?.GetValue(_gc) as HashSet<string>;
        }

        private Dictionary<string, float> GetNarrativeVariables()
        {
            // try NarrativeVariables property first (if exists), else use manager
            var prop = P(_gc, "NarrativeVariables");
            if (prop != null) return prop.GetValue(_gc) as Dictionary<string, float>;

            var nvm = F(_gc, "_narrativeVariableManager")?.GetValue(_gc);
            if (nvm != null)
            {
                var m = M(nvm, "GetAllFloatsRO");
                if (m != null) return m.Invoke(nvm, null) as Dictionary<string, float>;
            }
            return null;
        }

        private Dictionary<string, bool> GetNarrativeBooleans()
        {
            var prop = P(_gc, "NarrativeBooleans");
            if (prop != null) return prop.GetValue(_gc) as Dictionary<string, bool>;

            var nvm = F(_gc, "_narrativeVariableManager")?.GetValue(_gc);
            if (nvm != null)
            {
                // prefer read-only method if exists, else fallback to copy-style method names
                var m = M(nvm, "GetAllBoolsRO") ?? M(nvm, "GetAllBools");
                if (m != null) return m.Invoke(nvm, null) as Dictionary<string, bool>;
            }
            return null;
        }

        private List<string> GetCurrentChoices()
        {
            var ev = F(_gc, "_currentEvent")?.GetValue(_gc);
            if (ev == null) return new List<string>();

            // event.Choices (IList)
            var choicesProp = P(ev, "Choices");         // typical property
            var choicesField = F(ev, "Choices") ?? F(ev, "_choices"); // fallback
            var listObj = choicesProp != null ? choicesProp.GetValue(ev) : choicesField?.GetValue(ev);
            if (listObj is IEnumerable enumerable)
            {
                var result = new List<string>();
                foreach (var c in enumerable)
                {
                    // prefer c.Text, fallback c.choiceText
                    var pText = P(c, "Text") ?? P(c, "choiceText");
                    string label = pText != null ? (pText.GetValue(c) as string) : "(choice)";
                    result.Add(label);
                }
                return result;
            }
            return new List<string>();
        }

        private bool TrySelectChoice(int index)
        {
            var ev = F(_gc, "_currentEvent")?.GetValue(_gc);
            if (ev == null) return false;

            var choicesProp = P(ev, "Choices");
            var listObj = choicesProp != null ? choicesProp.GetValue(ev) : F(ev, "Choices")?.GetValue(ev);
            if (listObj is IList list)
            {
                if (index < 0 || index >= list.Count) return false;
                var choice = list[index];

                // PushRewindSnapshot() if exists
                var push = M(_gc, "PushRewindSnapshot");
                push?.Invoke(_gc, null);

                // OnChoiceSelected(choice) (private) – normal flow
                var handler = M(_gc, "OnChoiceSelected", choice.GetType());
                if (handler != null) { handler.Invoke(_gc, new object[] { choice }); return true; }
            }
            return false;
        }

        private bool TryPresentEvent(SimEventSO ev)
        {
            if (ev == null) return false;

            // Prefer public method names if any exist in your build
            var dbgJump = M(_gc, "Debug_JumpToEvent", typeof(SimEventSO));
            if (dbgJump != null) { dbgJump.Invoke(_gc, new object[] { ev }); return true; }

            // Fallback to private PresentEvent(SimEventSO)
            var present = M(_gc, "PresentEvent", typeof(SimEventSO));
            if (present != null) { present.Invoke(_gc, new object[] { ev }); return true; }

            return false;
        }

        private bool TryCallPublic(string method)
        {
            var mi = M(_gc, method);
            if (mi != null) { mi.Invoke(_gc, null); return true; }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Export / Commands / Log
        // ─────────────────────────────────────────────────────────────────────

        private void ExportState()
        {
            if (!_gc) { Log("EXPORT", "Cannot export - no controller", _errorColor); return; }

            try
            {
                var state = new
                {
                    timestamp  = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    currentDay = GetCurrentDay(),
                    currentEvent = GetCurrentEventName(),
                    saveName   = GetSaveName(),
                    resources  = GetResources(),
                    flags      = GetFlags()?.ToArray(),
                    narrativeVars  = GetNarrativeVariables(),
                    narrativeBools = GetNarrativeBooleans()
                };

                string json = JsonUtility.ToJson(state, true);

#if UNITY_EDITOR
                var path = UnityEditor.EditorUtility.SaveFilePanel(
                    "Export Narrative State JSON",
                    Application.dataPath,
                    "narrative_state.json",
                    "json");
                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, json);
                    Log("EXPORT", $"State exported to {path}", _okColor);
                }
#else
                Log("EXPORT", json, _okColor);
#endif
            }
            catch (System.Exception ex)
            {
                Log("EXPORT", $"Export failed: {ex.Message}", _errorColor);
            }
        }

        private void RunCmd(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            var op = cmd.Trim().ToLowerInvariant();
            switch (op)
            {
                case "rewind":  if (!TryCallPublic("RequestRewind")) DBG.Warning("RequestRewind() not found"); else Log("CMD", "rewind", _okColor); break;
                case "restart": if (!TryCallPublic("RestartRun"))    DBG.Warning("RestartRun() not found");    else Log("CMD", "restart", _okColor); break;
                case "nextday": if (!TryCallPublic("AdvanceDay"))    DBG.Warning("AdvanceDay() not found");    else Log("CMD", "nextday", _okColor); break;
                default: Log("CMD", $"Unknown: {op}", _warnColor); break;
            }
        }

        private void Log(string type, string msg, Color c)
        {
            _log.Enqueue(new LogEntry(type, msg, c));
            while (_log.Count > MAX_LOG) _log.Dequeue();
        }
    }
}
