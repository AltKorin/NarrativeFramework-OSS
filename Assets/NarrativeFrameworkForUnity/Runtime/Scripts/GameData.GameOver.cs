using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Additive (non-breaking) part of GameData: Game Over / Endings config.
    /// Kept in a partial so we don't touch your existing GameData file.
    /// </summary>
    public partial class GameData : ScriptableObject
    {
        // ---------- Serialized property names used by the editor UI ----------
        public const string PROP_ENABLE_RESOURCE_DEATHS = "_enableResourceDeaths";
        public const string PROP_RESOURCE_DEATH_RULES   = "_resourceDeathRules";
        public const string PROP_CAMPAIGN_ENDING        = "_campaignEnding";
        public const string PROP_GAME_OVER_FLAGS        = "_gameOverFlags";

        [Serializable]
        public class GameOverRule
        {
            [Tooltip("Resource Id to watch (must match Resource.Id).")]
            public string resourceId;

            [Tooltip("When current value is <= threshold, the rule fires.")]
            public float threshold = 0f;

            [Tooltip("Ending event to present when this rule fires.")]
            public SimEventSO endingEvent;

            [Tooltip("Optional flag to set when this rule fires (analytics/achievements).")]
            public string flagOnDeath;
        }

        [Header("Game Over / Endings")]
        [SerializeField] private bool _enableResourceDeaths = false;

        [SerializeField] private List<GameOverRule> _resourceDeathRules = new();

        [SerializeField] private SimEventSO _campaignEnding;

        [SerializeField] private List<string> _gameOverFlags = new(); // optional

        // ---------- Read-only accessors (keep API tidy & safe) ----------
        public bool EnableResourceDeaths => _enableResourceDeaths;
        public IReadOnlyList<GameOverRule> ResourceDeathRules => _resourceDeathRules;
        public SimEventSO CampaignEnding => _campaignEnding;
        public IReadOnlyList<string> GameOverFlags => _gameOverFlags;

#if UNITY_EDITOR
        // Small editor helpers (invoked by your NarrativeLinkerGameDataView)
        public void Editor_AddMissingDeathRulesForAllResources()
        {
            if (Resources == null) return;
            var existing = new HashSet<string>();
            foreach (var r in _resourceDeathRules)
                if (!string.IsNullOrWhiteSpace(r?.resourceId))
                    existing.Add(r.resourceId);

            int added = 0;
            foreach (var res in Resources)
            {
                if (res == null || string.IsNullOrWhiteSpace(res.Id)) continue;
                if (existing.Contains(res.Id)) continue;

                _resourceDeathRules.Add(new GameOverRule
                {
                    resourceId = res.Id,
                    threshold  = res.MinValue,
                    endingEvent = null,
                    flagOnDeath = string.Empty
                });
                added++;
            }

            if (added > 0)
                UnityEngine.Debug.Log($"[GameData] Added {added} missing GameOver rules.");
        }

#endif
    }
}
