using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Supplementary types & stubs for GameController.
    /// Keep data-only and minimal to avoid breaking existing flow.
    /// </summary>
    public partial class GameController
    {
        // Enum used by preview UI
        public enum PreviewCategory
        {
            Resource,
            Variable,
            Flag,
            Relationship,
            Event,      // <-- added to satisfy usages in GameController
            Other
        }

        /// <summary>
        /// UI-friendly description of a predicted outcome effect (used by ChoiceButton).
        /// Includes common aliases (camelCase/PascalCase) so legacy code still compiles.
        /// </summary>
        public class OutcomePreviewItem
        {
            // Identity / labels
            public string ResourceName;
            public string VariableName;
            public string Label; public string label;

            // Prepared texts
            public string Text; public string text;
            public string Tooltip; public string tooltip;
            public string ValueText; public string valueText;

            // Numeric / boolean deltas
            public float Delta; public float delta;
            public bool BoolValue; public bool boolValue;
            public bool IsPositive; public bool isPositive;

            // Visuals
            public Sprite Icon; public Sprite icon;
            public Color Color = Color.white; public Color color = Color.white;

            // Optional id
            public string Id; public string id;

            // Fields referenced by GameController
            public float probability;
            public PreviewCategory category;

            public OutcomePreviewItem() { }

            public OutcomePreviewItem(string displayText, float deltaValue = 0f, Sprite displayIcon = null)
            {
                Text = displayText; text = displayText;
                Delta = deltaValue; delta = deltaValue;
                Icon = displayIcon; icon = displayIcon;
                IsPositive = deltaValue >= 0f; isPositive = IsPositive;
                category = PreviewCategory.Other;
            }
        }

        /// <summary>
        /// Stub helpers to satisfy calls from Outcome/SimulationManager without breaking flow.
        /// Wire to your StoryArcManager if available.
        /// </summary>
        public void TriggerStoryArc(StoryArcSO arc)
        {
            if (arc == null) return;
            Debug.Log($"[GameController] TriggerStoryArc(StoryArcSO) called for '{arc.name}' (stub).");
        }

        public void TriggerStoryArc(string arcId)
        {
            if (string.IsNullOrWhiteSpace(arcId)) return;
            Debug.Log($"[GameController] TriggerStoryArc(string) called for '{arcId}' (stub).");
        }
    }
}
