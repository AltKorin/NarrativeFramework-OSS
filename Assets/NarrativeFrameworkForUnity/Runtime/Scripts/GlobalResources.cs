// GlobalResource.cs
using System.Collections.Generic;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame
{
    [CreateAssetMenu(fileName = "GlobalResources", menuName = "Narrative Framework/Global Resources")]
    public class GlobalResources : ScriptableObject
    {
        [SerializeField] private List<ResourceEntry> _resources = new();

        private Dictionary<string, float> _resourceDict = new();

        public void Initialize()
        {
            _resourceDict.Clear();
            foreach (var entry in _resources)
                _resourceDict[entry.Id] = entry.InitialValue;
        }

        public float GetValue(string id)
        {
            if (_resourceDict.TryGetValue(id, out float value))
                return value;
            Log.Warning($"Resource '{id}' not found");
            return 0f;
        }

        public void ModifyValue(string id, float amount)
        {
            if (_resourceDict.ContainsKey(id))
                _resourceDict[id] += amount;
            else
                _resourceDict[id] = amount;
        }

        public IReadOnlyDictionary<string, float> GetAll() => _resourceDict;
    }

    [System.Serializable]
    public class ResourceEntry
    {
        public string Id;
        public float InitialValue;
    }
}
