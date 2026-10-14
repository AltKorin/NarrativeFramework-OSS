using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Diagnostics;
using SimulationGame.Interfaces;
using SimulationGame.DebugTools;

namespace SimulationGame.Save
{
    /// <summary>
    /// Async save manager that prevents frame drops during save/load operations
    /// Uses Unity's async/await pattern for smooth performance
    /// </summary>
    public class AsyncSaveManager : MonoBehaviour, ISaveManager
    {
        [Header("Save Settings")]
        [SerializeField] private string _saveDirectory = "SaveData";
        [SerializeField] private string _fileExtension = ".json";
        [SerializeField] private bool _enableBackups = true;
        [SerializeField] private int _maxBackupFiles = 3;
        [SerializeField] private bool _enableDebugLogging = false;

        [Header("Performance Settings")]
        

        private readonly Dictionary<string, Task> _activeOperations = new();
        private readonly object _operationLock = new object();

        public bool IsOperationInProgress(string slotName)
        {
            lock (_operationLock)
            {
                return _activeOperations.ContainsKey(slotName) && !_activeOperations[slotName].IsCompleted;
            }
        }

        public async void SaveGame(ISaveDataProvider provider)
        {
            if (provider == null)
            {
                Log.Error("[AsyncSaveManager] Cannot save - provider is null");
                return;
            }

            string slotName = provider.SlotName ?? "default";

            if (IsOperationInProgress(slotName))
            {
                Log.Warning($"[AsyncSaveManager] Save operation already in progress for slot: {slotName}");
                return;
            }

            try
            {
                var saveTask = SaveGameAsync(provider);

                lock (_operationLock)
                {
                    _activeOperations[slotName] = saveTask;
                }

                await saveTask;
            }
            catch (Exception e)
            {
                Log.Error($"[AsyncSaveManager] Save operation failed: {e.Message}");
            }
            finally
            {
                lock (_operationLock)
                {
                    _activeOperations.Remove(slotName);
                }
            }
        }

