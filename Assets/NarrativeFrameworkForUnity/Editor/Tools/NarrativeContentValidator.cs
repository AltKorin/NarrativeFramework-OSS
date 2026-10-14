using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SimulationGame.Editor.Tools
{
    /// <summary>
    /// Editor tool for validating narrative content assets
    /// Scans Resources folder and validates all SimEventSO, Choice, and related assets
    /// </summary>
    public class NarrativeContentValidator : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<ValidationResult> validationResults = new List<ValidationResult>();
        private bool showOnlyErrors = false;
        private bool autoValidateOnChanges = false;
        private ValidationCategory filterCategory = ValidationCategory.All;
        
        private static readonly string[] CATEGORIES = { "All", "Events", "Choices", "Outcomes", "Resources", "References" };

        [MenuItem("Tools/Narrative Framework/Narrative Content Validator")]
        public static void ShowWindow()
        {
            var window = GetWindow<NarrativeContentValidator>("Narrative Validator");
            window.minSize = new Vector2(600, 400);
        }
        
        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }
        
        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }
        
        private void OnProjectChanged()
        {
            if (autoValidateOnChanges)
            {
                ValidateAllContent();
            }
        }
        
        private void OnGUI()
        {
            GUILayout.Space(10);
            
            // Header
            EditorGUILayout.LabelField("Narrative Content Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool validates all narrative content assets in your project, checking for common issues and inconsistencies.", MessageType.Info);
            
            GUILayout.Space(10);
            
            // Controls
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Validate All Content", GUILayout.Height(30)))
            {
                ValidateAllContent();
            }
            
            if (GUILayout.Button("Clear Results", GUILayout.Width(100)))
            {
                validationResults.Clear();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginChangeCheck();
            showOnlyErrors = EditorGUILayout.Toggle("Show Only Errors", showOnlyErrors);
            
            filterCategory = (ValidationCategory)EditorGUILayout.Popup("Category", (int)filterCategory, CATEGORIES);
            
            autoValidateOnChanges = EditorGUILayout.Toggle("Auto-validate", autoValidateOnChanges);
            
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Results summary
            if (validationResults.Count > 0)
            {
                var errorCount = validationResults.Count(r => r.severity == ValidationSeverity.Error);
                var warningCount = validationResults.Count(r => r.severity == ValidationSeverity.Warning);
                var infoCount = validationResults.Count(r => r.severity == ValidationSeverity.Info);
                
                EditorGUILayout.BeginHorizontal();
                
                if (errorCount > 0)
                {
                    GUI.color = Color.red;
                    GUILayout.Label($"Errors: {errorCount}", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                
                if (warningCount > 0)
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label($"Warnings: {warningCount}", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                
                if (infoCount > 0)
                {
                    GUILayout.Label($"Info: {infoCount}");
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Total: {validationResults.Count}");
                
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.Space(10);
            
            // Results list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            var filteredResults = GetFilteredResults();
            
            foreach (var result in filteredResults)
            {
                DrawValidationResult(result);
            }
            
            if (filteredResults.Count == 0 && validationResults.Count > 0)
            {
                EditorGUILayout.HelpBox("No results match the current filter criteria.", MessageType.Info);
            }
            else if (validationResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Validate All Content' to scan your narrative assets.", MessageType.Info);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private List<ValidationResult> GetFilteredResults()
        {
            return validationResults.Where(r =>
            {
                if (showOnlyErrors && r.severity != ValidationSeverity.Error)
                    return false;
                    
                if (filterCategory != ValidationCategory.All && r.category != filterCategory)
                    return false;
                    
                return true;
            }).ToList();
        }
        
        private void DrawValidationResult(ValidationResult result)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            
            // Severity icon and color
            GUI.color = GetSeverityColor(result.severity);
            GUILayout.Label(GetSeverityIcon(result.severity), GUILayout.Width(20));
            GUI.color = Color.white;
            
            // Category badge
            GUI.backgroundColor = GetCategoryColor(result.category);
            GUILayout.Label(result.category.ToString(), EditorStyles.miniButton, GUILayout.Width(80));
            GUI.backgroundColor = Color.white;
            
            // Asset name (clickable)
            if (!string.IsNullOrEmpty(result.assetPath))
            {
                if (GUILayout.Button(result.assetName, EditorStyles.linkLabel))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(result.assetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }
            else
            {
                GUILayout.Label(result.assetName, EditorStyles.boldLabel);
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
            
            // Message
            EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedLabel);
            
            // Additional context
            if (!string.IsNullOrEmpty(result.context))
            {
                GUI.color = Color.gray;
                EditorGUILayout.LabelField(result.context, EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(2);
        }
        
        private void ValidateAllContent()
        {
            validationResults.Clear();
            
            EditorUtility.DisplayProgressBar("Validating Content", "Loading GameData...", 0f);
            
            try
            {
                // Load GameData
                var gameData = Resources.Load<GameData>("GameData");
                if (gameData == null)
                {
                    validationResults.Add(new ValidationResult
                    {
                        severity = ValidationSeverity.Error,
                        category = ValidationCategory.Resources,
                        assetName = "GameData",
                        message = "GameData.asset not found in Resources folder. This is required for the narrative system to function.",
                        context = "Expected path: Assets/Resources/GameData.asset"
                    });
                    return;
                }
                
                // Validate GameData structure
                ValidateGameData(gameData);
                
                // Validate Events
                if (gameData.Events != null)
                {
                    for (int i = 0; i < gameData.Events.Count; i++)
                    {
                        EditorUtility.DisplayProgressBar("Validating Content", $"Validating event {i + 1}/{gameData.Events.Count}...", (float)i / gameData.Events.Count);
                        ValidateEvent(gameData.Events[i], gameData);
                    }
                }
                
                // Validate Resources
                if (gameData.Resources != null)
                {
                    for (int i = 0; i < gameData.Resources.Count; i++)
                    {
                        ValidateResource(gameData.Resources[i]);
                    }
                }
                
                // Validate Story Arcs
                if (gameData.StoryArcs != null)
                {
                    for (int i = 0; i < gameData.StoryArcs.Count; i++)
                    {
                        ValidateStoryArc(gameData.StoryArcs[i], gameData);
                    }
                }
                
                // Cross-reference validation
                ValidateCrossReferences(gameData);
            }
            catch (System.Exception e)
            {
                validationResults.Add(new ValidationResult
                {
                    severity = ValidationSeverity.Error,
                    category = ValidationCategory.All,
                    assetName = "Validation System",
                    message = $"Validation failed with exception: {e.Message}",
                    context = e.StackTrace
                });
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }
        
        private void ValidateGameData(GameData gameData)
        {
            string assetPath = AssetDatabase.GetAssetPath(gameData);
            
            // Check basic structure
            if (gameData.Events == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Events, "GameData", 
                    "Events list is null", "Initialize the Events list in GameData", assetPath);
            }
            else if (gameData.Events.Count == 0)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Events, "GameData", 
                    "No events defined", "Add some SimEventSO assets to the Events list", assetPath);
            }
            
            if (gameData.Resources == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Resources, "GameData", 
                    "Resources list is null", "Initialize the Resources list in GameData", assetPath);
            }

            if (gameData.Actors == null)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.All, "GameData", 
                    "Characters list is null", "Initialize the Characters list if using character system", assetPath);
            }
            
            // Check campaign settings
            if (gameData.CampaignLength <= 0)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.All, "GameData", 
                    "Campaign length must be greater than 0", $"Current value: {gameData.CampaignLength}", assetPath);
            }
            else if (gameData.CampaignLength > 365)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.All, "GameData", 
                    "Campaign length is very long", $"Consider if {gameData.CampaignLength} days is intended", assetPath);
            }
        }
        
        private void ValidateEvent(SimEventSO eventSO, GameData gameData)
        {
            if (eventSO == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Events, "NULL Event", 
                    "Found null event reference in GameData", "Remove null references from Events list");
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(eventSO);
            string eventName = eventSO.name;
            
            // Basic validation
            if (string.IsNullOrEmpty(eventSO.SimEventTitle))
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Events, eventName, 
                    "Event title is empty", "Provide a descriptive title for the event", assetPath);
            }
            
            if (string.IsNullOrEmpty(eventSO.SimEventDescription))
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                    "Event description is empty", "Add a description to help with content management", assetPath);
            }
            
            // Day range validation
            if (eventSO.MinTimeUnit > eventSO.MaxTimeUnit)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Events, eventName, 
                    "Invalid day range", $"MinTimeUnit ({eventSO.MinTimeUnit}) > MaxTimeUnit ({eventSO.MaxTimeUnit})", assetPath);
            }
            
            if (eventSO.MinTimeUnit < 1)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Events, eventName, 
                    "MinTimeUnit must be at least 1", $"Current value: {eventSO.MinTimeUnit}", assetPath);
            }
            
            if (eventSO.MaxTimeUnit > gameData.CampaignLength)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                    "MaxTimeUnit exceeds campaign length", $"Event max day ({eventSO.MaxTimeUnit}) > campaign length ({gameData.CampaignLength})", assetPath);
            }
            
            // Validate choices
            if (eventSO.Choices == null)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Choices, eventName, 
                    "No choices defined", "Events without choices cannot progress the narrative", assetPath);
            }
            else if (eventSO.Choices.Count == 0)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Choices, eventName, 
                    "Empty choices list", "Add at least one choice to allow player interaction", assetPath);
            }
            else
            {
                for (int i = 0; i < eventSO.Choices.Count; i++)
                {
                    ValidateChoice(eventSO.Choices[i], eventName, i, gameData, assetPath);
                }
            }
            
            // Validate required flags
            if (eventSO.RequiredFlags != null)
            {
                foreach (var flag in eventSO.RequiredFlags)
                {
                    if (string.IsNullOrEmpty(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                            "Empty flag in RequiredFlags", "Remove empty flag entries", assetPath);
                    }
                    else if (!IsValidFlagName(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                            "Invalid flag name format", $"Flag '{flag}' should follow naming convention (alphanumeric, underscores)", assetPath);
                    }
                }
            }
            
            // Validate forbidden flags
            if (eventSO.ForbiddenFlags != null)
            {
                foreach (var flag in eventSO.ForbiddenFlags)
                {
                    if (string.IsNullOrEmpty(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                            "Empty flag in ForbiddenFlags", "Remove empty flag entries", assetPath);
                    }
                    else if (!IsValidFlagName(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Events, eventName, 
                            "Invalid flag name format", $"Flag '{flag}' should follow naming convention", assetPath);
                    }
                }
            }
        }
        
        private void ValidateChoice(Choice choice, string eventName, int choiceIndex, GameData gameData, string assetPath)
        {
            string choiceContext = $"Event: {eventName}, Choice {choiceIndex + 1}";
            
            if (choice == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Choices, eventName, 
                    "Null choice found", choiceContext, assetPath);
                return;
            }
            
            // Basic validation
            if (string.IsNullOrEmpty(choice.ChoiceText))
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Choices, eventName, 
                    "Choice text is empty", choiceContext, assetPath);
            }
            
            // Validate outcomes and next event
            bool hasOutcomes = choice.Outcomes != null && choice.Outcomes.Count > 0;
            bool hasNextEvent = choice.NextEvent != null;
            
            if (!hasOutcomes && !hasNextEvent)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Choices, eventName, 
                    "Choice has no outcomes or next event", $"{choiceContext} - choices must lead somewhere", assetPath);
            }
            
            // Validate outcomes
            if (choice.Outcomes != null)
            {
                for (int i = 0; i < choice.Outcomes.Count; i++)
                {
                    ValidateOutcome(choice.Outcomes[i], choiceContext, i, gameData, assetPath);
                }
            }
            
            // Validate next event reference
            if (choice.NextEvent != null)
            {
                if (!gameData.Events.Contains(choice.NextEvent))
                {
                    AddResult(ValidationSeverity.Error, ValidationCategory.References, eventName, 
                        "NextEvent reference not found in GameData", $"{choiceContext} - NextEvent: {choice.NextEvent.name}", assetPath);
                }
            }
            
            // Validate required flags
            if (choice.RequiredFlags != null)
            {
                foreach (var flag in choice.RequiredFlags)
                {
                    if (string.IsNullOrEmpty(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Choices, eventName, 
                            "Empty flag in choice RequiredFlags", choiceContext, assetPath);
                    }
                }
            }
        }
        
        private void ValidateOutcome(Outcome outcome, string choiceContext, int outcomeIndex, GameData gameData, string assetPath)
        {
            if (outcome == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Outcomes, "Choice", 
                    "Null outcome found", $"{choiceContext}, Outcome {outcomeIndex + 1}", assetPath);
                return;
            }
            
            // Validate resource references
            if (!string.IsNullOrEmpty(outcome.TargetId))
            {
                bool resourceExists = gameData.Resources != null && 
                                    gameData.Resources.Any(r => r != null && r.DisplayName == outcome.TargetId);
                
                if (!resourceExists)
                {
                    AddResult(ValidationSeverity.Error, ValidationCategory.References, "Outcome", 
                        "Resource ID not found in GameData", $"{choiceContext}, Outcome {outcomeIndex + 1} - TargetId: {outcome.TargetId}", assetPath);
                }
            }
            
            // Validate flags
            if (outcome.FlagsToAdd != null)
            {
                foreach (var flag in outcome.FlagsToAdd)
                {
                    if (string.IsNullOrEmpty(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Outcomes, "Outcome", 
                            "Empty flag in FlagsToAdd", $"{choiceContext}, Outcome {outcomeIndex + 1}", assetPath);
                    }
                }
            }
            
            if (outcome.FlagsToRemove != null)
            {
                foreach (var flag in outcome.FlagsToRemove)
                {
                    if (string.IsNullOrEmpty(flag))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Outcomes, "Outcome", 
                            "Empty flag in FlagsToRemove", $"{choiceContext}, Outcome {outcomeIndex + 1}", assetPath);
                    }
                }
            }
        }
        
        private void ValidateResource(Resource resource)
        {
            if (resource == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Resources, "NULL Resource", 
                    "Found null resource reference in GameData", "Remove null references from Resources list");
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(resource);
            
            if (string.IsNullOrEmpty(resource.TargetId))
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.Resources, resource.name, 
                    "Resource ID is empty", "Provide a unique identifier for the resource", assetPath);
            }
            
            if (string.IsNullOrEmpty(resource.DisplayName))
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Resources, resource.name, 
                    "Display name is empty", "Provide a user-friendly display name", assetPath);
            }
            
            if (resource.InitialValue < 0)
            {
                AddResult(ValidationSeverity.Warning, ValidationCategory.Resources, resource.name, 
                    "Starting value is negative", $"Consider if negative starting value ({resource.InitialValue}) is intended", assetPath);
            }
        }
        
        private void ValidateStoryArc(StoryArcSO storyArc, GameData gameData)
        {
            if (storyArc == null)
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.All, "NULL StoryArc", 
                    "Found null story arc reference in GameData", "Remove null references from StoryArcs list");
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(storyArc);
            
            if (string.IsNullOrEmpty(storyArc.ArcName))
            {
                AddResult(ValidationSeverity.Error, ValidationCategory.All, storyArc.name, 
                    "Story arc name is empty", "Provide a name for the story arc", assetPath);
            }
            
            // Validate event references in story arc
            if (storyArc.EventsInArc != null)
            {
                foreach (var eventRef in storyArc.EventsInArc)
                {
                    if (eventRef != null && !gameData.Events.Contains(eventRef))
                    {
                        AddResult(ValidationSeverity.Error, ValidationCategory.References, storyArc.name, 
                            "Story arc references event not in GameData", $"Event: {eventRef.name}", assetPath);
                    }
                }
            }
        }
        
        private void ValidateCrossReferences(GameData gameData)
        {
            // Check for duplicate resource IDs
            if (gameData.Resources != null)
            {
                var resourceIds = new HashSet<string>();
                foreach (var resource in gameData.Resources)
                {
                    if (resource != null && !string.IsNullOrEmpty(resource.TargetId))
                    {
                        if (!resourceIds.Add(resource.TargetId))
                        {
                            AddResult(ValidationSeverity.Error, ValidationCategory.Resources, "GameData", 
                                "Duplicate resource ID found", $"ResourceID '{resource.DisplayName}' is used by multiple resources");
                        }
                    }
                }
            }
            
            // Check for orphaned events (events that can't be reached)
            if (gameData.Events != null && gameData.Events.Count > 0)
            {
                var reachableEvents = new HashSet<SimEventSO>();
                
                // Find all events reachable from day 1
                foreach (var evt in gameData.Events)
                {
                    if (evt != null && evt.MinTimeUnit <= 1)
                    {
                        CollectReachableEvents(evt, reachableEvents, gameData.Events);
                    }
                }
                
                // Report unreachable events
                foreach (var evt in gameData.Events)
                {
                    if (evt != null && !reachableEvents.Contains(evt))
                    {
                        AddResult(ValidationSeverity.Warning, ValidationCategory.Events, evt.name, 
                            "Event may be unreachable", "This event is not referenced by any choices and doesn't start on day 1", 
                            AssetDatabase.GetAssetPath(evt));
                    }
                }
            }
        }
        
        private void CollectReachableEvents(SimEventSO startEvent, HashSet<SimEventSO> reachableEvents, IReadOnlyList<SimEventSO> allEvents)
        {
            if (startEvent == null || reachableEvents.Contains(startEvent))
                return;
            
            reachableEvents.Add(startEvent);
            
            // Follow all choice paths
            if (startEvent.Choices != null)
            {
                foreach (var choice in startEvent.Choices)
                {
                    if (choice?.NextEvent != null)
                    {
                        CollectReachableEvents(choice.NextEvent, reachableEvents, allEvents);
                    }
                }
            }
        }
        
        private void AddResult(ValidationSeverity severity, ValidationCategory category, string assetName, string message, string context = "", string assetPath = "")
        {
            validationResults.Add(new ValidationResult
            {
                severity = severity,
                category = category,
                assetName = assetName,
                message = message,
                context = context,
                assetPath = assetPath
            });
        }
        
        private bool IsValidFlagName(string flagName)
        {
            return !string.IsNullOrEmpty(flagName) && Regex.IsMatch(flagName, @"^[a-zA-Z][a-zA-Z0-9_]*$");
        }
        
        private Color GetSeverityColor(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Error => Color.red,
                ValidationSeverity.Warning => Color.yellow,
                ValidationSeverity.Info => Color.cyan,
                _ => Color.white
            };
        }
        
        private Color GetCategoryColor(ValidationCategory category)
        {
            return category switch
            {
                ValidationCategory.Events => new Color(0.8f, 0.9f, 1f),
                ValidationCategory.Choices => new Color(0.9f, 1f, 0.8f),
                ValidationCategory.Outcomes => new Color(1f, 0.9f, 0.8f),
                ValidationCategory.Resources => new Color(0.9f, 0.8f, 1f),
                ValidationCategory.References => new Color(1f, 0.8f, 0.8f),
                _ => Color.white
            };
        }
        
        private string GetSeverityIcon(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Error => "⚠",
                ValidationSeverity.Warning => "⚡",
                ValidationSeverity.Info => "ℹ",
                _ => "•"
            };
        }
    }
    
    [System.Serializable]
    public class ValidationResult
    {
        public ValidationSeverity severity;
        public ValidationCategory category;
        public string assetName;
        public string message;
        public string context;
        public string assetPath;
    }
    
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
    
    public enum ValidationCategory
    {
        All,
        Events,
        Choices,
        Outcomes,
        Resources,
        References
    }
}