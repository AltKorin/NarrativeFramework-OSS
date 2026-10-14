using System.Collections.Generic;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame.Save
{
    /// <summary>
    /// Versioned save data container with migration support
    /// Handles backward compatibility and data structure changes
    /// </summary>
    [System.Serializable] 
    public class VersionedSaveData
    {
        public const int CURRENT_SAVE_VERSION = 2;
        
        [Header("Version Information")]
        public int version = CURRENT_SAVE_VERSION;
        public string gameVersion = Application.version;
        public string createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        [Header("Save Data")]
        public SaveData data;
        
        /// <summary>
        /// Creates a new versioned save data with current version
        /// </summary>
        public VersionedSaveData()
        {
            version = CURRENT_SAVE_VERSION;
            gameVersion = Application.version;
            createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data = new SaveData();
        }
        
        /// <summary>
        /// Creates versioned save data from existing SaveData
        /// </summary>
        public VersionedSaveData(SaveData saveData)
        {
            version = CURRENT_SAVE_VERSION;
            gameVersion = Application.version;
            createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data = saveData;
        }
        
        /// <summary>
        /// Attempts to load and migrate save data from JSON
        /// </summary>
        public static VersionedSaveData LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Log.Error("[VersionedSaveData] Cannot load from null or empty JSON");
                return null;
            }
            
            try
            {
                // Try to parse as versioned save first
                var versioned = JsonUtility.FromJson<VersionedSaveData>(json);
                
                if (versioned != null && versioned.data != null)
                {
                    return MigrateToCurrentVersion(versioned);
                }
                
                // Fallback: try to parse as legacy SaveData
                Log.Warning("[VersionedSaveData] Attempting to parse legacy save format");
                var legacyData = JsonUtility.FromJson<SaveData>(json);
                
                if (legacyData != null)
                {
                    return new VersionedSaveData
                    {
                        version = 1, // Assume version 1 for legacy data
                        gameVersion = "Legacy",
                        createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        data = legacyData
                    };
                }
                
                Log.Error("[VersionedSaveData] Failed to parse save data in any known format");
                return null;
            }
            catch (System.Exception e)
            {
                Log.Error($"[VersionedSaveData] Error loading save data: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Migrates save data to current version
        /// </summary>
        public static VersionedSaveData MigrateToCurrentVersion(VersionedSaveData saveData)
        {
            if (saveData == null)
            {
                Log.Error("[VersionedSaveData] Cannot migrate null save data");
                return null;
            }
            
            var workingData = saveData;
            int originalVersion = workingData.version;
            
            // Migrate through each version incrementally
            while (workingData.version < CURRENT_SAVE_VERSION)
            {
                workingData = MigrateToNextVersion(workingData);
                
                if (workingData == null)
                {
                    Log.Error($"[VersionedSaveData] Migration failed at version {workingData?.version ?? -1}");
                    return null;
                }
            }
            
            if (originalVersion != CURRENT_SAVE_VERSION)
            {
                Log.Info($"[VersionedSaveData] Successfully migrated save data from version {originalVersion} to {CURRENT_SAVE_VERSION}");
            }
            
            return workingData;
        }
        
        /// <summary>
        /// Migrates save data to the next version
        /// </summary>
        private static VersionedSaveData MigrateToNextVersion(VersionedSaveData saveData)
        {
            switch (saveData.version)
            {
                case 1:
                    return MigrateV1ToV2(saveData);
                    
                // Add future migration cases here
                // case 2:
                //     return MigrateV2ToV3(saveData);
                
                default:
                    Log.Error($"[VersionedSaveData] No migration path found from version {saveData.version}");
                    return null;
            }
        }
        
        /// <summary>
        /// Migrates version 1 save data to version 2
        /// Version 2 adds narrative boolean variables and story arc tracking
        /// </summary>
        private static VersionedSaveData MigrateV1ToV2(VersionedSaveData v1Data)
        {
            Log.Info("[VersionedSaveData] Migrating from version 1 to version 2");
            
            try
            {
                var v2Data = new VersionedSaveData
                {
                    version = 2,
                    gameVersion = Application.version,
                    createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    data = new SaveData
                    {
                        // Copy existing data
                        Version = 2,
                        SlotName = v1Data.data.SlotName,
                        SaveName = v1Data.data.SaveName,
                        CurrentDay = v1Data.data.CurrentDay,
                        Flags = v1Data.data.Flags ?? new List<string>(),
                        Resources = v1Data.data.Resources ?? new Dictionary<string, float>(),
                        Relationships = v1Data.data.Relationships ?? new Dictionary<string, float>(),
                        NarrativeVariables = v1Data.data.NarrativeVariables ?? new Dictionary<string, float>(),
                        CompletedEventIds = v1Data.data.CompletedEventIds ?? new List<string>(),
                        
                        // New in version 2: Add default values for new fields
                        NarrativeBooleans = new Dictionary<string, bool>(), // New field
                        ActiveStoryArcs = new List<string>(), // New field
                        CurrentEventId = "" // New field
                    }
                };
                
                // Attempt to preserve current event if it exists in v1 data
                if (!string.IsNullOrEmpty(v1Data.data.CurrentEventId))
                {
                    v2Data.data.CurrentEventId = v1Data.data.CurrentEventId;
                }
                
                Log.Info("[VersionedSaveData] Successfully migrated to version 2");
                return v2Data;
            }
            catch (System.Exception e)
            {
                Log.Error($"[VersionedSaveData] Error migrating v1 to v2: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Validates the save data structure
        /// </summary>
        public bool Validate()
        {
            if (data == null)
            {
                Log.Error("[VersionedSaveData] Save data is null");
                return false;
            }
            
            if (version < 1 || version > CURRENT_SAVE_VERSION)
            {
                Log.Error($"[VersionedSaveData] Invalid version: {version}");
                return false;
            }
            
            if (data.CurrentDay < 1)
            {
                Log.Error($"[VersionedSaveData] Invalid current day: {data.CurrentDay}");
                return false;
            }
            
            if (data.Flags == null)
            {
                Log.Warning("[VersionedSaveData] Flags list is null, initializing empty list");
                data.Flags = new List<string>();
            }
            
            if (data.Resources == null)
            {
                Log.Warning("[VersionedSaveData] Resources dictionary is null, initializing empty dictionary");
                data.Resources = new Dictionary<string, float>();
            }
            
            if (data.NarrativeVariables == null)
            {
                Log.Warning("[VersionedSaveData] NarrativeVariables dictionary is null, initializing empty dictionary");
                data.NarrativeVariables = new Dictionary<string, float>();
            }
            
            // Version-specific validation
            if (version >= 2)
            {
                if (data.NarrativeBooleans == null)
                {
                    Log.Warning("[VersionedSaveData] NarrativeBooleans dictionary is null (v2+), initializing empty dictionary");
                    data.NarrativeBooleans = new Dictionary<string, bool>();
                }
                
                if (data.ActiveStoryArcs == null)
                {
                    Log.Warning("[VersionedSaveData] ActiveStoryArcs list is null (v2+), initializing empty list");
                    data.ActiveStoryArcs = new List<string>();
                }
            }
            
            Log.Info($"[VersionedSaveData] Save data validation passed (version {version})");
            return true;
        }
        
        /// <summary>
        /// Converts to JSON string
        /// </summary>
        public string ToJson(bool prettyPrint = false)
        {
            try
            {
                return JsonUtility.ToJson(this, prettyPrint);
            }
            catch (System.Exception e)
            {
                Log.Error($"[VersionedSaveData] Error converting to JSON: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets debug information about this save data
        /// </summary>
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"VersionedSaveData Debug Info:");
            info.AppendLine($"  Version: {version} (Current: {CURRENT_SAVE_VERSION})");
            info.AppendLine($"  Game Version: {gameVersion}");
            info.AppendLine($"  Created: {createdDate}");
            info.AppendLine($"  Save Name: {data?.SaveName ?? "null"}");
            info.AppendLine($"  Slot Name: {data?.SlotName ?? "null"}");
            info.AppendLine($"  Current Day: {data?.CurrentDay ?? -1}");
            info.AppendLine($"  Flags Count: {data?.Flags?.Count ?? 0}");
            info.AppendLine($"  Resources Count: {data?.Resources?.Count ?? 0}");
            info.AppendLine($"  Narrative Variables Count: {data?.NarrativeVariables?.Count ?? 0}");
            
            if (version >= 2)
            {
                info.AppendLine($"  Narrative Booleans Count: {data?.NarrativeBooleans?.Count ?? 0}");
                info.AppendLine($"  Active Story Arcs Count: {data?.ActiveStoryArcs?.Count ?? 0}");
                info.AppendLine($"  Current Event ID: {data?.CurrentEventId ?? "null"}");
            }
            
            return info.ToString();
        }
    }
}