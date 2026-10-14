using System.Collections.Generic;

namespace SimulationGame
{
    public interface INarrativeVariableManager
    {
        void Initialize(IEnumerable<NarrativeVariableSO> variables);
        bool HasVariable(string id);
        float GetValue(string id);
        void SetValue(string id, float value);
        void ModifyVariable(string id, float delta);
        bool IsBoolean(string id);
        void SetBoolean(string id, bool value);
        bool GetBoolean(string id);
        IReadOnlyDictionary<string, float> GetFloatVariables();
        IReadOnlyDictionary<string, bool> GetBoolVariables();
        void Apply(
            Dictionary<string, float> floatVars,
            Dictionary<string, bool> boolVars
        );
        IReadOnlyDictionary<string, float> GetAllVariables();

    }
    
}
