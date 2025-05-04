using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag
{
    public interface IOfferFlagService
    {
        Task FlagOfferAsync(int offerId, int userId, string reason);
        Task<PagedResult<TblOfferFlag>> GetPendingFlagsAsync(int page, int pageSize);
        Task<PagedResult<TblOfferFlag>> GetProcessedFlagsAsync(int page, int pageSize);
        Task DismissFlagAsync(int flagId, int adminUserId, string reason);
        Task RemoveOfferAsync(int flagId, int adminUserId, string reason);

        /// <summary>
        /// Returns true if the given user has an un‐handled flag on the given offer.
        /// </summary>
        Task<bool> HasPendingFlagAsync(int offerId, int flaggedByUserId);
        Task<PagedResult<FlaggedOfferSummary>> GetFlaggedOfferSummariesAsync(int page, int pageSize);
        Task<PagedResult<TblOfferFlag>> GetFlagsForOfferAsync(int offerId, int page, int pageSize);

        Task<DashboardMetricsDto> GetDashboardMetricsAsync(
                DateTime periodStart, DateTime periodEnd,
                int mostFlaggedTake = 5,
                int recentActionsTake = 10);
    }
}
