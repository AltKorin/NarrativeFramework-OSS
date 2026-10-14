using UnityEditor;
using UnityEngine;
using System;

namespace SimulationGame.Editor
{
    /// <summary>
    /// An editor popup window that allows designers to generate event descriptions
    /// using the OpenAI API.
    /// </summary>
    public class OpenAIDescriptionPopup : EditorWindow
    {
        /// <summary>
        /// The callback to execute when a description is successfully generated.
        /// </summary>
        private Action<string> _onDescriptionGenerated;
        private string _prompt;
        private string _apiKey;
        private Vector2 _scrollPosition;

        /// <summary>
        /// Displays the popup window.
        /// </summary>
        /// <param name="prompt">The initial prompt to send to the AI.</param>
        /// <param name="apiKey">The user's OpenAI API key.</param>
        /// <param name="onGenerated">The callback to invoke with the generated text.</param>
        public static void ShowWindow(string prompt, string apiKey, Action<string> onGenerated)
        {
            var window = GetWindow<OpenAIDescriptionPopup>(true, "Generate Description", true);
            window._prompt = prompt;
            window._apiKey = apiKey;
            window._onDescriptionGenerated = onGenerated;
            window.minSize = new Vector2(400, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OpenAI Description Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Prompt:", EditorStyles.wordWrappedLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(100));
            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate"))
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    EditorUtility.DisplayDialog("API Key Missing", "Please set your OpenAI API key in the project settings.", "OK");
                    return;
                }

                // This is where the call to the OpenAI API would happen.
                // For this example, we'll just simulate a successful response.
                string generatedText = $"Generated from prompt: '{_prompt}'";
                _onDescriptionGenerated?.Invoke(generatedText);
                Close();
            }
        }

        public static void Show(SimEventSO targetEvent, string suggestedTitle, string suggestedDescription, Action onApplied)
        {
            var wnd = CreateInstance<OpenAIDescriptionPopup>();
            wnd.titleContent = new GUIContent("Apply AI Description");
            wnd._onDescriptionGenerated = _ => onApplied?.Invoke();
            wnd.minSize = new Vector2(400, 200);

            // Możesz też zapisać suggestedTitle/Description w polach i wyświetlić w OnGUI
            wnd._prompt = suggestedTitle + "\n\n" + suggestedDescription;

            wnd.ShowUtility();
        }
    }

}

