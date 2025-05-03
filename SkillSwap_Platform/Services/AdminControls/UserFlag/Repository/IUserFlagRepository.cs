using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.UserFlag.Repository
{
    public interface IUserFlagRepository
    {
        Task AddAsync(TblUserFlag flag);
        Task<TblUserFlag?> GetByIdAsync(int flagId);
        Task<IEnumerable<TblUserFlag>> GetPendingAsync();
        Task<IEnumerable<TblUserFlag>> GetByUserAsync(int flaggedUserId);
        Task UpdateAsync(TblUserFlag flag);
    }
}
