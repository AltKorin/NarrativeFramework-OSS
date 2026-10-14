// Assets/SimulationGame/Runtime/Scripts/Managers/SaveManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using SimulationGame.Interfaces;

namespace SimulationGame
{
    /// <summary>
    /// JSON save/load manager.
    /// - Interface-required methods (no slot): SaveGame(provider), LoadGame(receiver), DeleteSave(name), HasSave(name).
    /// - Extra slot-based helpers: SaveGame(provider, slot), LoadSave(slot, receiver), DeleteSaveSlot(slot), ListSaves().
    /// - File layout:
    ///     autosave  -> Saves/save.json
    ///     other     -> Saves/save_{slot}.json
    /// Uses reflection to stay compatible with existing ISaveDataProvider/Receiver without changing their signatures.
    /// </summary>
    public class SaveManager : MonoBehaviour, ISaveManager
    {
        [Header("File Settings")]
        [SerializeField] private string _saveFolderName = "Saves";
        [SerializeField] private bool _prettyPrint = true;

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, _saveFolderName);

        private void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }

        private static string SanitizeSlotName(string saveName)
        {
            if (string.IsNullOrWhiteSpace(saveName))
                return "autosave";
            var sb = new StringBuilder(saveName.Length);
            foreach (char ch in saveName)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString();
        }

        private string ComposeFileName(string saveName)
        {
            if (string.IsNullOrWhiteSpace(saveName) ||
                string.Equals(saveName, "autosave", StringComparison.OrdinalIgnoreCase))
                return "save.json";
            return $"save_{SanitizeSlotName(saveName)}.json";
        }

        private string ComposeFilePath(string saveName) =>
            Path.Combine(SaveDirectory, ComposeFileName(saveName));

        // ─────────────────────────────────────────────────────────────────────
        // Interface-required API (no-slot)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>ISaveManager: save to autosave.</summary>
        public void SaveGame(ISaveDataProvider provider)
        {
            SaveGame(provider, "autosave");
        }

        /// <summary>ISaveManager: load from autosave. Calls receiver.ApplySaveData(...).</summary>
        public void LoadGame(ISaveDataReceiver receiver)
        {
            LoadSave("autosave", receiver);
        }

        /// <summary>ISaveManager: delete a save by name (void per interface).</summary>
        public void DeleteSave(string saveName)
        {
            _ = DeleteSaveSlot(saveName);
        }

        /// <summary>ISaveManager: check if a save exists.</summary>
        public bool HasSave(string saveName)
        {
            EnsureDirectory();
            string path = ComposeFilePath(saveName);
            return File.Exists(path);
        }

        // Explicit interface implementation to guarantee signature match.
        bool ISaveManager.HasSave(string saveName) => HasSave(saveName);

        // ─────────────────────────────────────────────────────────────────────
        // Extended API (slot-aware) — non-breaking helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Save to a named slot or autosave.</summary>
        public void SaveGame(ISaveDataProvider provider, string saveName)
        {
            EnsureDirectory();

            if (provider == null)
            {
                Debug.LogWarning("[SaveManager] SaveGame called with null provider.");
                return;
            }

            string path = ComposeFilePath(saveName);
            try
            {
                // Prefer a direct GetSaveData() if present (any return type).
                object payload = TryInvokeGetSaveData(provider);

                // If not available, build a shim from common provider members.
                if (payload == null)
                    payload = BuildShimFromProvider(provider, saveName);

                if (payload == null)
                {
                    Debug.LogError("[SaveManager] Could not obtain save data from provider.");
                    return;
                }

                string json = JsonUtility.ToJson(payload, _prettyPrint);
                File.WriteAllText(path, json, Encoding.UTF8);
#if UNITY_EDITOR
                Debug.Log($"[SaveManager] Saved '{saveName}' → {path}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Error saving '{saveName}' to '{path}': {ex.Message}");
            }
        }

        /// <summary>Load a named slot or autosave; returns true on success.</summary>
        public bool LoadSave(string saveName, ISaveDataReceiver receiver)
        {
            EnsureDirectory();

            if (receiver == null)
            {
                Debug.LogWarning("[SaveManager] LoadSave called with null receiver.");
                return false;
            }

            string path = ComposeFilePath(saveName);
            if (!File.Exists(path))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SaveManager] Save '{saveName}' not found at {path}.");
#endif
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);

                // Deserialize to shim (unknown concrete type otherwise).
                var shim = JsonUtility.FromJson<SaveDataShim>(json);
                if (shim == null)
                {
                    Debug.LogError("[SaveManager] Failed to deserialize save data shim.");
                    return false;
                }

                // Normalize nulls
                if (shim.Flags == null) shim.Flags = new List<string>();
                shim.Resources ??= new Dictionary<string, float>();
                shim.Relationships ??= new Dictionary<string, float>();
                shim.Variables ??= new Dictionary<string, float>();
                shim.BoolVariables ??= new Dictionary<string, bool>();
                shim.Achievements ??= new List<string>();

                // Try to call the receiver's ApplySaveData(...) with the exact signature from your error.
                if (!TryApplyShimToReceiver(receiver, shim))
                {
                    Debug.LogError("[SaveManager] Receiver.ApplySaveData(...) with the expected signature was not found.");
                    return false;
                }

