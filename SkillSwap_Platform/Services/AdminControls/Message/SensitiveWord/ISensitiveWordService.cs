namespace SkillSwap_Platform.Services.AdminControls.Message.SensitiveWord
{
    public interface ISensitiveWordService
    {
        Task<PagedResult<SensitiveWordVm>> GetPagedAsync(int page, int pageSize);
        Task<IReadOnlyList<SensitiveWordVm>> GetAllAsync();
        Task<SensitiveWordVm> GetByIdAsync(int id);
        Task CreateAsync(SensitiveWordVm vm);
        Task UpdateAsync(SensitiveWordVm vm);
        Task DeleteAsync(int id);
    }
}
