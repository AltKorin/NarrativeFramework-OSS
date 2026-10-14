using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SimulationGame.UI
{
    public class ResourceDisplay : MonoBehaviour
    {
        public string ResourceId;
        public TextMeshProUGUI LabelText;

        public TextMeshProUGUI ValueText;
        public Slider ValueSlider;
        public float MinValue = 0f;
        public float MaxValue = 100f;
        public float StartingValue = 50f;

        public void Setup(string resourceId, float minValue, float maxValue, float startingValue, string displayName)
        {
            ResourceId = resourceId;
            MinValue = minValue;
            MaxValue = maxValue;
            StartingValue = startingValue;

            if (LabelText != null)
            {
                LabelText.text = displayName;
                Debug.Log(LabelText.text);
            }

            if (ValueText != null)
            {
                ValueText.text = $"{startingValue}";
                Debug.Log(ValueText.text);
            }
            else
                ValueText.text = $"Error";

            if (ValueSlider != null)
            {
                ValueSlider.minValue = MinValue;
                ValueSlider.maxValue = MaxValue;
                ValueSlider.value = StartingValue;
            }
        }


        public void UpdateDisplay(float currentValue)
        {
            if (ValueText != null)
                ValueText.text = $"{currentValue:F0}";

            if (ValueSlider != null)
                ValueSlider.value = currentValue;
        }
    }
}