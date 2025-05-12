using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository
{
    public interface IOfferFlagRepository
    {
        Task AddAsync(TblOfferFlag flag);
        Task<TblOfferFlag> GetByIdAsync(int flagId);
        Task<IEnumerable<TblOfferFlag>> GetPendingFlagsAsync();
        Task DeleteAsync(TblOfferFlag flag);
        Task<IEnumerable<TblOfferFlag>> GetByOfferIdAsync(int offerId);
        Task UpdateAsync(TblOfferFlag flag);

    }
}
