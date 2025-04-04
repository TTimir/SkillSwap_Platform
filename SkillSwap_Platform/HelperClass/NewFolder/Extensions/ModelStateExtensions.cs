using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SkillSwap_Platform.HelperClass.NewFolder.Extensions
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
    }
}
