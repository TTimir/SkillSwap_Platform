using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository
{
    public interface IOfferRepository
    {
        Task<TblOffer> GetByIdAsync(int id);
        Task UpdateAsync(TblOffer offer);
    }
}
