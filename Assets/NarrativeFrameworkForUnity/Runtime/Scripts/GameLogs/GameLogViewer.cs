using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationGame.Runtime
{
    /// <summary>
    /// Displays the game log entries in a UI format.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]

    public class GameLogViewer : MonoBehaviour
    {
        [SerializeField] private GameLogSystem _logSystem;
        [SerializeField] private GameObject _entryPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private Button _refreshButton;

        private void Start()
        {
            _refreshButton.onClick.AddListener(RefreshLog);
            RefreshLog();
        }

        public void RefreshLog()
        {
            foreach (Transform child in _container)
                Destroy(child.gameObject);

            foreach (var entry in _logSystem.Entries)
            {
                var go = Instantiate(_entryPrefab, _container);
                var ui = go.GetComponent<GameLogEntryUI>();
                ui.SetData(entry);
            }
        }
    }
}
