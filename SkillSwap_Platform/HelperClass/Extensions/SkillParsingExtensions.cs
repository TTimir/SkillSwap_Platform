namespace SkillSwap_Platform.HelperClass.Extensions
{
    public static class SkillParsingExtensions
    {
        public static IReadOnlyList<string> ToSkillList(this string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return Array.Empty<string>();

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
