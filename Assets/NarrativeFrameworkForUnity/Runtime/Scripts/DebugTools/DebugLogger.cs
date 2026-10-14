using UnityEngine;

namespace SimulationGame.DebugTools
{
    /// <summary>
    /// Centralized debug logging utility for the narrative system
    /// Provides consistent logging with prefixes and conditional compilation
    /// </summary>
    public static class Log
    {
        private const string PREFIX = "[NarrativeSystem]";
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            UnityEngine.Debug.Log($"{PREFIX} {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(string message)
        {
            UnityEngine.Debug.LogWarning($"{PREFIX} {message}");
        }
        
        public static void Error(string message)
        {
            UnityEngine.Debug.LogError($"{PREFIX} {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Format(string format, params object[] args)
        {
            UnityEngine.Debug.LogFormat($"{PREFIX} {format}", args);
        }
    }
}