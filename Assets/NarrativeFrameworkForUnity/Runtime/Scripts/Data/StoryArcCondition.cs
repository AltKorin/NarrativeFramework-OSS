using UnityEngine;

namespace SimulationGame
{
    public enum StoryArcConditionType
    {
        FlagExists,
        FlagNotExists,
        GlobalFlag,          // same as FlagExists but semantically “world scope”
        ActorBoolAttribute,
        ActorFloatAttribute,
        NarrativeBool,
        NarrativeFloat,
        BoolExpectedValue    // explicit comparison against ExpectedBool
    }

    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        Equal
    }

    [System.Serializable]
    public class StoryArcCondition
    {
        [Tooltip("Which criterion is evaluated?")]
        public StoryArcConditionType ConditionType;

        [Tooltip("Flag, narrative variable, or attribute key.")]
        public string Key;

        [Tooltip("Optional actor when checking attributes.")]
        public ActorSO TargetActor;

        [Header("Float comparison")]
        public float FloatValue;
        public ComparisonOperator FloatComparison = ComparisonOperator.Equal;

        [Header("Bool comparison (BoolExpectedValue)")]
        public bool ExpectedBool = true;

        /// <summary>
        /// Evaluate this condition against the current <see cref="GameController"/>.
        /// </summary>
        public bool Evaluate(GameController controller)
        {
            switch (ConditionType)
            {
                case StoryArcConditionType.FlagExists:
                case StoryArcConditionType.GlobalFlag:
                    return controller.HasFlag(Key);

                case StoryArcConditionType.FlagNotExists:
                    return !controller.HasFlag(Key);

                case StoryArcConditionType.ActorBoolAttribute:
                    return TargetActor?.GetAttribute(Key)?.ToLowerInvariant() == "true";

                case StoryArcConditionType.ActorFloatAttribute:
                    if (float.TryParse(TargetActor?.GetAttribute(Key), out float attrVal))
                        return Compare(attrVal, FloatValue, FloatComparison);
                    return false;

                case StoryArcConditionType.NarrativeBool:
                    return controller.VariableManager.GetBool(Key);

                case StoryArcConditionType.NarrativeFloat:
                    float current = controller.VariableManager.GetValue(Key);
                    return Compare(current, FloatValue, FloatComparison);

                case StoryArcConditionType.BoolExpectedValue:
                    return controller.VariableManager.GetBool(Key) == ExpectedBool;

                default:
                    return false;
            }
        }

        private static bool Compare(float a, float b, ComparisonOperator op) => op switch
        {
            ComparisonOperator.GreaterThan => a >  b,
            ComparisonOperator.LessThan    => a <  b,
            ComparisonOperator.Equal       => Mathf.Approximately(a, b),
            _                              => false
        };
    }
}
