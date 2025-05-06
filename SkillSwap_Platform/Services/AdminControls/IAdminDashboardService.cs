using SkillSwap_Platform.Services.AdminControls.AdminSearch;

namespace SkillSwap_Platform.Services.AdminControls
{
    public interface IAdminDashboardService
    {
        Task<int> GetPendingCertificatesCountAsync();
        Task<int> GetOtpFailureCountAsync();
        Task<int> GetUsersWithFailedOtpCountAsync();
        Task<int> GetPendingEscrowCountAsync();
        Task<int> GetHeldUsersCountAsync();
        Task<int> GetFlaggedOffersCountAsync();

        // --- new methods ---
        Task<int> GetFlaggedReviewsCountAsync();
        Task<int> GetFlaggedReviewRepliesCountAsync();
        Task<int> GetFlaggedMessagesCountAsync();
        Task<int> GetHeldAccountsCountAsync();
        Task<int> GetActiveFlaggedUsersCountAsync();


        Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync(DateTime periodStart, DateTime periodEnd);
    }
}
