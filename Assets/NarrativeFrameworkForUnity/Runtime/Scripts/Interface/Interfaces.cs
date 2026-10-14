using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame.Interfaces
{
    /// <summary>
    /// Defines a contract for any object that can provide narrative events.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// Gets the next event to be presented to the player.
        /// </summary>
        /// <returns>A SimEventSO representing the next event.</returns>
        SimEventSO GetNextEvent();
    }

    /// <summary>
    /// Defines a contract for any object that can trigger the effects of an outcome.
    /// </summary>
    public interface IOutcomeTrigger
    {
        /// <summary>
        /// Executes the logic associated with a given outcome.
        /// </summary>
        /// <param name="outcome">The outcome to trigger.</param>
        void TriggerOutcome(Outcome outcome);
    }

    /// <summary>
    /// Defines a contract for managing game state flags.
    /// </summary>
    public interface IFlagManager
    {
        /// <summary>
        /// Checks if a specific flag is currently set in the game state.
        /// </summary>
        /// <param name="flag">The ID of the flag to check.</param>
        /// <returns>True if the flag is set, otherwise false.</returns>
        bool HasFlag(string flag);

        /// <summary>
        /// Sets a specific flag in the game state.
        /// </summary>
        /// <param name="flag">The ID of the flag to set.</param>
        void SetFlag(string flag);
    }
}
