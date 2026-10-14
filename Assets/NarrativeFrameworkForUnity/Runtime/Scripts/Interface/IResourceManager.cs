using System.Collections.Generic;

namespace SimulationGame.Interfaces
{
    public interface IResourceManager
    {
        float GetValue(string id);
        void ModifyValue(string id, float amount);
        void SetValue(string id, float value);
        IReadOnlyDictionary<string, float> GetAllResources();
        void ModifyResource(Resource resource, float amount);
        Dictionary<string, float> GetAllRuntimeValues();
        void Apply(Dictionary<string, float> values);

    }
}
