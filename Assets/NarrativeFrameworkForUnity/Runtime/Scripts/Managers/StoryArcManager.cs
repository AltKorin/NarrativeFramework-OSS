using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimulationGame.DebugTools;

namespace SimulationGame
{
    /// <summary>
    /// Manages story arcs: activation, progression, and completion.
    /// </summary>
    public class StoryArcManager
    {
        private GameData gameData;

        // Context state (updated externally)
        private int currentDay;
        private IEnumerable<string> currentFlags;
        private HashSet<SimEventSO> occurredEvents = new();

        // Active arcs and their current index in EventsInArc (for sequenced arcs)
        private Dictionary<StoryArcSO, int> activeArcProgress = new();

        // Legacy-ish list for persistence / external querying
        private List<StoryArcSO> activeStoryArcs = new();

        public StoryArcManager(GameData data)
        {
            gameData = data;
        }

        public void UpdateState(int day, IEnumerable<string> flags, HashSet<SimEventSO> occurred)
        {
            currentDay = day;
            currentFlags = flags;
            occurredEvents = occurred;
        }

        /// <summary>
        /// Attempts to trigger any arc that has a TriggeredEvent and is currently available.
        /// Returns the event to fire immediately (the TriggeredEvent) or null.
        /// </summary>
        public SimEventSO TryGetTriggeredStoryArcEvent()
        {
            foreach (var arc in gameData.StoryArcs)
            {
                if (arc.TriggeredEvent == null)
                    continue;

                if (arc.IsActivated && !arc.AllowMultipleActivations)
                    continue;

                if (!arc.IsAvailable(currentDay, currentFlags, occurredEvents))
                    continue;

                Log.Info($"[StoryArcManager] Triggered StoryArc via TriggeredEvent: {arc.ArcName}");
                StartArcInternal(arc);
                return arc.TriggeredEvent;
            }

            return null;
        }

        /// <summary>
        /// Checks all arcs that should start in the normal flow (first event in EventsInArc).
        /// Returns list of new events to present (one per newly started arc).
        /// </summary>
        public List<SimEventSO> CheckForNewStoryEvents()
        {
            List<SimEventSO> newEvents = new();

            foreach (var arc in gameData.StoryArcs)
            {
                if (arc.IsActivated && !arc.AllowMultipleActivations)
                    continue;

                if (!arc.IsAvailable(currentDay, currentFlags, occurredEvents))
                    continue;

                Log.Info($"[StoryArcManager] Activating arc: {arc.ArcName}");
                StartArcInternal(arc);

                if (arc.EventsInArc != null && arc.EventsInArc.Count > 0)
                {
                    newEvents.Add(arc.EventsInArc[0]);
                }
            }

            return newEvents;
        }

        /// <summary>
        /// Force-starts a specific StoryArc, ignoring availability checks (useful from an Outcome).
        /// Returns the entry event: TriggeredEvent if present, otherwise first in EventsInArc.
        /// </summary>
        public SimEventSO StartArcForced(StoryArcSO arc)
        {
            if (arc == null)
                return null;

            Log.Info($"[StoryArcManager] Force-starting arc: {arc.ArcName}");
            StartArcInternal(arc);

            if (arc.TriggeredEvent != null)
                return arc.TriggeredEvent;

            if (arc.EventsInArc != null && arc.EventsInArc.Count > 0)
                return arc.EventsInArc[0];

            return null;
        }

        /// <summary>
        /// Should be called when a SimEventSO has finished. Advances any arc that was waiting on it
        /// and returns the next event in that arc (or null if arc ended / nothing to advance).
        /// </summary>
        public SimEventSO NotifyEventFinished(SimEventSO finishedEvent)
        {
            // First, consider arcs where the finished event was the TriggeredEvent and they chain into EventsInArc
            foreach (var kv in activeArcProgress.ToList())
            {
                var arc = kv.Key;
                int progress = kv.Value;

                if (arc.TriggeredEvent == finishedEvent)
                {
                    // After TriggeredEvent, continue into arc sequence if any
                    if (arc.EventsInArc != null && arc.EventsInArc.Count > 0)
                    {
                        activeArcProgress[arc] = 0;
                        return arc.EventsInArc[0];
                    }
                    else
                    {
                        CompleteArc(arc);
                        return null;
                    }
                }

                // Normal progression: finishedEvent matches current index in EventsInArc
                if (arc.EventsInArc != null && arc.EventsInArc.Count > progress &&
                    arc.EventsInArc[progress] == finishedEvent)
                {
                    int nextIndex = progress + 1;
                    if (nextIndex >= arc.EventsInArc.Count)
                    {
                        CompleteArc(arc);
                        return null;
                    }
                    else
                    {
                        activeArcProgress[arc] = nextIndex;
                        return arc.EventsInArc[nextIndex];
                    }
                }
            }

            return null;
        }

        private void StartArcInternal(StoryArcSO arc)
        {
            if (arc == null)
                return;

            if (!activeStoryArcs.Contains(arc))
                activeStoryArcs.Add(arc);

            if (!activeArcProgress.ContainsKey(arc))
                activeArcProgress[arc] = -1; // -1 means we haven't entered EventsInArc yet (e.g., waiting if triggeredEvent exists)

            arc.MarkAsActivated();
        }

        private void CompleteArc(StoryArcSO arc)
        {
            if (arc == null)
                return;

            Log.Info($"[StoryArcManager] Completing arc: {arc.ArcName}");
            activeArcProgress.Remove(arc);
            activeStoryArcs.Remove(arc);
            // Note: arc.IsActivated stays true (persisted); reactivation rules governed by AllowMultipleActivations
        }

        public void Reset()
        {
            activeArcProgress.Clear();
            activeStoryArcs.Clear();
            occurredEvents.Clear();
            currentDay = 0;
        }

        public List<string> GetActiveStoryArcIds()
        {
            return activeStoryArcs.ConvertAll(arc => arc.Id);
        }

        public void LoadActiveStoryArcs(List<string> activeStoryArcIds)
        {
            activeArcProgress.Clear();
            activeStoryArcs.Clear();

            foreach (var arcId in activeStoryArcIds)
            {
                var arc = gameData.StoryArcs.FirstOrDefault(a => a.Id == arcId);
                if (arc != null)
                {
                    arc.MarkAsActivated();
                    activeStoryArcs.Add(arc);
                    // assume we resume at start of sequence
                    activeArcProgress[arc] = 0;
                }
                else
                {
                    Log.Warning($"[StoryArcManager] Could not find StoryArc with ID: {arcId}");
                }
            }
        }
        public SimEventSO CheckAndActivateImmediateStoryArc(
            int day,
            IEnumerable<string> flags,
            HashSet<SimEventSO> occurred)
        {
            // Keep internal state in sync
            UpdateState(day, flags, occurred);

            // Reuse the existing trigger logic; this will:
            // - scan arcs
            // - verify availability
            // - mark arc as activated and add to active list
            // - return the first triggered event (or null)
            return TryGetTriggeredStoryArcEvent();
        }

            }
}
