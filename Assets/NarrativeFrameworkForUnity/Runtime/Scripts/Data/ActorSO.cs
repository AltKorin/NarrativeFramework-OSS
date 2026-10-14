using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// A single faction leader / house / character the simulation can reference.
    /// Stores name, portrait, description, type, and arbitrary key‑value attributes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewActor", menuName = "Narrative Framework/Actor")]
    public class ActorSO : NarrativeObject
    {
        /* ─────────────────────────────────────────────
         *  Public constants – handy for custom editors
         * ───────────────────────────────────────────── */
        public const string PROP_NAME        = "_actorName";
        public const string PROP_SPRITE      = "_actorSprite";
        public const string PROP_DESCRIPTION = "_actorDescription";
        public const string PROP_TYPE        = "_actorType";
        public const string PROP_ATTRIBUTES  = "_attributes";

        /* ─────────────────────────────────────────────
         *  Serialized data
         * ───────────────────────────────────────────── */
        [SerializeField] private string        _actorName       = "Unnamed House";
        [SerializeField] private Sprite        _actorSprite;
        [SerializeField] private string        _actorDescription = "Describe this faction here.";
        [SerializeField] private ActorTypeSO   _actorType;
        [SerializeField] private List<ActorAttribute> _attributes = new();

        /* ─────────────────────────────────────────────
         *  Public read‑only accessors
         * ───────────────────────────────────────────── */
        public string          ActorName        => _actorName;
        public Sprite          ActorSprite      => _actorSprite;
        public string          ActorDescription => _actorDescription;
        public ActorTypeSO     ActorType        => _actorType;
        public IReadOnlyList<ActorAttribute> Attributes => _attributes;

        /* ─────────────────────────────────────────────
         *  Runtime initializer (optional)
         * ───────────────────────────────────────────── */
        public void Initialize(string name,
                               Sprite sprite,
                               string description,
                               ActorTypeSO type = null)
        {
            _actorName        = name;
            _actorSprite      = sprite;
            _actorDescription = description;
            _actorType        = type;
        }

        /* ─────────────────────────────────────────────
         *  Attribute helpers
         * ───────────────────────────────────────────── */
        public string GetAttribute(string key)
        {
            var attr = _attributes.Find(a => a.Key == key);
            return attr?.Value;
        }

        /// <summary>
        /// Adds or updates a custom attribute (string key/value pair).
        /// </summary>
        public void SetAttribute(string key, string value)
        {
            var attr = _attributes.Find(a => a.Key == key);
            if (attr != null)
                attr.Value = value;
            else
                _attributes.Add(new ActorAttribute { Key = key, Value = value });
        }

#if UNITY_EDITOR
        /* ─────────────────────────────────────────────
         *  Editor‑time validation and auto‑naming
         * ───────────────────────────────────────────── */
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_actorName))
                _actorName = name.Replace('_', ' ');

            // keep Id (inherited from NarrativeObject) in sync if empty
            if (string.IsNullOrWhiteSpace(Id))
                Id = _actorName.ToLowerInvariant().Replace(' ', '_');
        }
#endif
    }

    // Helper struct for arbitrary key/value metadata
    [System.Serializable]
    public class ActorAttribute
    {
        public string Key;
        public string Value;
    }
}
