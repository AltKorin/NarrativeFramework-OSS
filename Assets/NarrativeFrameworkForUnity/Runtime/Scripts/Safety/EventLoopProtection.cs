using System.Collections.Generic;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame.Safety
{
    /// <summary>
    /// Protects against infinite loops in narrative event chains
    /// Tracks event call frequency and detects potential circular references
    /// </summary>
    public class EventLoopProtection : MonoBehaviour
    {
        [Header("Loop Detection Settings")]
        [SerializeField] private int maxEventCallsPerFrame = 10;
        [SerializeField] private int maxEventCallsPerSecond = 50;
        [SerializeField] private bool enableDebugLogging = false;
        
        private Dictionary<string, int> _eventCallCount = new();
        private Dictionary<string, float> _lastEventCallTime = new();
        private Dictionary<string, int> _eventCallsThisSecond = new();
        
        private static EventLoopProtection _instance;
        public static EventLoopProtection Instance => _instance;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Checks if an event call would create a loop or exceed safety limits
        /// </summary>
        /// <param name="eventId">ID of the event being called</param>
        /// <returns>True if event should be blocked to prevent loop</returns>
        public bool CheckEventLoop(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Log.Warning("EventLoopProtection: Empty event ID provided");
                return false;
            }
            
            // Check frame-based limits
            if (!_eventCallCount.ContainsKey(eventId))
                _eventCallCount[eventId] = 0;
            
            _eventCallCount[eventId]++;
            
            if (_eventCallCount[eventId] > maxEventCallsPerFrame)
            {
                Log.Error($"Event loop detected: '{eventId}' called {_eventCallCount[eventId]} times in one frame. Blocking further calls.");
                return true;
            }
            
            // Check time-based limits
            float currentTime = Time.time;
            if (_lastEventCallTime.TryGetValue(eventId, out float lastCallTime))
            {
                if (currentTime - lastCallTime < 1f) // Within same second
                {
                    if (!_eventCallsThisSecond.ContainsKey(eventId))
                        _eventCallsThisSecond[eventId] = 0;
                    
                    _eventCallsThisSecond[eventId]++;
                    
                    if (_eventCallsThisSecond[eventId] > maxEventCallsPerSecond)
                    {
                        Log.Error($"Event spam detected: '{eventId}' called {_eventCallsThisSecond[eventId]} times in one second. Blocking further calls.");
                        return true;
                    }
                }
                else
                {
                    // Reset second-based counter
                    _eventCallsThisSecond[eventId] = 1;
                }
            }
            
            _lastEventCallTime[eventId] = currentTime;
            
            if (enableDebugLogging)
            {
                Log.Info($"EventLoopProtection: Event '{eventId}' called {_eventCallCount[eventId]} times this frame");
            }
            
            return false;
        }
        
        /// <summary>
        /// Resets all event call counters (called at end of frame)
        /// </summary>
        public void ResetFrameEventCounts()
        {
            _eventCallCount.Clear();
        }
        
        /// <summary>
        /// Resets counters for a specific event (useful for valid repeated events)
        /// </summary>
        /// <param name="eventId">Event ID to reset</param>
        public void ResetEventCount(string eventId)
        {
            if (_eventCallCount.ContainsKey(eventId))
                _eventCallCount.Remove(eventId);
            
            if (_eventCallsThisSecond.ContainsKey(eventId))
                _eventCallsThisSecond.Remove(eventId);
            
            if (_lastEventCallTime.ContainsKey(eventId))
                _lastEventCallTime.Remove(eventId);
        }
        
        /// <summary>
        /// Gets current call count for an event this frame
        /// </summary>
        /// <param name="eventId">Event ID to check</param>
        /// <returns>Number of times event was called this frame</returns>
        public int GetEventCallCount(string eventId)
        {
            return _eventCallCount.TryGetValue(eventId, out int count) ? count : 0;
        }
        
        /// <summary>
        /// Validates an event chain for circular references (editor-time validation)
        /// </summary>
        /// <param name="startEvent">Starting event to validate</param>
        /// <param name="maxDepth">Maximum chain depth to check</param>
        /// <returns>True if chain is valid, false if circular reference found</returns>
        public bool ValidateEventChain(SimEventSO startEvent, int maxDepth = 20)
        {
            if (startEvent == null)
            {
                Log.Warning("EventLoopProtection: Cannot validate null event");
                return false;
            }
            
            var visitedEvents = new HashSet<string>();
            var eventStack = new Stack<string>();
            
            return ValidateEventRecursive(startEvent, visitedEvents, eventStack, 0, maxDepth);
        }
        
        private bool ValidateEventRecursive(SimEventSO evt, HashSet<string> visitedEvents, Stack<string> eventStack, int depth, int maxDepth)
        {
            if (evt == null) return true;
            
            if (depth > maxDepth)
            {
                Log.Error($"Event chain too deep (>{maxDepth}), possible infinite loop detected");
                return false;
            }
            
            string eventId = evt.name;
            if (visitedEvents.Contains(eventId))
            {
                string chainPath = string.Join(" → ", eventStack.ToArray());
                Log.Error($"Circular reference detected in event chain: {chainPath} → {eventId}");
                return false;
            }
            
            visitedEvents.Add(eventId);
            eventStack.Push(eventId);
            
            // Check all choices for next events
            if (evt.Choices != null)
            {
                foreach (var choice in evt.Choices)
                {
                    if (choice?.NextEvent != null)
                    {
                        if (!ValidateEventRecursive(choice.NextEvent, visitedEvents, eventStack, depth + 1, maxDepth))
                            return false;
                    }
                }
            }
            
            eventStack.Pop();
            visitedEvents.Remove(eventId);
            return true;
        }
        
        private void LateUpdate()
        {
            // Reset frame counters at end of each frame
            ResetFrameEventCounts();
        }
        
        #region Debug Information
        
        /// <summary>
        /// Gets debug information about current event call statistics
        /// </summary>
        /// <returns>Debug string with event call information</returns>
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("Event Loop Protection Status:");
            info.AppendLine($"Max calls per frame: {maxEventCallsPerFrame}");
            info.AppendLine($"Max calls per second: {maxEventCallsPerSecond}");
            
            if (_eventCallCount.Count > 0)
            {
                info.AppendLine("Current frame calls:");
                foreach (var kvp in _eventCallCount)
                {
                    info.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                info.AppendLine("No events called this frame");
            }
            
            return info.ToString();
        }
        
        #endregion
    }
}