namespace SkillSwap_Platform.Services.Newsletter
{
    /// <summary>
    /// A simple service for subscribing an email to the newsletter.
    /// </summary>
    public interface INewsletterService
    {
        Task SubscribeAsync(string email);
    }
}
