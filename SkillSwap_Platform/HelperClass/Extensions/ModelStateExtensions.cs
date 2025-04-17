using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.HelperClass.Extensions
{
    public static class ModelStateExtensions
    {
        public static void RemoveProperties(this ModelStateDictionary modelState, params string[] keys)
        {
            foreach (var key in keys)
            {
                modelState.Remove(key);
            }
        }

        /// <summary>
        /// Validates that the attempted value for <paramref name="fieldName"/> is a well‑formed e‑mail address.
        /// </summary>
        public static bool IsValidEmail(
            this ModelStateDictionary ms,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));

            if (!ms.TryGetValue(fieldName, out var entry) || entry == null)
                return false;

            var email = entry.AttemptedValue;
            return
                !string.IsNullOrWhiteSpace(email) &&
                Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
    }
}
