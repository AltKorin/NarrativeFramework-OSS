using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// One directional relationship: Source → Target.
    /// Strength ranges from ‑100 (mortal enemies) to +100 (allies).
    /// </summary>
    [System.Serializable]
    public class Relationship
    {
        /* ─────────────────────────────────────────────
         *  Visible in the Inspector
         * ───────────────────────────────────────────── */
        [Tooltip("Actor initiating the relationship.")]
        public ActorSO Source;

        [Tooltip("Actor who is the target of the relationship.")]
        public ActorSO Target;

        [Tooltip("‑100 = hostile, 0 = neutral, +100 = allied.")]
        [Range(-100f, 100f)]
        public float Strength = 0f;

        [Tooltip("Human‑readable label (Allies, Rivals, Trade Partner...).")]
        public string RelationshipType = "Neutral";

        /* ─────────────────────────────────────────────
         *  Helper methods
         * ───────────────────────────────────────────── */

        /// <summary>
        /// Adjusts strength by delta and clamps to the valid range.
        /// </summary>
        public void Modify(float delta)
        {
            Strength = Mathf.Clamp(Strength + delta, -100f, 100f);
            UpdateTypeAutomatically();
        }

        /// <summary>
        /// Returns true if Source and Target are allies (≥ 50).
        /// </summary>
        public bool IsAllied() => Strength >= 50f;

        /// <summary>
        /// Returns true if Source and Target are hostile (≤ ‑50).
        /// </summary>
        public bool IsHostile() => Strength <= -50f;

        /// <summary>
        /// Call after changing Strength to keep the string label in sync.
        /// Feel free to customize thresholds.
        /// </summary>
        private void UpdateTypeAutomatically()
        {
            if (Strength >= 50f)      RelationshipType = "Allies";
            else if (Strength <= -50) RelationshipType = "Rivals";
            else                      RelationshipType = "Neutral";
        }
    }
}
