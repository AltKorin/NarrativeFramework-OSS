// Assets/SimulationGame/Editor/OpenAI/OpenAISettingsEditor.cs
using UnityEditor;
using UnityEngine;
using SimulationGame.OpenAI;
using System.Threading.Tasks;

namespace SimulationGame.Editor
{
    [CustomEditor(typeof(OpenAISettings))]
    public class OpenAISettingsEditor : UnityEditor.Editor
    {
        // Prompt helpers (optional, same as before)
        private string prompt  = "Describe a mysterious event in a snowy forest…";
        private string result  = "";
        private bool   loading = false;

        public override void OnInspectorGUI()
        {
            // Always work with a SerializedObject so we touch the real asset fields
            serializedObject.Update();

            /* ── Core fields ────────────────────────────────────────────── */
            EditorGUILayout.LabelField("Core", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("apiKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("model"));
            EditorGUILayout.Slider(
                serializedObject.FindProperty("temperature"),
                0f, 1f,
                new GUIContent("Temperature"));

            /* ── Optional Assistants section (if you kept those fields) ── */
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useAssistantApi"));
            if (serializedObject.FindProperty("useAssistantApi").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("assistantId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("threadId"));
                EditorGUI.indentLevel--;
            }

            /* ── Test prompt UI  (pure editor helper) ──────────────────── */
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Prompt", EditorStyles.boldLabel);
            prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(50));

            GUI.enabled = !loading;
            if (GUILayout.Button("🧠  Test Call (chat completions)"))
            {
                loading = true;
                _ = CallTestAsync(prompt);
            }
            GUI.enabled = true;

            EditorGUILayout.HelpBox(result, MessageType.None);

            /* ── Apply changes ─────────────────────────────────────────── */
            serializedObject.ApplyModifiedProperties();
        }

        /* Small async helper to test the key directly from the inspector */
        private async Task CallTestAsync(string p)
        {
            result  = "⏳ contacting OpenAI…";
            Repaint();

            var answer = await OpenAIGenerator.GenerateEvent(p);
            result     = string.IsNullOrEmpty(answer)
                       ? "❌ request failed (check console)"
                       : answer;

            loading = false;
            Repaint();
        }
    }
}
