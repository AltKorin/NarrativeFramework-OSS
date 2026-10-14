using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Renders the UI for the 'Custom Outcomes' tab in the Narrative Linker window.
    /// This view allows designers to create and manage custom outcome logic.
    /// </summary>
    public class NarrativeLinkerCustomOutcomeView
    {
        private const string CustomOutcomesDir = "Assets/SimulationGame/Data/Outcomes/Custom";

        private Vector2 _scrollPosition;
        private List<CustomOutcome> _customOutcomes = new List<CustomOutcome>();

        /// <summary>
        /// Initializes the view by loading all custom outcome assets.
        /// </summary>
        public void OnEnable()
        {
            RefreshCustomOutcomeList();
        }

        /// <summary>
        /// Draws the entire Custom Outcome view.
        /// </summary>
        public void Draw()
        {
            if (GUILayout.Button("Create New Custom Outcome"))
            {
                CreateNewCustomOutcome();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_customOutcomes.Count == 0)
            {
                EditorGUILayout.HelpBox("No custom outcomes found. Click 'Create' to add one.", MessageType.Info);
            }
            else
            {
                foreach (var outcome in _customOutcomes)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(outcome.name);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = outcome;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Reloads all CustomOutcome assets from the project directory.
        /// </summary>
        private void RefreshCustomOutcomeList()
        {
            _customOutcomes.Clear();
            if (!Directory.Exists(CustomOutcomesDir))
            {
                Directory.CreateDirectory(CustomOutcomesDir);
            }

            var guids = AssetDatabase.FindAssets("t:CustomOutcome", new[] { CustomOutcomesDir });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var outcome = AssetDatabase.LoadAssetAtPath<CustomOutcome>(path);
                if (outcome != null)
                {
                    _customOutcomes.Add(outcome);
                }
            }
            _customOutcomes = _customOutcomes.OrderBy(o => o.name).ToList();
        }

        /// <summary>
        /// Creates a new CustomOutcome asset.
        /// </summary>
        private void CreateNewCustomOutcome()
        {
            // Note: This only creates a base CustomOutcome asset.
            // The user must create a new C# script inheriting from CustomOutcome to define behavior.
            var newOutcome = ScriptableObject.CreateInstance<CustomOutcome>();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(CustomOutcomesDir, "NewCustomOutcome.asset"));

            AssetDatabase.CreateAsset(newOutcome, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newOutcome;

            RefreshCustomOutcomeList();
        }
    }
}

