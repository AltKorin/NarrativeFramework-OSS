using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Template that describes a category of actors
    /// (e.g., “Great House”, “Mercenary Company”, “Guild”).
    /// All individual <see cref="ActorSO"/> instances can
    /// point at one of these for shared flavor data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewActorType", menuName = "Narrative Framework/Actor Type")]
    public class ActorTypeSO : ScriptableObject
    {
        /* ─────────────────────────────────────────────
         *  Public constants – useful for custom editors
         * ───────────────────────────────────────────── */
        public const string PROP_TYPE_NAME  = "_typeName";
        public const string PROP_DISPLAY    = "_displayName";
        public const string PROP_DESC       = "_description";
        public const string PROP_ATTRIBUTES = "_attributes";

        /* ─────────────────────────────────────────────
         *  Serialized data
         * ───────────────────────────────────────────── */
        [SerializeField] private string        _typeName     = "house";
        [SerializeField] private string        _displayName  = "Great House";
        [SerializeField] [TextArea(2, 4)]
        private string                         _description  = "A noble lineage vying for power.";
        [SerializeField] private List<string>  _attributes   = new();

        /* ─────────────────────────────────────────────
         *  Public read‑only accessors
         * ───────────────────────────────────────────── */
        public string            TypeName     => _typeName;
        public string            DisplayName  => _displayName;
        public string            Description  => _description;
        public IReadOnlyList<string> Attributes => _attributes;

        /* ─────────────────────────────────────────────
         *  Optional runtime initializer
         * ───────────────────────────────────────────── */
        public void Initialize(string typeName,
                               string displayName,
                               string description,
                               IEnumerable<string> attributes = null)
        {
            _typeName    = typeName;
            _displayName = displayName;
            _description = description;

            _attributes.Clear();
            if (attributes != null)
                _attributes.AddRange(attributes);
        }

#if UNITY_EDITOR
        /* ─────────────────────────────────────────────
         *  Editor‑time validation
         * ───────────────────────────────────────────── */
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_typeName))
                _typeName = name.ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(_displayName))
                _displayName = _typeName;
        }
#endif

        public override string ToString() => _typeName;
    }
}
