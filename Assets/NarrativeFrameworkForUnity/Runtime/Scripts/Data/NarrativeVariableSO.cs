using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// A single, save‑able narrative variable that game logic can read / write.
    /// Use floats for “sliders” (0‒100 reputation, ‑1…1 alignment, etc.)
    /// and booleans for binary flags that don’t belong in the global _flags HashSet.
    /// </summary>
    [CreateAssetMenu(fileName = "NarrativeVariable",
                     menuName = "Narrative Framework/Narrative Variable")]
    public class NarrativeVariableSO : NarrativeObject
    {
        /*  Property names for custom editors  */
        public const string PROP_NAME = "VariableName";
        public const string PROP_TYPE = "Type";
        public const string PROP_FLOAT = "InitialFloatValue";
        public const string PROP_BOOL = "InitialBoolValue";

        public enum VariableType { Float, Boolean }

        [SerializeField] private string _variableName = "loyalty";
        [SerializeField] private VariableType _type = VariableType.Float;

        [SerializeField] private float _initialFloatValue = 0f;
        [SerializeField] private bool _initialBoolValue = false;

        /*  Public accessors  */
        public string VariableName => _variableName;
        public VariableType Type => _type;
        public float InitialFloatValue => _initialFloatValue;
        public bool InitialBoolValue => _initialBoolValue;

        //  ─── NarrativeVariableSO.cs ───
public void Initialize(string varName,
                       VariableType varType,
                       float initialFloat = 0f,
                       bool  initialBool  = false)
{
    _variableName      = varName;
    _type              = varType;
    _initialFloatValue = initialFloat;
    _initialBoolValue  = initialBool;
}


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_variableName))
                _variableName = name.ToLowerInvariant().Replace(' ', '_');
        }
#endif
    }
    
    
}
