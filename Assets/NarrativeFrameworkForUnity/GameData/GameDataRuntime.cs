using System.Reflection;
using UnityEngine;

namespace SimulationGame
{

    public static class GameDataRuntime
    {
        /// <summary>Switch the global active GameData used by runtime systems.</summary>
        public static void SetActive(GameData gd)
        {
            // Prefer a public setter if it exists
            var t = typeof(GameData);

            // Try property 'Instance'
            var prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(null, gd);
                return;
            }

            // Try common backing field names
            var field = t.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static)
                     ?? t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);

            if (field != null)
            {
                field.SetValue(null, gd);
                return;
            }

            // Fallback: if GameData already exposes a custom setter, call it by name
            var method = t.GetMethod("SetActive", BindingFlags.Public | BindingFlags.Static)
                      ?? t.GetMethod("SetInstance", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, new object[] { gd });
                return;
            }

            UnityEngine.Debug.LogWarning("GameDataRuntime.SetActive: could not set GameData.Instance (no setter/field found).");
        }
    }
}