using UnityEngine;
using SimulationGame.Interfaces;
using System;

namespace SimulationGame
{
    [CreateAssetMenu(fileName = "CustomOutcome", menuName = "Narrative Framework/Outcomes/Custom")]
    public class CustomOutcome : Outcome
    {
        [Tooltip("Custom logic that will be executed when this outcome is triggered")]
        private BaseOutcome _customLogic;
        public BaseOutcome CustomLogic
        {
            get => _customLogic;
            set => _customLogic = value;
        }
    }
}
