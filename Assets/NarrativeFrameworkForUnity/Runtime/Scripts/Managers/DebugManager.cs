using SimulationGame.DebugTools;
using SimulationGame.Runtime;
using UnityEngine;

namespace SimulationGame.Managers
{
    public class DebugManager : MonoBehaviour
    {
        [SerializeField] private GameObject _debugConsolePrefab;
        [SerializeField] private GameObject _telemetryCollectorPrefab;

        private DebugConsole _debugConsole;
        private TelemetryCollector _telemetryCollector;

        public void Initialize()
        {
            if (_debugConsolePrefab != null)
            {
                var consoleGO = Instantiate(_debugConsolePrefab, transform);
                _debugConsole = consoleGO.GetComponent<DebugConsole>();
            }

            if (_telemetryCollectorPrefab != null)
            {
                var telemetryGO = Instantiate(_telemetryCollectorPrefab, transform);
                _telemetryCollector = telemetryGO.GetComponent<TelemetryCollector>();
            }
        }

        public DebugConsole Console => _debugConsole;
        public TelemetryCollector Telemetry => _telemetryCollector;
    }
}
