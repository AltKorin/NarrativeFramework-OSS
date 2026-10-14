using System.Collections.Generic;
using SimulationGame;

namespace SimulationGame.Runtime
{
    /// <summary>
    /// Interface for managing relationships in the simulation game.
    /// </summary>

    public interface IRelationshipManager
    {
        List<Relationship> GetAllRelationships();
        void LoadRelationships(List<Relationship> relationships);
        void Apply(Dictionary<string, float> values);
    }
}