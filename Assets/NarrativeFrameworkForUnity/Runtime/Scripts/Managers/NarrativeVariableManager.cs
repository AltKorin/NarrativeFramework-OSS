using System.Collections.Generic;
using UnityEngine;
using SimulationGame.Interfaces;
using SimulationGame.DebugTools;

namespace SimulationGame
{
    /// <summary>
    /// Manages the state of all narrative variables (floats and booleans) for the game.
    /// This class is responsible for initializing, retrieving, and modifying variable values.
    /// </summary>
    public class NarrativeVariableManager : INarrativeVariableManager
    {
        // Internal storage for float and boolean variables.
        private Dictionary<string, float> _floatVariables = new();
        private Dictionary<string, bool> _boolVariables = new();

        /// <summary>
        /// Gets a read-only dictionary of the current float variables.
        /// </summary>
        public IReadOnlyDictionary<string, float> GetFloatVariables() => _floatVariables;

        /// <summary>
        /// Gets a read-only dictionary of the current boolean variables.
        /// </summary>
        public IReadOnlyDictionary<string, bool> GetBoolVariables() => _boolVariables;

        /// <summary>
        /// Initializes the manager with a collection of variable definitions from ScriptableObjects.
        /// </summary>
        /// <param name="variables">An enumeration of NarrativeVariableSO to load.</param>
        public void Initialize(IEnumerable<NarrativeVariableSO> variables)
        {
            _floatVariables.Clear();
            _boolVariables.Clear();

            if (variables == null) return;

            foreach (var variable in variables)
            {
                // Safety check: skip missing or unnamed variable assets.
                if (variable == null || string.IsNullOrWhiteSpace(variable.Id))
                {
                    Log.Warning("[NarrativeVariableManager] Skipped a null or unnamed variable asset during initialization.");
                    continue;
                }

                switch (variable.Type)
                {
                    case NarrativeVariableSO.VariableType.Float:
                        _floatVariables[variable.Id] = variable.InitialFloatValue;
                        break;
                    case NarrativeVariableSO.VariableType.Boolean:
                        _boolVariables[variable.Id] = variable.InitialBoolValue;
                        break;
                }
            }
        }

        /// <summary>
        /// Checks if a float variable with the specified ID exists.
        /// </summary>
        /// <param name="id">The ID of the variable to check.</param>
        /// <returns>True if the float variable exists, false otherwise.</returns>
        public bool HasVariable(string id) => _floatVariables.ContainsKey(id);

        /// <summary>
        /// Gets the value of a float variable.
        /// </summary>
        /// <param name="id">The ID of the float variable.</param>
        /// <returns>The value of the variable, or 0f if not found.</returns>
        public float GetValue(string id)
        {
            if (_floatVariables.TryGetValue(id, out float value))
                return value;

            Log.Warning($"[NarrativeVariableManager] Float variable '{id}' not found. Returning default value 0f.");
            return 0f;
        }

        /// <summary>
        /// Sets the value of a float variable.
        /// </summary>
        /// <param name="id">The ID of the float variable.</param>
        /// <param name="value">The new value to set.</param>
        public void SetValue(string id, float value)
        {
            if (_floatVariables.ContainsKey(id))
            {
                _floatVariables[id] = value;
            }
            else
            {
                Log.Warning($"[NarrativeVariableManager] Cannot set value. Float variable '{id}' not found.");
            }
        }

        /// <summary>
        /// Modifies a float variable by a given delta. This method name is used by the Outcome system.
        /// </summary>
        /// <param name="id">The ID of the float variable to modify.</param>
        /// <param name="delta">The amount to add to the current value.</param>
        public void ModifyVariable(string id, float delta)
        {
            if (_floatVariables.ContainsKey(id))
            {
                _floatVariables[id] += delta;
            }
            else
            {
                Log.Warning($"[NarrativeVariableManager] Cannot modify value. Float variable '{id}' not found.");
            }
        }

        /// <summary>
        /// Checks if a boolean variable with the specified ID exists.
        /// </summary>
        /// <param name="id">The ID of the variable to check.</param>
        /// <returns>True if the boolean variable exists, false otherwise.</returns>
        public bool IsBoolean(string id) => _boolVariables.ContainsKey(id);

        /// <summary>
        /// Sets the value of a boolean variable.
        /// </summary>
        /// <param name="id">The ID of the boolean variable.</param>
        /// <param name="value">The new value to set.</param>
        public void SetBoolean(string id, bool value)
        {
            if (_boolVariables.ContainsKey(id))
            {
                _boolVariables[id] = value;
            }
            else
            {
                Log.Warning($"[NarrativeVariableManager] Cannot set boolean. Variable '{id}' not found.");
            }
        }

        /// <summary>
        /// Gets the value of a boolean variable.
        /// </summary>
        /// <param name="id">The ID of the boolean variable.</param>
        /// <returns>The value of the variable, or false if not found.</returns>
        public bool GetBoolean(string id)
        {
            if (_boolVariables.TryGetValue(id, out var value))
                return value;

            Log.Warning($"[NarrativeVariableManager] Boolean variable '{id}' not found. Returning default value false.");
            return false;
        }

        /// <summary>
        /// Alias for GetBoolean(id).
        /// </summary>
        public bool GetBool(string id) => GetBoolean(id);

        /// <summary>
        /// Alias for SetBoolean(id, value).
        /// </summary>
        public void SetBool(string id, bool value) => SetBoolean(id, value);

        /// <summary>
        /// Gets a read-only dictionary of all boolean variables.
        /// </summary>
        public IReadOnlyDictionary<string, bool> GetAllBooleans() => _boolVariables;

        /// <summary>
        /// Applies a full state of float and boolean variables, typically from a save file.
        /// This will overwrite all existing variable data.
        /// </summary>
        /// <param name="floatVars">The dictionary of float variables to apply.</param>
        /// <param name="boolVars">The dictionary of boolean variables to apply.</param>
        public void Apply(Dictionary<string, float> floatVars, Dictionary<string, bool> boolVars)
        {
            _floatVariables = floatVars ?? new Dictionary<string, float>();
            _boolVariables = boolVars ?? new Dictionary<string, bool>();
        }

        /// <summary>
        /// Gets a read-only dictionary of all float variables.
        /// </summary>
        public IReadOnlyDictionary<string, float> GetAllVariables() => _floatVariables;
    }
}
