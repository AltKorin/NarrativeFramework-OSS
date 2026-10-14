#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using SimulationGame; // GameController, SimEventSO, GameData

namespace SimulationGame.EditorTools
{
    /// <summary>
    /// Editor menu & hotkeys to control the runtime NarrativeDebugger without changing core code.
    /// </summary>
    public static class NarrativeDebuggerMenu
    {
        private const string MenuRoot = "Tools/NarrativeDebugger/";

        // ===== Public menu items (with hotkeys) =====
        [MenuItem(MenuRoot + "Toggle Debugger %#d")]  // Ctrl/Cmd + Shift + D
        public static void ToggleDebugger() => EnsurePlayAndRun(() => ToggleVisible(EnsureDebugger()));

        [MenuItem(MenuRoot + "Show Debugger &%d")]    // Alt + Shift + D
        public static void ShowDebugger()   => EnsurePlayAndRun(() => SetVisible(EnsureDebugger(), true));

        [MenuItem(MenuRoot + "Hide Debugger &%h")]    // Alt + Shift + H
        public static void HideDebugger()   => EnsurePlayAndRun(() => SetVisible(EnsureDebugger(), false));

        [MenuItem(MenuRoot + "Rewind One  &%r")]      // Alt + Shift + R
        public static void RewindOne() => EnsurePlayAndRun(() => TryCallOnGC("RequestRewind"));

        [MenuItem(MenuRoot + "Restart Run &%s")]      // Alt + Shift + S
        public static void RestartRun() => EnsurePlayAndRun(() => TryCallOnGC("RestartRun"));

        [MenuItem(MenuRoot + "Next Day    &%n")]      // Alt + Shift + N
        public static void NextDay() => EnsurePlayAndRun(() => TryCallOnGC("AdvanceDay"));

        [MenuItem(MenuRoot + "Present Selected Event &%p")] // Alt + Shift + P
        public static void PresentSelectedEvent() => EnsurePlayAndRun(() =>
        {
            var ev = Selection.activeObject as SimEventSO;
            if (!ev) { EditorUtility.DisplayDialog("Narrative", "Select a SimEventSO asset in Project.", "OK"); return; }
            TryPresentEvent(ev);
        });

        [MenuItem(MenuRoot + "Present Selected Event &%p", true)]
        private static bool ValidatePresentSelectedEvent()
            => Selection.activeObject is SimEventSO;

        // ===== Core helpers =====

        /// <summary>Ensure we're in Play Mode; if not, enter Play and run the action when ready.</summary>
        private static void EnsurePlayAndRun(Action action)
        {
            if (EditorApplication.isPlaying)
            {
                action?.Invoke();
                return;
            }

            // Schedule after entering play mode
            void Handler(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    EditorApplication.playModeStateChanged -= Handler;
                    // a small delay ensures scene is initialized
                    EditorApplication.delayCall += () => action?.Invoke();
                }
            }
            EditorApplication.playModeStateChanged += Handler;
            EditorApplication.isPlaying = true;
        }

        /// <summary>Find or create the NarrativeDebugger in the running scene (DontDestroyOnLoad).</summary>
        private static Component EnsureDebugger()
        {
            var dbg = FindRuntimeComponent("SimulationGame.DebugTools.NarrativeDebugger");
            if (dbg) return dbg;

            var go = new GameObject("NarrativeDebugger (Runtime)");
            // do NOT hide in hierarchy so you can select it while testing
            UnityEngine.Object.DontDestroyOnLoad(go);

            var type = Type.GetType("SimulationGame.DebugTools.NarrativeDebugger, Assembly-CSharp");
            if (type == null) { Debug.LogWarning("[Narrative] NarrativeDebugger type not found."); return null; }

            dbg = go.AddComponent(type);
            // Try to start open in editor
            SafeSetField(dbg, "startOpenInEditor", true);
            SafeSetField(dbg, "_visible", true);
            return dbg;
        }

        /// <summary>Find the first active NarrativeDebugger component by type name.</summary>
        private static Component FindRuntimeComponent(string qualifiedTypeName)
        {
            var type = Type.GetType($"{qualifiedTypeName}, Assembly-CSharp");
            if (type == null) return null;

            // Unity 2021+ has FindFirstObjectByType; fallback to FindObjectOfType on older.
#if UNITY_2022_2_OR_NEWER
            return (Component)UnityEngine.Object.FindFirstObjectByType(type);
#else
            foreach (var obj in UnityEngine.Object.FindObjectsOfType(type))
                return obj as Component;
            return null;
#endif
        }

        /// <summary>Toggle debugger's private _visible flag via reflection.</summary>
        private static void ToggleVisible(Component dbg)
        {
            if (!dbg) return;
            var f = dbg.GetType().GetField("_visible", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) { Debug.LogWarning("[Narrative] _visible field not found on NarrativeDebugger."); return; }
            bool cur = (bool)f.GetValue(dbg);
            f.SetValue(dbg, !cur);
        }

        /// <summary>Set debugger's private _visible flag via reflection.</summary>
        private static void SetVisible(Component dbg, bool v)
        {
            if (!dbg) return;
            var f = dbg.GetType().GetField("_visible", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) { Debug.LogWarning("[Narrative] _visible field not found on NarrativeDebugger."); return; }
            f.SetValue(dbg, v);
        }

        /// <summary>Call a public/private method on GameController via reflection.</summary>
        private static bool TryCallOnGC(string methodName)
        {
            var gc = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (!gc) { Debug.LogWarning("[Narrative] GameController not found in scene."); return false; }

            var mi = typeof(GameController).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) { Debug.LogWarning($"[Narrative] Method not found: {methodName}"); return false; }

            mi.Invoke(gc, null);
            return true;
        }

        /// <summary>Present a SimEventSO using GameController.PresentEvent (even if it's private).</summary>
        private static bool TryPresentEvent(SimEventSO ev)
        {
            var gc = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (!gc) { Debug.LogWarning("[Narrative] GameController not found in scene."); return false; }
            if (!ev) { Debug.LogWarning("[Narrative] SimEventSO is null."); return false; }

            var mi = typeof(GameController).GetMethod("PresentEvent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(SimEventSO) }, null);

            if (mi == null) { Debug.LogWarning("[Narrative] PresentEvent(SimEventSO) not found."); return false; }
            mi.Invoke(gc, new object[] { ev });
            return true;
        }

        private static void SafeSetField(Component c, string field, object value)
        {
            if (!c) return;
            var fi = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType.IsAssignableFrom(value?.GetType())) fi.SetValue(c, value);
        }
    }
}
#endif
