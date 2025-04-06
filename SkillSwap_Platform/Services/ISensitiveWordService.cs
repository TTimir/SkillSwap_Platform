namespace SkillSwap_Platform.Services
{
    public interface ISensitiveWordService
    {
        Task<Dictionary<string, string>> CheckSensitiveWordsAsync(string input);
    }
}
