using System;
using System.Collections.Generic;
using SimulationGame;            // SimEventSO, Choice, Resource
// No UnityEvent here on purpose: the manager already exposes C# events (Action / Action<T>).

namespace SimulationGame.Interfaces
{
    /// <summary>
    /// Designer-first UI contract used by GameController and other systems.
    /// This interface mirrors the current UIManager implementation to avoid breaking changes.
    /// </summary>
    public interface IUIManager
    {
        // -----------------------------
        // Player interaction events
        // -----------------------------

        /// <summary>
        /// Fired when the player selects a choice button in the UI.
        /// </summary>
        event Action<Choice> OnChoiceSelected;

        /// <summary>
        /// Fired when the player clicks the "Next Day" button.
        /// </summary>
        event Action OnNextDayClicked;

        /// <summary>
        /// Fired when the player requests a restart from the Game Over panel.
        /// </summary>
        event Action OnRestartRequested;

        // -----------------------------
        // UI display & utility methods
        // -----------------------------

        /// <summary>
        /// Updates the day label (e.g., "Day 3", "Week 2 / Day 1").
        /// </summary>
        void SetDayText(string dayInfo);

        /// <summary>
        /// Shows the main event (title, description, images, ambient audio).
        /// </summary>
        void DisplayEvent(SimEventSO evt);

        /// <summary>
        /// Spawns and binds the choice buttons for the given list.
        /// </summary>
        void DisplayChoices(IList<Choice> choices);

        /// <summary>
        /// Updates the HUD/resource widgets (by resource id).
        /// </summary>
        void UpdateResources(Dictionary<string, float> currentValues);

        /// <summary>
        /// Shows or hides the "Next Day" button.
        /// </summary>
        void SetNextDayButtonVisible(bool visible);

        /// <summary>
        /// Rebuilds resource widgets for the provided resources (designer-first flow).
        /// </summary>
        void CreateResourceDisplays(List<Resource> resources);

        /// <summary>
        /// Shows the Game Over panel with provided title and body.
        /// </summary>
        void ShowGameOver(string title, string body);

        /// <summary>
        /// Hides the Game Over panel (optional flow).
        /// </summary>
        void HideGameOver();
    }
}
