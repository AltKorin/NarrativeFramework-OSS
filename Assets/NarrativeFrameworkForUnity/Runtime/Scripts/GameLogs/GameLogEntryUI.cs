using TMPro;
using UnityEngine;

namespace SimulationGame.Runtime
{
    /// <summary>
    /// UI component for displaying a single game log entry.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]

    public class GameLogEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _choiceText;
        [SerializeField] private TMP_Text _outcomesText;

        public void SetData(GameLogEntry entry)
        {
            _titleText.text = $"Day {entry.Day}: {entry.EventTitle}";
            _choiceText.text = $"Choice: {entry.ChoiceText}";
            _outcomesText.text = string.Join("\n", entry.OutcomesDescriptions);
        }
    }
}