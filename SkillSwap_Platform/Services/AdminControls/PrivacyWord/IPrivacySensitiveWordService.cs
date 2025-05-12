namespace SkillSwap_Platform.Services.AdminControls.PrivacyWord
{
    public interface IPrivacySensitiveWordService
    {
        Task<PagedResult<PrivacySensitiveWordVm>> GetPagedAsync(int page, int pageSize);
        Task<PrivacySensitiveWordVm> GetByIdAsync(int id);
        Task CreateAsync(PrivacySensitiveWordVm vm);
        Task UpdateAsync(PrivacySensitiveWordVm vm);
        Task DeleteAsync(int id);
    }
}
