using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationGame.UI
{
    public class ResourceDisplayUI : MonoBehaviour
    {
        public TextMeshProUGUI ValueText;
        public Slider ValueSlider;
        public Image IconImage;

        public void Setup(string label, float value, Sprite icon = null, float min = 0f, float max = 100f)
        {
            if (ValueText != null)
                ValueText.text = $"{label}: {value:F0}";

            if (ValueSlider != null)
            {
                ValueSlider.minValue = min;
                ValueSlider.maxValue = max;
                ValueSlider.value = value;
            }

            if (IconImage != null && icon != null)
            {
                IconImage.sprite = icon;
                IconImage.enabled = true;
            }
        }

        public void UpdateValue(float value)
        {
            if (ValueText != null)
                ValueText.text = $"{value:F0}";

            if (ValueSlider != null)
                ValueSlider.value = value;
        }
    }
}
