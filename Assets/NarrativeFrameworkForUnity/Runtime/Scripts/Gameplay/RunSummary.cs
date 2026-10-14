using System.Collections.Generic;

namespace SimulationGame
{
    /// <summary>
    /// Lightweight snapshot describing how a run ended.
    /// Exposes both lowercase fields (legacy) and PascalCase properties (modern),
    /// keeping types aligned with current GameController assignments.
    /// </summary>
    public class RunSummary
    {
        // Lowercase fields used in existing code paths
        public int day;
        public string reason;
        public string endingEventId;

        // GameController assigns a List<string> here; use List for direct compatibility.
        public List<string> flags = new List<string>();

        // Resource / relation / variable snapshots (kept as dictionaries)
        public Dictionary<string, float> resources = new Dictionary<string, float>();
        public Dictionary<string, float> relationships = new Dictionary<string, float>();
        public Dictionary<string, float> variables = new Dictionary<string, float>();

        // PascalCase accessors (aliases to the same data)
        public int Day { get => day; set => day = value; }
        public string Reason { get => reason; set => reason = value; }
        public string EndingEventId { get => endingEventId; set => endingEventId = value; }

        public List<string> Flags { get => flags; set => flags = value ?? new List<string>(); }
        public Dictionary<string, float> Resources { get => resources; set => resources = value ?? new Dictionary<string, float>(); }
        public Dictionary<string, float> Relationships { get => relationships; set => relationships = value ?? new Dictionary<string, float>(); }
        public Dictionary<string, float> Variables { get => variables; set => variables = value ?? new Dictionary<string, float>(); }

        public RunSummary() { }

        public RunSummary(string endingId, int day, string reason = null)
        {
            this.endingEventId = endingId;
            this.day = day;
            this.reason = reason;
        }
    }
}
