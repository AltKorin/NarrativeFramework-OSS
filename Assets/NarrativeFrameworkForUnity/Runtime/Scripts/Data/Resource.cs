using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// ScriptableObject representing a global resource
    /// (gold, food, prestige, etc.). All values are floats so
    /// you can use fractional modifiers if desired.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "Narrative Framework/Resource")]
    public class Resource : NarrativeObject
    {
        /* ─────────────────────────────────────────────
         *  Public constants – handy for custom editors
         * ───────────────────────────────────────────── */
        public const string PROP_DISPLAY_NAME = "_displayName";
        public const string PROP_TARGET_ID    = "_targetId";
        public const string PROP_INITIAL      = "_initialValue";
        public const string PROP_MIN          = "_minValue";
        public const string PROP_MAX          = "_maxValue";
        public const string PROP_ICON         = "_icon";

        /* ─────────────────────────────────────────────
         *  Serialized fields
         * ───────────────────────────────────────────── */
        [SerializeField] private string  _displayName = "Gold";
        [SerializeField] private string  _targetId    = "gold";

        [SerializeField] private float   _initialValue = 100f;
        [SerializeField] private float   _minValue     =   0f;
        [SerializeField] private float   _maxValue     = 9999f;

        [SerializeField] private Sprite  _icon;

        /* ─────────────────────────────────────────────
         *  Public read‑only properties
         * ───────────────────────────────────────────── */
        public string DisplayName  => _displayName;
        public string TargetId
        {
            get => _targetId;
            set => _targetId = value;
        }

        public float  InitialValue => _initialValue;
        public float  MinValue     => _minValue;
        public float  MaxValue     => _maxValue;
        public Sprite Icon         => _icon;

        /* ─────────────────────────────────────────────
         *  Runtime initializer (optional)
         * ───────────────────────────────────────────── */
        public void Initialize(string displayName,
                               string targetId,
                               float initialValue,
                               float minValue,
                               float maxValue,
                               Sprite icon = null)
        {
            _displayName  = displayName;
            _targetId     = targetId;
            _initialValue = initialValue;
            _minValue     = minValue;
            _maxValue     = maxValue;
            _icon         = icon;
        }

public string Name => _displayName; 
#if UNITY_EDITOR
        /* ─────────────────────────────────────────────
         *  Validation – keeps values in sensible bounds
         * ───────────────────────────────────────────── */
        private void OnValidate()
        {
            // Ensure min ≤ max
            if (_minValue > _maxValue) _maxValue = _minValue;

            // Clamp initial value to the defined range
            _initialValue = Mathf.Clamp(_initialValue, _minValue, _maxValue);
        }
#endif
    }
}
