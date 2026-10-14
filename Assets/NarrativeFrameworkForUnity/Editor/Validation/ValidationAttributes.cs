using UnityEditor;
using UnityEngine;

namespace SimulationGame.Validation
{
    /// <summary>
    /// Validates that a day range is within acceptable bounds
    /// </summary>
    public class ValidateDayRangeAttribute : PropertyAttribute
    {
        public int minDay = 1;
        public int maxDay = 365;
        public string errorMessage = "Day must be within valid range";
        
        public ValidateDayRangeAttribute(int minDay = 1, int maxDay = 365)
        {
            this.minDay = minDay;
            this.maxDay = maxDay;
            this.errorMessage = $"Day must be between {minDay} and {maxDay}";
        }
    }
    
    /// <summary>
    /// Validates that a string field is not null or empty
    /// </summary>
    public class ValidateNonEmptyAttribute : PropertyAttribute
    {
        public string errorMessage = "Field cannot be empty";
        
        public ValidateNonEmptyAttribute(string errorMessage = "Field cannot be empty")
        {
            this.errorMessage = errorMessage;
        }
    }
    
    /// <summary>
    /// Validates that an event reference is not null and exists in GameData
    /// </summary>
    public class ValidateEventReferenceAttribute : PropertyAttribute
    {
        public bool allowNull = false;
        public string errorMessage = "Event reference is invalid";
        
        public ValidateEventReferenceAttribute(bool allowNull = false)
        {
            this.allowNull = allowNull;
            this.errorMessage = allowNull ? "Event reference is invalid" : "Event reference cannot be null";
        }
    }
    
    /// <summary>
    /// Validates that a probability value is between 0 and 1
    /// </summary>
    public class ValidateProbabilityAttribute : PropertyAttribute
    {
        public string errorMessage = "Probability must be between 0.0 and 1.0";
        
        public ValidateProbabilityAttribute()
        {
        }
    }
    
    /// <summary>
    /// Validates that a list has at least a minimum number of elements
    /// </summary>
    public class ValidateMinimumListSizeAttribute : PropertyAttribute
    {
        public int minimumSize = 1;
        public string errorMessage = "List must have at least one element";
        
        public ValidateMinimumListSizeAttribute(int minimumSize = 1)
        {
            this.minimumSize = minimumSize;
            this.errorMessage = $"List must have at least {minimumSize} element{(minimumSize == 1 ? "" : "s")}";
        }
    }
    
    /// <summary>
    /// Validates that a choice has valid outcomes
    /// </summary>
    public class ValidateChoiceOutcomesAttribute : PropertyAttribute
    {
        public bool requireAtLeastOne = true;
        public string errorMessage = "Choice must have at least one outcome or next event";
        
        public ValidateChoiceOutcomesAttribute(bool requireAtLeastOne = true)
        {
            this.requireAtLeastOne = requireAtLeastOne;
        }
    }
    
    /// <summary>
    /// Validates that flags in a list follow proper naming conventions
    /// </summary>
    public class ValidateFlagNamingAttribute : PropertyAttribute
    {
        public bool allowEmpty = true;
        public string pattern = @"^[a-zA-Z][a-zA-Z0-9_]*$";
        public string errorMessage = "Flag names must start with a letter and contain only letters, numbers, and underscores";
        
        public ValidateFlagNamingAttribute(bool allowEmpty = true)
        {
            this.allowEmpty = allowEmpty;
        }
    }
    
    /// <summary>
    /// Validates that resource IDs exist in GameData
    /// </summary>
    public class ValidateResourceIdAttribute : PropertyAttribute
    {
        public bool allowEmpty = false;
        public string errorMessage = "Resource ID not found in GameData";
        
        public ValidateResourceIdAttribute(bool allowEmpty = false)
        {
            this.allowEmpty = allowEmpty;
        }
    }
    
    /// <summary>
    /// Shows a warning if a field value might cause performance issues
    /// </summary>
    public class PerformanceWarningAttribute : PropertyAttribute
    {
        public float warningThreshold;
        public string warningMessage;
        
        public PerformanceWarningAttribute(float warningThreshold, string warningMessage)
        {
            this.warningThreshold = warningThreshold;
            this.warningMessage = warningMessage;
        }
    }
    
    /// <summary>
    /// Shows a helpful hint below the property field
    /// </summary>
    public class HelpAttribute : PropertyAttribute
    {
        public string helpText;
        public MessageType messageType;
        
        public HelpAttribute(string helpText, MessageType messageType = MessageType.Info)
        {
            this.helpText = helpText;
            this.messageType = messageType;
        }
    }
    
    /// <summary>
    /// Message types for help and validation messages
    /// </summary>
}