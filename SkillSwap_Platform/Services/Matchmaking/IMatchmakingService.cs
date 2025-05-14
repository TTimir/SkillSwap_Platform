using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.OfferPublicVM;

namespace SkillSwap_Platform.Services.Matchmaking
{
    public interface IMatchmakingService
    {
        /// <summary>
        /// Returns all offers that mutually match the skills provided and desired
        /// by the given offer.
        /// </summary>
        Task<IReadOnlyList<OfferCardVM>> GetSuggestedOffersForUserAsync(int userId);
    }
}
