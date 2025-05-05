using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.Faqs
{
    public interface IFaqService
    {
        Task<List<TblSkillSwapFaq>> GetBySectionAsync(string section);
        Task<TblSkillSwapFaq> GetByIdAsync(int id);
        Task<int> GetMaxSortOrderAsync(string section);
        Task AddAsync(TblSkillSwapFaq faq);
        Task UpdateAsync(TblSkillSwapFaq faq);
        Task DeleteAsync(int id);
    }
}
