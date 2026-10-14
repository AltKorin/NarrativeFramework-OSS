using UnityEngine;
using UnityEditor;

namespace SimulationGame.Editor
{
    public class OpenAIResultEditorWindow : EditorWindow
    {
        private string generatedTitle = "";
        private string generatedDescription = "";
        private SimEventSO targetEvent;

        public static void ShowWindow(SimEventSO target, string title, string description)
        {
            var window = CreateInstance<OpenAIResultEditorWindow>();
            window.titleContent = new GUIContent("AI Event Result");
            window.generatedTitle = title;
            window.generatedDescription = description;
            window.targetEvent = target;
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 300);
            window.ShowUtility(); // modal window
        }

        private void OnGUI()
        {
            GUILayout.Label("Generated Event Data", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            generatedTitle = EditorGUILayout.TextField("Title", generatedTitle);
            EditorGUILayout.LabelField("Description");
            generatedDescription = EditorGUILayout.TextArea(generatedDescription, GUILayout.Height(150));

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
            {
                ApplyToTarget();
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyToTarget()
        {
            if (targetEvent != null)
            {
                targetEvent.simEventTitle = generatedTitle;
                targetEvent.simEventDescription = generatedDescription;
                EditorUtility.SetDirty(targetEvent);
                AssetDatabase.SaveAssets();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }
}
