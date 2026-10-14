using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame.Safety
{
    /// <summary>
    /// Safe reference system for SimEventSO that provides GUID-based fallback
    /// and handles broken references gracefully
    /// </summary>
    [System.Serializable]
    public class SafeEventReference
    {
        [Header("Primary Reference")]
        [SerializeField] private SimEventSO _eventReference;
        
        [Header("Fallback Data")]
        [SerializeField] private string _eventId;
        [SerializeField] private string _eventGuid;
        [SerializeField] private bool _enableDebugLogging = false;
        
        /// <summary>
        /// Gets the referenced event with fallback resolution
        /// </summary>
        public SimEventSO Event
        {
            get
            {
                // Try primary reference first
                if (_eventReference != null)
                {
                    if (_enableDebugLogging)
                        Log.Info($"[SafeEventReference] Primary reference valid for: {_eventReference.name}");
                    return _eventReference;
                }
                
                // Try GUID-based lookup
                if (!string.IsNullOrEmpty(_eventGuid))
                {
                    var foundByGuid = FindEventByGuid(_eventGuid);
                    if (foundByGuid != null)
                    {
                        Log.Warning($"[SafeEventReference] Resolved event by GUID: {foundByGuid.name}");
                        _eventReference = foundByGuid; // Cache for future use
                        return foundByGuid;
                    }
                }
                
                // Try ID-based lookup as last resort
                if (!string.IsNullOrEmpty(_eventId))
                {
                    var foundById = ValidationHelpers.SafeGetEventById(_eventId, null, "SafeEventReference");
                    if (foundById != null)
                    {
                        Log.Warning($"[SafeEventReference] Resolved event by ID: {foundById.name}");
                        _eventReference = foundById; // Cache for future use
                        return foundById;
                    }
                }
                
                if (_enableDebugLogging)
                    Log.Warning($"[SafeEventReference] Failed to resolve event (ID: {_eventId}, GUID: {_eventGuid})");
                
                return null;
            }
        }
        
        /// <summary>
        /// Checks if the reference is valid (can resolve to an event)
        /// </summary>
        public bool IsValid => Event != null;
        
        /// <summary>
        /// Gets the ID of the referenced event
        /// </summary>
        public string EventId => Event?.name ?? _eventId ?? "";
        
        /// <summary>
        /// Creates a safe reference from a SimEventSO
        /// </summary>
        public SafeEventReference(SimEventSO eventSO)
        {
            SetEvent(eventSO);
        }
        
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public SafeEventReference() { }
        
        /// <summary>
        /// Sets the event reference and caches fallback data
        /// </summary>
        public void SetEvent(SimEventSO eventSO)
        {
            _eventReference = eventSO;
            
            if (eventSO != null)
            {
                _eventId = eventSO.name;
                
                #if UNITY_EDITOR
                // In editor, we can get the GUID
                var path = UnityEditor.AssetDatabase.GetAssetPath(eventSO);
                if (!string.IsNullOrEmpty(path))
                {
                    _eventGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                }
                #endif
            }
            else
            {
                _eventId = "";
                _eventGuid = "";
            }
        }
        
        /// <summary>
        /// Attempts to find an event by GUID (editor only for now)
        /// </summary>
        private SimEventSO FindEventByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
                
            #if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<SimEventSO>(path);
            }
            #endif
            
            // Runtime fallback - would need custom GUID tracking system
            Log.Warning("[SafeEventReference] GUID lookup not available at runtime");
            return null;
        }
        
        /// <summary>
        /// Validates the reference and logs any issues
        /// </summary>
        public bool Validate(string context = "Unknown")
        {
            if (_eventReference != null)
            {
                return true; // Primary reference is valid
            }
            
            if (!string.IsNullOrEmpty(_eventId))
            {
                var found = ValidationHelpers.SafeGetEventById(_eventId, null, context);
                if (found != null)
                {
                    return true; // Can resolve by ID
                }
            }
            
            Log.Error($"[{context}] SafeEventReference validation failed - no valid reference or fallback data");
            return false;
        }
        
        /// <summary>
        /// Gets debug information about this reference
        /// </summary>
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"SafeEventReference Debug Info:");
            info.AppendLine($"  Primary Reference: {(_eventReference != null ? _eventReference.name : "null")}");
            info.AppendLine($"  Event ID: {_eventId ?? "null"}");
            info.AppendLine($"  Event GUID: {_eventGuid ?? "null"}");
            info.AppendLine($"  Is Valid: {IsValid}");
            info.AppendLine($"  Resolved Event: {Event?.name ?? "null"}");
            return info.ToString();
        }
        
        /// <summary>
        /// Implicit conversion to SimEventSO for easy use
        /// </summary>
        public static implicit operator SimEventSO(SafeEventReference reference)
        {
            return reference?.Event;
        }
        
        /// <summary>
        /// Implicit conversion from SimEventSO
        /// </summary>
        public static implicit operator SafeEventReference(SimEventSO eventSO)
        {
            return new SafeEventReference(eventSO);
        }
    }
}