using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag
{
    public interface IOfferFlagService
    {
        Task FlagOfferAsync(int offerId, int userId, string reason);
        Task<IEnumerable<TblOfferFlag>> GetPendingFlagsAsync();
        Task DismissFlagAsync(int flagId);
        Task RemoveOfferAsync(int flagId);
    }
}
