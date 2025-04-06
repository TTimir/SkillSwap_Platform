namespace SkillSwap_Platform.HelperClass
{
    public class UniqueIdGenerator
    {
        public static string GenerateSixCharHexToken()
        {
            // Generate a new GUID and convert it to a 32-character hexadecimal string.
            string fullGuid = Guid.NewGuid().ToString("N");
            // Extract the last 6 characters.
            return fullGuid.Substring(fullGuid.Length - 6, 6);
        }
    }
}