#if UNITY_EDITOR
                Debug.Log($"[SaveManager] Loaded '{saveName}' from {path}");
#endif
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Error loading '{saveName}' from '{path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>Delete a named slot; returns true if a file was deleted.</summary>
        public bool DeleteSaveSlot(string saveName)
        {
            EnsureDirectory();
            string path = ComposeFilePath(saveName);
            if (!File.Exists(path))
                return false;

            try
            {
                File.Delete(path);
#if UNITY_EDITOR
                Debug.Log($"[SaveManager] Deleted '{saveName}' at {path}");
#endif
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Error deleting '{saveName}' at '{path}': {ex.Message}");
                return false;
            }
        }

        public string[] ListSaves()
        {
            EnsureDirectory();
            var files = Directory.GetFiles(SaveDirectory, "save*.json");
            var list = new List<string>();

            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                if (name.Equals("save.json", StringComparison.OrdinalIgnoreCase))
                    list.Add("autosave");
                else if (name.StartsWith("save_") && name.EndsWith(".json"))
                    list.Add(name.Substring(5, name.Length - 5 - 5));
            }
            return list.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reflection-based compatibility layer
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Try provider.GetSaveData() by reflection. Works with any return type.
        /// </summary>
        private static object TryInvokeGetSaveData(ISaveDataProvider provider)
        {
            var t = provider.GetType();
            var m = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .FirstOrDefault(x => x.Name == "GetSaveData" && x.GetParameters().Length == 0);
            if (m != null)
            {
                try { return m.Invoke(provider, null); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveManager] GetSaveData() invocation failed: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Build a minimal, serializable snapshot from common provider members.
        /// This does not change your provider API; we only read what exists.
        /// </summary>
        private static SaveDataShim BuildShimFromProvider(ISaveDataProvider provider, string saveName)
        {
            var shim = new SaveDataShim { SaveName = saveName };

            // Day
            shim.Day = TryRead<int>(provider, "CurrentDay", "Day", "GetCurrentDay");

            // Flags -> IEnumerable<string> → List<string>
            var flagsEnum = TryReadObj(provider, new[] { "Flags", "GetFlags", "CurrentFlags" }) as System.Collections.IEnumerable;
            if (flagsEnum != null)
            {
                shim.Flags = new List<string>();
                foreach (var it in flagsEnum)
                {
                    if (it is string s) shim.Flags.Add(s);
                }
            }
            else
            {
                shim.Flags = new List<string>();
            }

            // Resources -> Dictionary<string,float>
            shim.Resources = TryReadDictFloat(provider, "Resources", "GetResources", "CurrentResources")
                             ?? new Dictionary<string, float>();

            // Relationships -> Dictionary<string,float> (if present)
            shim.Relationships = TryReadDictFloat(provider, "Relationships", "GetRelationships", "CurrentRelationships")
                                 ?? new Dictionary<string, float>();

            // Variables float
            shim.Variables = TryReadDictFloat(provider, "Variables", "GetVariables", "CurrentVariables")
                             ?? new Dictionary<string, float>();

            // Variables bool
            shim.BoolVariables = TryReadDictBool(provider, "BoolVariables", "GetBoolVariables", "CurrentBoolVariables")
                                 ?? new Dictionary<string, bool>();

            // Achievements list
            var achEnum = TryReadObj(provider, new[] { "Achievements", "GetAchievements" }) as System.Collections.IEnumerable;
            if (achEnum != null)
            {
                shim.Achievements = new List<string>();
                foreach (var it in achEnum)
                {
                    if (it is string s) shim.Achievements.Add(s);
                }
            }
            else
            {
                shim.Achievements = new List<string>();
            }

            // Player actor id
            shim.PlayerActorId = TryRead<string>(provider, "PlayerActorId", "GetPlayerActorId");

            // Current event id
            shim.CurrentEventId = TryRead<string>(provider, "CurrentEventId", "GetCurrentEventId");

            return shim;
        }

        /// <summary>
        /// Call receiver.ApplySaveData(int, HashSet&lt;string&gt;, Dictionary&lt;string,float&gt;,
        /// Dictionary&lt;string,float&gt;, Dictionary&lt;string,float&gt;, Dictionary&lt;string,bool&gt;,
        /// List&lt;string&gt;, string, string, string) via reflection.
        /// </summary>
        private static bool TryApplyShimToReceiver(ISaveDataReceiver receiver, SaveDataShim shim)
        {
            var recvType = receiver.GetType();

            var wantedParamTypes = new[]
            {
                typeof(int),
                typeof(HashSet<string>),
                typeof(Dictionary<string, float>),
                typeof(Dictionary<string, float>),
                typeof(Dictionary<string, float>),
                typeof(Dictionary<string, bool>),
                typeof(List<string>),
                typeof(string),
                typeof(string),
                typeof(string)
            };

            var method = recvType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                 .FirstOrDefault(m =>
                                 {
                                     if (m.Name != "ApplySaveData") return false;
                                     var ps = m.GetParameters();
                                     if (ps.Length != wantedParamTypes.Length) return false;
                                     for (int i = 0; i < ps.Length; i++)
                                         if (ps[i].ParameterType != wantedParamTypes[i]) return false;
                                     return true;
                                 });

            if (method == null) return false;

            var flagsSet = new HashSet<string>(shim.Flags ?? new List<string>());

            var args = new object[]
            {
                shim.Day,
                flagsSet,
                shim.Resources ?? new Dictionary<string, float>(),
                shim.Relationships ?? new Dictionary<string, float>(),
                shim.Variables ?? new Dictionary<string, float>(),
                shim.BoolVariables ?? new Dictionary<string, bool>(),
                shim.Achievements ?? new List<string>(),
                shim.PlayerActorId ?? string.Empty,
                shim.CurrentEventId ?? string.Empty,
                shim.SaveName ?? "autosave"
            };

            try
            {
                method.Invoke(receiver, args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] ApplySaveData invocation failed: {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reflection utilities
        // ─────────────────────────────────────────────────────────────────────

        private static T TryRead<T>(object obj, params string[] names)
        {
            if (obj == null) return default;
            var t = obj.GetType();

            // Properties
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(T))
                    return (T)p.GetValue(obj);
            }

            // Parameterless methods
            foreach (var n in names)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(T))
                    return (T)m.Invoke(obj, null);
            }

            return default;
        }

        private static object TryReadObj(object obj, string[] names)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null) return p.GetValue(obj);
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null) return m.Invoke(obj, null);
            }
            return null;
        }

        private static Dictionary<string, float> TryReadDictFloat(object obj, params string[] names)
        {
            var o = TryReadObj(obj, names);
            if (o == null) return null;

            if (o is Dictionary<string, float> d1) return d1;

            // Try IDictionary<string, float> → copy into Dictionary
            var idictType = o.GetType().GetInterfaces()
                              .FirstOrDefault(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                                                   i.GetGenericArguments()[0] == typeof(string) &&
                                                   i.GetGenericArguments()[1] == typeof(float));
            if (idictType != null)
            {
                var result = new Dictionary<string, float>();
                var enumerable = (System.Collections.IEnumerable)o;
                foreach (var entry in enumerable)
                {
                    var keyProp = entry.GetType().GetProperty("Key");
                    var valProp = entry.GetType().GetProperty("Value");
                    if (keyProp != null && valProp != null)
                    {
                        var key = keyProp.GetValue(entry) as string;
                        var val = (float)Convert.ChangeType(valProp.GetValue(entry), typeof(float));
                        if (key != null) result[key] = val;
                    }
                }
                return result;
            }

            return null;
        }

        private static Dictionary<string, bool> TryReadDictBool(object obj, params string[] names)
        {
            var o = TryReadObj(obj, names);
            if (o == null) return null;

            if (o is Dictionary<string, bool> d1) return d1;

            var idictType = o.GetType().GetInterfaces()
                              .FirstOrDefault(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                                                   i.GetGenericArguments()[0] == typeof(string) &&
                                                   i.GetGenericArguments()[1] == typeof(bool));
            if (idictType != null)
            {
                var result = new Dictionary<string, bool>();
                var enumerable = (System.Collections.IEnumerable)o;
                foreach (var entry in enumerable)
                {
                    var keyProp = entry.GetType().GetProperty("Key");
                    var valProp = entry.GetType().GetProperty("Value");
                    if (keyProp != null && valProp != null)
                    {
                        var key = keyProp.GetValue(entry) as string;
                        var val = (bool)Convert.ChangeType(valProp.GetValue(entry), typeof(bool));
                        if (key != null) result[key] = val;
                    }
                }
                return result;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Data shim (serializable minimal snapshot)
        // ─────────────────────────────────────────────────────────────────────

        [Serializable]
        private class SaveDataShim
        {
            public int Day;
            public List<string> Flags;
            public Dictionary<string, float> Resources;
            public Dictionary<string, float> Relationships;
            public Dictionary<string, float> Variables;
            public Dictionary<string, bool> BoolVariables;
            public List<string> Achievements;

            public string PlayerActorId;
            public string CurrentEventId;

            public string SaveName;
        }
    }
}
