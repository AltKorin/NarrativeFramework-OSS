using System.Collections.Generic;
using UnityEngine;
using SimulationGame.Interfaces;

namespace SimulationGame.Managers
{
    public class GlobalResourcesManager : MonoBehaviour, IResourceManager
    {
        private Dictionary<string, float> _resources = new Dictionary<string, float>();

        public float GetValue(string id)
        {
            if (_resources.TryGetValue(id, out float value))
            {
                return value;
            }
            Debug.LogWarning($"[GlobalResourcesManager] Resource {id} not found.");
            return 0f;
        }

        public void ModifyValue(string id, float amount)
        {
            if (_resources.ContainsKey(id))
            {
                _resources[id] += amount;
            }
            else
            {
                _resources.Add(id, amount);
            }
            Debug.Log($"[GlobalResourcesManager] Resource {id} modified by {amount}. New value: {_resources[id]}");
        }

        public void SetValue(string id, float value)
        {
            _resources[id] = value;
            Debug.Log($"[GlobalResourcesManager] Resource {id} set to {value}.");
        }

        public IReadOnlyDictionary<string, float> GetAllResources()
        {
            return _resources;
        }

        public void ModifyResource(Resource resource, float amount)
        {
            ModifyValue(resource.Id, amount);
        }

        public Dictionary<string, float> GetAllRuntimeValues()
        {
            return new Dictionary<string, float>(_resources);
        }

        public void Apply(Dictionary<string, float> values)
        {
            _resources = new Dictionary<string, float>(values);
        }
    }
}