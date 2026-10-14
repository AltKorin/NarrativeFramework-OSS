using UnityEngine;
using System.Collections.Generic;

namespace SimulationGame.DebugTools
{
    public class DebugConsole : MonoBehaviour
    {
        private List<string> _logMessages = new List<string>();
        private Vector2 _scrollPosition;
        private bool _showConsole = false;
        private string _command = "";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                _showConsole = !_showConsole;
            }
        }

        void OnGUI()
        {
            if (!_showConsole) return;

            float consoleHeight = Screen.height * 0.5f;
            GUI.Box(new Rect(0, 0, Screen.width, consoleHeight), "");

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(consoleHeight - 30));
            foreach (var message in _logMessages)
            {
                GUILayout.Label(message);
            }
            GUILayout.EndScrollView();

            GUI.SetNextControlName("CommandInput");
            _command = GUI.TextField(new Rect(10, consoleHeight - 25, Screen.width - 20, 20), _command);

            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return)
            {
                HandleCommand(_command);
                _command = "";
            }
        }

        private void HandleCommand(string command)
        {
            Log($"Command: {command}");
            // Command handling logic here
        }

        public void Log(string message)
        {
            _logMessages.Add(message);
        }
    }
}
