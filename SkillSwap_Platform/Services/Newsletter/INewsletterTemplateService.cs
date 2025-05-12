using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Newsletter
{
    public interface INewsletterTemplateService
    {
        Task<IList<NewsletterTemplate>> GetAllAsync(CancellationToken ct = default);
        Task<NewsletterTemplate> CreateAsync(
            string name,
            string htmlContent,
            string createdByAdmin,
            CancellationToken ct = default
        );
    }
}
