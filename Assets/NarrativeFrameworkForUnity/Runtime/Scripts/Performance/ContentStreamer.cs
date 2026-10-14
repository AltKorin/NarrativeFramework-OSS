using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame.Performance
{
    /// <summary>
    /// Manages memory-efficient loading and caching of narrative content
    /// Prevents memory overload by streaming events on-demand
    /// </summary>
    public class ContentStreamer : MonoBehaviour
    {
        [Header("Cache Settings")]
        [SerializeField] private int maxCachedEvents = 50;
        [SerializeField] private int maxCachedChoices = 100;
        [SerializeField] private int maxCachedOutcomes = 200;
        [SerializeField] private bool _enableDebugLogging = false;
        
        [Header("Preload Settings")]
        [SerializeField] private int _preloadRadius = 2; // Days around current day to preload
        [SerializeField] private bool _preloadLinkedEvents = true;
        
        private Dictionary<string, SimEventSO> _eventCache = new();
        private Dictionary<string, Choice> _choiceCache = new();
        private Dictionary<string, Outcome> _outcomeCache = new();
        
        private Queue<string> _eventCacheOrder = new();
        private Queue<string> _choiceCacheOrder = new();
        private Queue<string> _outcomeCacheOrder = new();
        
        private int _currentDay = 1;
        private HashSet<string> _preloadedEventIds = new();
        
        public static ContentStreamer Instance { get; private set; }
        
        public int CachedEventCount => _eventCache.Count;
        public int CachedChoiceCount => _choiceCache.Count;
        public int CachedOutcomeCount => _outcomeCache.Count;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Gets an event by ID with streaming and caching
        /// </summary>
        public SimEventSO GetEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Log.Warning("[ContentStreamer] GetEvent called with null/empty eventId");
                return null;
            }
            
            // Check cache first
            if (_eventCache.TryGetValue(eventId, out var cachedEvent))
            {
                if (_enableDebugLogging)
                    Log.Info($"[ContentStreamer] Retrieved event from cache: {eventId}");
                return cachedEvent;
            }
            
            // Load from GameData
            var evt = LoadEventFromGameData(eventId);
            if (evt != null)
            {
                CacheEvent(eventId, evt);
                
                if (_enableDebugLogging)
                    Log.Info($"[ContentStreamer] Loaded and cached event: {eventId}");
            }
            else
            {
                Log.Warning($"[ContentStreamer] Event not found: {eventId}");
            }
            
            return evt;
        }
        
        /// <summary>
        /// Preloads events for the current day and surrounding days
        /// </summary>
        public void PreloadEventsForDay(int day)
        {
            _currentDay = day;
            
            if (_enableDebugLogging)
                Log.Info($"[ContentStreamer] Preloading events for day {day}");
            
            var gameData = GameData.Instance;
            if (gameData?.Events == null)
            {
                Log.Warning("[ContentStreamer] GameData or Events list is null");
                return;
            }
            
            int preloadedCount = 0;
            
            foreach (var evt in gameData.Events)
            {
                if (evt == null) continue;
                
                // Check if event is relevant for current day range
                if (IsEventRelevantForDay(evt, day))
                {
                    string eventId = evt.name;
                    
                    if (!_preloadedEventIds.Contains(eventId))
                    {
                        CacheEvent(eventId, evt);
                        _preloadedEventIds.Add(eventId);
                        preloadedCount++;
                        
                        // Preload linked events if enabled
                        if (_preloadLinkedEvents)
                        {
                            PreloadLinkedEvents(evt);
                        }
                    }
                }
            }
            
            if (_enableDebugLogging)
                Log.Info($"[ContentStreamer] Preloaded {preloadedCount} events for day {day}");
        }
        
        /// <summary>
        /// Checks if an event is relevant for a specific day
        /// </summary>
        private bool IsEventRelevantForDay(SimEventSO evt, int day)
        {
            int minDay = day - _preloadRadius;
            int maxDay = day + _preloadRadius;
            
            // Check if event's day range overlaps with preload range
            return evt.MaxTimeUnit >= minDay && evt.MinTimeUnit <= maxDay;
        }
        
        /// <summary>
        /// Preloads events linked to the given event
        /// </summary>
        private void PreloadLinkedEvents(SimEventSO evt)
        {
            if (evt?.Choices == null) return;
            
            foreach (var choice in evt.Choices)
            {
                if (choice?.NextEvent != null)
                {
                    string linkedEventId = choice.NextEvent.name;
                    
                    if (!_eventCache.ContainsKey(linkedEventId) && !_preloadedEventIds.Contains(linkedEventId))
                    {
                        CacheEvent(linkedEventId, choice.NextEvent);
                        _preloadedEventIds.Add(linkedEventId);
                        
                        if (_enableDebugLogging)
                            Log.Info($"[ContentStreamer] Preloaded linked event: {linkedEventId}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Loads an event from GameData
        /// </summary>
        private SimEventSO LoadEventFromGameData(string eventId)
        {
            var gameData = GameData.Instance;
            if (gameData?.Events == null) return null;
            
            return gameData.Events.FirstOrDefault(e => e != null && e.name == eventId);
        }
        
        /// <summary>
        /// Caches an event with LRU eviction
        /// </summary>
        private void CacheEvent(string eventId, SimEventSO evt)
        {
            if (evt == null) return;
            
            // Remove oldest if cache is full
            if (_eventCache.Count >= maxCachedEvents && !_eventCache.ContainsKey(eventId))
            {
                EvictOldestEvent();
            }
            
            // Add or update cache
            _eventCache[eventId] = evt;
            
            // Update cache order
            if (_eventCacheOrder.Contains(eventId))
            {
                // Move to front (most recently used)
                var tempQueue = new Queue<string>();
                while (_eventCacheOrder.Count > 0)
                {
                    var id = _eventCacheOrder.Dequeue();
                    if (id != eventId)
                        tempQueue.Enqueue(id);
                }
                _eventCacheOrder = tempQueue;
            }
            
            _eventCacheOrder.Enqueue(eventId);
        }
        
        /// <summary>
        /// Evicts the oldest cached event
        /// </summary>
        private void EvictOldestEvent()
        {
            if (_eventCacheOrder.Count > 0)
            {
                string oldestId = _eventCacheOrder.Dequeue();
                _eventCache.Remove(oldestId);
                _preloadedEventIds.Remove(oldestId);
                
                if (_enableDebugLogging)
                    Log.Info($"[ContentStreamer] Evicted oldest event from cache: {oldestId}");
            }
        }
        
        /// <summary>
        /// Clears all cached content
        /// </summary>
        public void ClearCache()
        {
            _eventCache.Clear();
            _choiceCache.Clear();
            _outcomeCache.Clear();
            
            _eventCacheOrder.Clear();
            _choiceCacheOrder.Clear();
            _outcomeCacheOrder.Clear();
            
            _preloadedEventIds.Clear();
            
            Log.Info("[ContentStreamer] Cache cleared");
        }
        
        /// <summary>
        /// Preloads content for the next few days
        /// </summary>
        public void PreloadUpcomingContent(int currentDay, int daysAhead = 3)
        {
            for (int i = 0; i <= daysAhead; i++)
            {
                PreloadEventsForDay(currentDay + i);
            }
        }
        
        /// <summary>
        /// Cleans up irrelevant cached content for the current day
        /// </summary>
        public void CleanupIrrelevantContent(int currentDay)
        {
            var evictionList = new List<string>();
            
            foreach (var kvp in _eventCache)
            {
                var evt = kvp.Value;
                if (evt != null && !IsEventRelevantForDay(evt, currentDay))
                {
                    evictionList.Add(kvp.Key);
                }
            }
            
            foreach (var eventId in evictionList)
            {
                _eventCache.Remove(eventId);
                _preloadedEventIds.Remove(eventId);
                
                // Also remove from cache order
                var tempQueue = new Queue<string>();
                while (_eventCacheOrder.Count > 0)
                {
                    var id = _eventCacheOrder.Dequeue();
                    if (id != eventId)
                        tempQueue.Enqueue(id);
                }
                _eventCacheOrder = tempQueue;
            }
            
            if (_enableDebugLogging && evictionList.Count > 0)
                Log.Info($"[ContentStreamer] Cleaned up {evictionList.Count} irrelevant events for day {currentDay}");
        }
        
        /// <summary>
        /// Gets memory usage statistics
        /// </summary>
        public ContentStreamingStats GetStats()
        {
            return new ContentStreamingStats
            {
                CachedEvents = _eventCache.Count,
                CachedChoices = _choiceCache.Count,
                CachedOutcomes = _outcomeCache.Count,
                MaxCachedEvents = maxCachedEvents,
                MaxCachedChoices = maxCachedChoices,
                MaxCachedOutcomes = maxCachedOutcomes,
                PreloadedEventIds = _preloadedEventIds.Count,
                CurrentDay = _currentDay,
                PreloadRadius = _preloadRadius
            };
        }
        
        /// <summary>
        /// Gets debug information about the content streamer
        /// </summary>
        public string GetDebugInfo()
        {
            var stats = GetStats();
            var info = new System.Text.StringBuilder();
            
            info.AppendLine("ContentStreamer Debug Info:");
            info.AppendLine($"  Current Day: {stats.CurrentDay}");
            info.AppendLine($"  Preload Radius: {stats.PreloadRadius}");
            info.AppendLine($"  Cached Events: {stats.CachedEvents}/{stats.MaxCachedEvents}");
            info.AppendLine($"  Cached Choices: {stats.CachedChoices}/{stats.MaxCachedChoices}");
            info.AppendLine($"  Cached Outcomes: {stats.CachedOutcomes}/{stats.MaxCachedOutcomes}");
            info.AppendLine($"  Preloaded Events: {stats.PreloadedEventIds}");
            info.AppendLine($"  Preload Linked Events: {_preloadLinkedEvents}");
            
            float memoryUsagePercent = (float)stats.CachedEvents / stats.MaxCachedEvents * 100f;
            info.AppendLine($"  Memory Usage: {memoryUsagePercent:F1}%");
            
            return info.ToString();
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            // Clear cache when app is paused to free memory
            if (pauseStatus)
            {
                ClearCache();
                Log.Info("[ContentStreamer] Cache cleared due to application pause");
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // Preload content when app regains focus
            if (hasFocus && _currentDay > 0)
            {
                PreloadEventsForDay(_currentDay);
            }
        }
    }
    
    /// <summary>
    /// Statistics about content streaming performance
    /// </summary>
    [System.Serializable]
    public struct ContentStreamingStats
    {
        public int CachedEvents;
        public int CachedChoices;
        public int CachedOutcomes;
        public int MaxCachedEvents;
        public int MaxCachedChoices;
        public int MaxCachedOutcomes;
        public int PreloadedEventIds;
        public int CurrentDay;
        public int PreloadRadius;
    }
}