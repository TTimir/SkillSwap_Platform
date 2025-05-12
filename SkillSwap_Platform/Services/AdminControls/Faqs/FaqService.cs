using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.Faqs
{
    public class FaqService : IFaqService
    {
        private readonly SkillSwapDbContext _db;
        public FaqService(SkillSwapDbContext db) => _db = db;

        public Task<List<TblSkillSwapFaq>> GetBySectionAsync(string section) =>
            _db.TblSkillSwapFaqs
               .Where(f => f.Section == section && f.IsActive && !f.IsDeleted)
               .OrderBy(f => f.SortOrder)
               .ToListAsync();

        public Task<TblSkillSwapFaq> GetByIdAsync(int id) =>
            _db.TblSkillSwapFaqs
               .Where(f => f.FaqId == id && !f.IsDeleted)
               .FirstOrDefaultAsync();

        public async Task<int> GetMaxSortOrderAsync(string section)
        {
            var max = await _db.TblSkillSwapFaqs
                               .Where(f => f.Section == section && !f.IsDeleted)
                               .MaxAsync(f => (int?)f.SortOrder);
            return max ?? 0;
        }

        public async Task AddAsync(TblSkillSwapFaq faq)
        {
            // stamp on your dates
            faq.CreatedDate = DateTime.UtcNow;
            faq.UpdatedDate = null;
            faq.IsDeleted = false;

            // if no sort order given, push to the end
            if (faq.SortOrder <= 0)
                faq.SortOrder = await GetMaxSortOrderAsync(faq.Section) + 1;

            _db.TblSkillSwapFaqs.Add(faq);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(TblSkillSwapFaq faq)
        {
            var existing = await GetByIdAsync(faq.FaqId);
            if (existing is null) return;

            existing.Section = faq.Section;
            existing.Question = faq.Question;
            existing.Answer = faq.Answer;
            existing.SortOrder = faq.SortOrder;
            existing.IsActive = faq.IsActive;
            existing.UpdatedDate = DateTime.UtcNow;

            _db.TblSkillSwapFaqs.Update(existing);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var f = await GetByIdAsync(id);
            if (f != null)
            {
                // soft‐delete
                f.IsDeleted = true;
                f.IsActive = false;
                await _db.SaveChangesAsync();
            }
        }
    }
}
