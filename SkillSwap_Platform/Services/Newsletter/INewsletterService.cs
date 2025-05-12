namespace SkillSwap_Platform.Services.Newsletter
{
    /// <summary>
    /// A simple service for subscribing an email to the newsletter.
    /// </summary>
    public interface INewsletterService
    {
        Task SubscribeAsync(string email, CancellationToken ct = default);
        Task SendNewsletterAsync(string subject, string htmlContent, IEnumerable<IFormFile>? attachments = null, CancellationToken ct = default);
        Task SendToUserAsync(int userId, string subject, string htmlContent, IEnumerable<IFormFile>? attachments = null, CancellationToken ct = default);
    }
}
