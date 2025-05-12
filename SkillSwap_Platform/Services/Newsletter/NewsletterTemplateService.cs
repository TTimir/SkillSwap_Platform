using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Newsletter
{
    public class NewsletterTemplateService : INewsletterTemplateService
    {
        private readonly SkillSwapDbContext _db;
        public NewsletterTemplateService(SkillSwapDbContext db) => _db = db;

        public async Task<IList<Models.ViewModels.NewsletterTemplate>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.NewsletterTemplates
                            .AsNoTracking()
                            .OrderByDescending(t => t.CreatedAtUtc)
                            .Select(t => new Models.ViewModels.NewsletterTemplate
                            {
                                TemplateId = t.TemplateId,
                                Name = t.Name,
                                HtmlContent = t.HtmlContent,
                                CreatedBy = t.CreatedBy,
                                CreatedAtUtc = t.CreatedAtUtc
                            })
                            .ToListAsync(ct);
        }

        public async Task<Models.ViewModels.NewsletterTemplate> CreateAsync(
            string name,
            string htmlContent,
            string createdByAdmin,
            CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var entity = new Models.NewsletterTemplate
            {
                Name = name,
                HtmlContent = htmlContent,
                CreatedBy = createdByAdmin,
                CreatedAtUtc = now
            };

            _db.NewsletterTemplates.Add(entity);
            await _db.SaveChangesAsync(ct);

            // project back into the VM
            return new Models.ViewModels.NewsletterTemplate
            {
                TemplateId = entity.TemplateId,
                Name = entity.Name,
                HtmlContent = entity.HtmlContent,
                CreatedBy = entity.CreatedBy,
                CreatedAtUtc = entity.CreatedAtUtc
            };
        }
    }
}