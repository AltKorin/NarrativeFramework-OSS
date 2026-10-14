// DeveloperConsole.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimulationGame
{   

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[DefaultExecutionOrder(9999)]
public class DeveloperConsole : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Toggle console with this key in Editor/Development build.")]
    public KeyCode toggleKey = KeyCode.BackQuote;
    public bool startOpenInEditor = true;
    public bool onlyInDevelopmentBuild = true;

    [Header("Links (optional, will auto-find)")]
    public global::SimulationGame.GameController controller;
    public GameData gameData;

    [Header("Window")]
    public Rect windowRect = new Rect(20, 20, 520, 620);
    private bool _visible;
    private Vector2 _scroll;

    // Local editing buffers
    private readonly Dictionary<string, string> _resourceEdits = new();
    private readonly Dictionary<string, string> _relationEdits = new();
    private string _newFlag = "";
    private int _eventIndex = 0;
    private string _eventFilter = "";

    private void Awake()
    {
        if (onlyInDevelopmentBuild && !Application.isEditor && !Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }
#if UNITY_EDITOR
        _visible = startOpenInEditor;
#endif
        EnsureRefs();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _visible = !_visible;
        }
        if (!controller || !gameData)
            EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (!controller)
            controller = FindFirstObjectByType<global::SimulationGame.GameController>();
        if (!gameData)
            gameData = GameData.Instance ? GameData.Instance : FindFirstObjectByType<GameData>();
    }

    private void OnGUI()
    {
        if (!_visible) return;

        GUI.depth = 0;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Developer Console");
    }

    private void DrawWindow(int id)
    {
        if (!controller)
        {
            GUILayout.Label("GameController not found.");
            if (GUILayout.Button("Retry find")) EnsureRefs();
            GUI.DragWindow();
            return;
        }

        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawState();
        GUILayout.Space(6);

        DrawRewindAndFlow();
        GUILayout.Space(6);

        DrawJumpToEvent();
        GUILayout.Space(6);

        DrawResources();
        GUILayout.Space(6);

        DrawRelationships();
        GUILayout.Space(6);

        DrawFlags();

        GUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("Close")) _visible = false;

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // ── Sections ─────────────────────────────────────────────────────────────────

    private void DrawState()
    {
        GUILayout.Label("<b>State</b>", Rich());
        GUILayout.Label($"Event: <b>{controller.Debug_CurrentEventName}</b>", Rich());
        GUILayout.Label($"Day:   <b>{controller.Debug_CurrentDay}</b>", Rich());
    }

    private void DrawRewindAndFlow()
    {
        GUILayout.Label("<b>Flow</b>", Rich());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Rewind One"))
        {
            controller.Debug_RewindOne();
        }
        if (GUILayout.Button("Restart Run"))
        {
            controller.Debug_Restart();
        }
        if (GUILayout.Button("Next Day"))
        {
            controller.Debug_NextDay();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawJumpToEvent()
    {
        GUILayout.Label("<b>Jump to Event</b>", Rich());
        if (!gameData || gameData.Events == null || gameData.Events.Count == 0)
        {
            GUILayout.Label("No GameData / Events.");
            return;
        }

        // Filter
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter:", GUILayout.Width(48));
        var nf = GUILayout.TextField(_eventFilter ?? "", GUILayout.Width(160));
        if (nf != _eventFilter)
        {
            _eventFilter = nf;
            _eventIndex = 0;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Build filtered list
        var evts = string.IsNullOrWhiteSpace(_eventFilter)
            ? gameData.Events.Where(e => e != null).ToList()
            : gameData.Events.Where(e => e && e.name.IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        if (evts.Count == 0)
        {
            GUILayout.Label("No events match filter.");
            return;
        }

        _eventIndex = Mathf.Clamp(_eventIndex, 0, evts.Count - 1);
        var names = evts.Select(e => e.name).ToArray();
        _eventIndex = EditorLikePopup(_eventIndex, names);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Present"))
        {
            controller.Debug_JumpToEvent(evts[_eventIndex]);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawResources()
    {
        GUILayout.Label("<b>Resources</b>", Rich());

        var res = controller.Debug_Resources;
        if (res == null || res.Count == 0)
        {
            GUILayout.Label("(none)");
            return;
        }

        foreach (var kv in res)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(kv.Key, GUILayout.Width(160));

            if (!_resourceEdits.TryGetValue(kv.Key, out var buf))
                buf = kv.Value.ToString("0.###");
            var nb = GUILayout.TextField(buf, GUILayout.Width(80));
            if (nb != buf) _resourceEdits[kv.Key] = nb;

            if (GUILayout.Button("Set", GUILayout.Width(48)))
            {
                if (float.TryParse(nb, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var f)
                    || float.TryParse(nb, out f))
                {
                    controller.Debug_SetResource(kv.Key, f);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    private void DrawRelationships()
    {
        GUILayout.Label("<b>Relationships</b>", Rich());

        var rel = controller.Debug_Relationships;
        if (rel == null || rel.Count == 0)
        {
            GUILayout.Label("(none)");
            return;
        }

        foreach (var kv in rel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(kv.Key, GUILayout.Width(160));

            if (!_relationEdits.TryGetValue(kv.Key, out var buf))
                buf = kv.Value.ToString("0.###");
            var nb = GUILayout.TextField(buf, GUILayout.Width(80));
            if (nb != buf) _relationEdits[kv.Key] = nb;

            if (GUILayout.Button("Set", GUILayout.Width(48)))
            {
                if (float.TryParse(nb, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var f)
                    || float.TryParse(nb, out f))
                {
                    controller.Debug_SetRelationship(kv.Key, f);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    private void DrawFlags()
    {
        GUILayout.Label("<b>Flags</b>", Rich());

        var flags = controller.Debug_Flags;
        if (flags == null || flags.Count == 0)
        {
            GUILayout.Label("(none)");
        }
        else
        {
            foreach (var f in flags.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(f);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    controller.Debug_RemoveFlag(f);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        _newFlag = GUILayout.TextField(_newFlag, GUILayout.Width(180));
        if (GUILayout.Button("Add", GUILayout.Width(60)))
        {
            if (!string.IsNullOrWhiteSpace(_newFlag))
            {
                controller.Debug_AddFlag(_newFlag.Trim());
                _newFlag = "";
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GUIStyle Rich()
    {
        var st = new GUIStyle(GUI.skin.label) { richText = true };
        return st;
    }

    // Small popup that works in runtime (no EditorGUILayout.Popup)
    private static int EditorLikePopup(int index, string[] options, int maxVisible = 12)
    {
        if (options == null || options.Length == 0) return 0;
        index = Mathf.Clamp(index, 0, options.Length - 1);

        // Compact dropdown: show selected, cycle with buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(24))) index = (index - 1 + options.Length) % options.Length;
        GUILayout.Label(options[index], GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(24))) index = (index + 1) % options.Length;
        GUILayout.EndHorizontal();

        // (Optional) could draw a foldout list if needed
        return index;
    }
}
}