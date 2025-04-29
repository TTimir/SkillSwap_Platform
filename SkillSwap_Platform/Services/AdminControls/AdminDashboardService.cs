using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(
            SkillSwapDbContext db,
            ILogger<AdminDashboardService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> GetPendingCertificatesCountAsync()
        {
            try
            {
                return await _db.TblUserCertificates
                                .CountAsync(c => c.ApprovedDate == null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending certificates count");
                return 0;
            }
        }


        public async Task<int> GetHeldUsersCountAsync()
        {
            try
            {
                return await _db.TblUsers
                                .CountAsync(u => u.IsHeld);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting held users");
                return 0;
            }
        }


        public async Task<int> GetOtpFailureCountAsync()
        {
            try
            {
                // assuming FailedOtpAttempts increments per failure
                // or you have a log table
                var totalFailures = await _db.TblUsers
                                     .Where(u => u.FailedOtpAttempts > 0)
                                     .SumAsync(u => (int?)u.FailedOtpAttempts);

                return totalFailures ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OTP failure count");
                return 0;
            }
        }

        public async Task<int> GetPendingEscrowCountAsync()
        {
            try
            {
                return await _db.TblTokenTransactions
                        .CountAsync(tx => !tx.IsReleased);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending escrow count");
                return 0;
            }
        }

        public async Task<int> GetFlaggedOffersCountAsync()
        {
            try
            {
                return await _db.TblOfferFlags
                       .Select(f => f.OfferId)
                       .Distinct()
                       .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching flagged offers count");
                return 0;
            }
        }

        // --- new implementations ---

        public async Task<int> GetFlaggedReviewsCountAsync()
        {
            try
            {
                return await _db.TblReviews
                                .CountAsync(r => r.IsFlagged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching flagged reviews count");
                return 0;
            }
        }

        public async Task<int> GetFlaggedReviewRepliesCountAsync()
        {
            try
            {
                return await _db.TblReviewReplies
                                .CountAsync(r => r.IsFlagged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching flagged review replies count");
                return 0;
            }
        }

        public async Task<int> GetFlaggedMessagesCountAsync()
        {
            try
            {
                return await _db.TblMessages
                                .CountAsync(m => m.IsFlagged && !m.IsApproved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching flagged messages count");
                return 0;
            }
        }

        public async Task<int> GetHeldAccountsCountAsync()
        {
            try
            {
                return await _db.TblUsers
                                .CountAsync(u => u.IsHeld);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching held accounts count");
                return 0;
            }
        }
    }
}