        public async void LoadGame(ISaveDataReceiver receiver)
        {
            if (receiver == null)
            {
                Log.Error("[AsyncSaveManager] Cannot load - receiver is null");
                return;
            }

            string slotName = "default";

            if (IsOperationInProgress(slotName))
            {
                Log.Warning($"[AsyncSaveManager] Load operation already in progress for slot: {slotName}");
                return;
            }

            try
            {
                var loadTask = LoadGameAsync(slotName);

                lock (_operationLock)
                {
                    _activeOperations[slotName] = loadTask;
                }

                var saveData = await loadTask;

                if (saveData != null && saveData.data != null)
                {
                    ApplyLoadedData(receiver, saveData.data);
                }
                else
                {
                    Log.Error($"[AsyncSaveManager] Failed to load valid save data from slot: {slotName}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[AsyncSaveManager] Load operation failed: {e.Message}");
            }
            finally
            {
                lock (_operationLock)
                {
                    _activeOperations.Remove(slotName);
                }
            }
        }

        public bool HasSave(string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
                return false;

            string filePath = GetSaveFilePath(saveName);
            return File.Exists(filePath);
        }

        public void DeleteSave(string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                Log.Error("[AsyncSaveManager] Cannot delete save - saveName is null or empty");
                return;
            }

            if (IsOperationInProgress(saveName))
            {
                Log.Warning($"[AsyncSaveManager] Cannot delete save - operation in progress for slot: {saveName}");
                return;
            }

            try
            {
                string filePath = GetSaveFilePath(saveName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    if (_enableDebugLogging)
                        Log.Info($"[AsyncSaveManager] Deleted save file: {filePath}");
                }
                else
                {
                    Log.Warning($"[AsyncSaveManager] Save file not found for deletion: {filePath}");
                }

                if (_enableBackups)
                {
                    DeleteBackupFiles(saveName);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[AsyncSaveManager] Failed to delete save '{saveName}': {e.Message}");
            }
        }

        private void DeleteBackupFiles(string saveName)
        {
            try
            {
                string saveDir = GetSaveDirectory();
                string backupPattern = $"{saveName}_backup_*{_fileExtension}";
                var backupFiles = Directory.GetFiles(saveDir, backupPattern);

                foreach (var backupFile in backupFiles)
                {
                    File.Delete(backupFile);
                    if (_enableDebugLogging)
                        Log.Info($"[AsyncSaveManager] Deleted backup file: {backupFile}");
                }

                if (backupFiles.Length > 0 && _enableDebugLogging)
                    Log.Info($"[AsyncSaveManager] Deleted {backupFiles.Length} backup files for save: {saveName}");
            }
            catch (Exception e)
            {
                Log.Warning($"[AsyncSaveManager] Failed to delete backup files for '{saveName}': {e.Message}");
            }
        }

        private async Task SaveGameAsync(ISaveDataProvider provider)
        {
            if (_enableDebugLogging)
                Log.Info($"[AsyncSaveManager] Starting save operation for: {provider.SaveName}");

            try
            {
                var saveData = CreateSaveData(provider);
                var versionedData = new VersionedSaveData(saveData);

                if (!versionedData.Validate())
                {
                    Log.Error("[AsyncSaveManager] Save data validation failed");
                    return;
                }

                string json = await Task.Run(() => versionedData.ToJson(prettyPrint: true));

                if (string.IsNullOrEmpty(json))
                {
                    Log.Error("[AsyncSaveManager] Failed to serialize save data to JSON");
                    return;
                }

                if (_enableBackups)
                {
                    await CreateBackupAsync(provider.SaveName);
                }

                string filePath = GetSaveFilePath(provider.SaveName);
                await WriteFileAsync(filePath, json);

                if (_enableDebugLogging)
                    Log.Info($"[AsyncSaveManager] Successfully saved game to: {filePath}");
            }
            catch (Exception e)
            {
                Log.Error($"[AsyncSaveManager] Save operation failed: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        private async Task<VersionedSaveData> LoadGameAsync(string saveName)
        {
            if (_enableDebugLogging)
                Log.Info($"[AsyncSaveManager] Starting load operation for: {saveName}");

            try
            {
                string filePath = GetSaveFilePath(saveName);

                if (!File.Exists(filePath))
                {
                    Log.Warning($"[AsyncSaveManager] Save file not found: {filePath}");
                    return null;
                }

                string json = await ReadFileAsync(filePath);

                if (string.IsNullOrEmpty(json))
                {
                    Log.Error($"[AsyncSaveManager] Save file is empty or corrupted: {filePath}");
                    return null;
                }

                var versionedData = await Task.Run(() => VersionedSaveData.LoadFromJson(json));

                if (versionedData == null)
                {
                    Log.Error($"[AsyncSaveManager] Failed to parse save data from: {filePath}");
                    return null;
                }

                if (!versionedData.Validate())
                {
                    Log.Error($"[AsyncSaveManager] Loaded save data validation failed: {filePath}");
                    return null;
                }

                if (_enableDebugLogging)
                    Log.Info($"[AsyncSaveManager] Successfully loaded game from: {filePath}");

                return versionedData;
            }
            catch (Exception e)
            {
                Log.Error($"[AsyncSaveManager] Load operation failed: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        private async Task CreateBackupAsync(string saveName)
        {
            try
            {
                string originalPath = GetSaveFilePath(saveName);

                if (!File.Exists(originalPath))
                    return;

                string backupPath = GetBackupFilePath(saveName);
                string content = await ReadFileAsync(originalPath);
                await WriteFileAsync(backupPath, content);
                await CleanupOldBackupsAsync(saveName);

                if (_enableDebugLogging)
                    Log.Info($"[AsyncSaveManager] Created backup: {backupPath}");
            }
            catch (Exception e)
            {
                Log.Warning($"[AsyncSaveManager] Failed to create backup: {e.Message}");
            }
        }

        private async Task CleanupOldBackupsAsync(string saveName)
        {
            await Task.Run(() =>
            {
                try
                {
                    string saveDir = GetSaveDirectory();
                    string backupPattern = $"{saveName}_backup_*{_fileExtension}";
                    var backupFiles = Directory.GetFiles(saveDir, backupPattern);
                    Array.Sort(backupFiles);
                    int filesToRemove = backupFiles.Length - _maxBackupFiles;

                    for (int i = 0; i < filesToRemove; i++)
                    {
                        File.Delete(backupFiles[i]);
                        if (_enableDebugLogging)
                            Log.Info($"[AsyncSaveManager] Removed old backup: {backupFiles[i]}");
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"[AsyncSaveManager] Failed to cleanup old backups: {e.Message}");
                }
            });
        }

        private async Task<string> ReadFileAsync(string filePath)
        {
#if UNITY_2021_2_OR_NEWER
            return await File.ReadAllTextAsync(filePath);
#else
            return await Task.Run(() => File.ReadAllText(filePath));
#endif
        }

        private async Task WriteFileAsync(string filePath, string content)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

#if UNITY_2021_2_OR_NEWER
            await File.WriteAllTextAsync(filePath, content);
#else
            await Task.Run(() => File.WriteAllText(filePath, content));
#endif
        }

        private SaveData CreateSaveData(ISaveDataProvider provider)
        {
            return new SaveData
            {
                Version = VersionedSaveData.CURRENT_SAVE_VERSION,
                SlotName = provider.SlotName,
                SaveName = provider.SaveName,
                CurrentDay = provider.CurrentDay,
                Flags = new List<string>(provider.Flags),
                Resources = new Dictionary<string, float>(provider.Resources),
                Relationships = new Dictionary<string, float>(provider.Relationships),
                NarrativeVariables = new Dictionary<string, float>(provider.NarrativeVariables),
                NarrativeBooleans = new Dictionary<string, bool>(provider.NarrativeBooleans),
                ActiveStoryArcs = new List<string>(provider.ActiveStoryArcs),
                CurrentEventId = provider.CurrentEventId,
                CompletedEventIds = new List<string>()
            };
        }

        private void ApplyLoadedData(ISaveDataReceiver receiver, SaveData saveData)
        {
            receiver.ApplySaveData(
                saveData.CurrentDay,
                new HashSet<string>(saveData.Flags ?? new List<string>()),
                saveData.Resources ?? new Dictionary<string, float>(),
                saveData.Relationships ?? new Dictionary<string, float>(),
                saveData.NarrativeVariables ?? new Dictionary<string, float>(),
                saveData.NarrativeBooleans ?? new Dictionary<string, bool>(),
                saveData.ActiveStoryArcs ?? new List<string>(),
                saveData.CurrentEventId ?? "",
                saveData.SlotName ?? "default",
                saveData.SaveName ?? "Unknown"
            );
        }

        private string GetSaveFilePath(string saveName)
        {
            return Path.Combine(GetSaveDirectory(), $"{saveName}{_fileExtension}");
        }

        private string GetBackupFilePath(string saveName)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(GetSaveDirectory(), $"{saveName}_backup_{timestamp}{_fileExtension}");
        }

        private string GetSaveDirectory()
        {
            string path = Path.Combine(Application.persistentDataPath, _saveDirectory);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("AsyncSaveManager Debug Info:");
            info.AppendLine($"  Save Directory: {GetSaveDirectory()}");
            info.AppendLine($"  File Extension: {_fileExtension}");
            info.AppendLine($"  Backups Enabled: {_enableBackups}");
            info.AppendLine($"  Max Backup Files: {_maxBackupFiles}");
            info.AppendLine($"  Active Operations: {_activeOperations.Count}");

            lock (_operationLock)
            {
                foreach (var op in _activeOperations)
                {
                    info.AppendLine($"    {op.Key}: {op.Value.Status}");
                }
            }

            return info.ToString();
        }

        private void OnDestroy()
        {
            var pendingTasks = new List<Task>();

            lock (_operationLock)
            {
                foreach (var operation in _activeOperations.Values)
                {
                    if (!operation.IsCompleted)
                    {
                        pendingTasks.Add(operation);
                    }
                }
            }

            if (pendingTasks.Count > 0)
            {
                Log.Info($"[AsyncSaveManager] Waiting for {pendingTasks.Count} operations to complete...");
                Task.WaitAll(pendingTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
        }
    }
}
