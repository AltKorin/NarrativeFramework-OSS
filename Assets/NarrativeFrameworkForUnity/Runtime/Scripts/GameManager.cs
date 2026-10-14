using SimulationGame.Managers;
using UnityEngine;

namespace SimulationGame
{
    namespace SimulationGame.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private DebugManager _debugManager;
        [SerializeField] private GameController _gameController;

        void Start()
        {
            _debugManager.Initialize();
            _gameController.StartGame();
        }
    }
}
}
