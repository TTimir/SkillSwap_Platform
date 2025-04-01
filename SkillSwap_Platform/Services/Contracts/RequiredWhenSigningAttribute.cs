using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;

namespace SkillSwap_Platform.Services.Contracts
{
    public class RequiredWhenSigningAttribute : ValidationAttribute
    {
        public string ConditionProperty { get; set; }
        public string ConditionValue { get; set; } = "Sign"; // Default condition

        public RequiredWhenSigningAttribute(string conditionProperty)
        {
            ConditionProperty = conditionProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            // Get the property that determines the condition.
            PropertyInfo conditionPropertyInfo = validationContext.ObjectType.GetProperty(ConditionProperty);
            if (conditionPropertyInfo == null)
            {
                return new ValidationResult($"Unknown property: {ConditionProperty}");
            }

            // Get the value of the condition property.
            var conditionValue = conditionPropertyInfo.GetValue(validationContext.ObjectInstance, null)?.ToString();

            // If the condition matches our specified value (e.g., "Sign"), enforce required validation.
            if (string.Equals(conditionValue, ConditionValue, StringComparison.OrdinalIgnoreCase))
            {
                if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required when signing.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
