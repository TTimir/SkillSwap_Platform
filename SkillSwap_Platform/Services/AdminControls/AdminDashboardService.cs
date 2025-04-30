using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private const int OtpFailureWindowDays = 7;               // ← your global window
        private readonly DateTime _CutoffUtc;                  // ← computed once
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(
            SkillSwapDbContext db,
            ILogger<AdminDashboardService> logger)
        {
            _db = db;
            _logger = logger;
            _CutoffUtc = DateTime.UtcNow.AddDays(-OtpFailureWindowDays);
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
                        .Where(u => u.IsHeld
                                 && u.HeldAt >= _CutoffUtc)
                        .CountAsync();
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
                var totalFailures = await _db.OtpAttempts
                        .Where(a => !a.WasSuccessful && a.AttemptedAt >= _CutoffUtc)
                        .Select(a => a.UserId)
                        .Distinct()
                        .CountAsync();

                return totalFailures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OTP failure count");
                return 0;
            }
        }

        public async Task<int> GetUsersWithFailedOtpCountAsync()
        {
            return await _db.OtpAttempts
                            .Where(a => !a.WasSuccessful && a.AttemptedAt >= _CutoffUtc)
                            .Select(a => a.UserId)
                            .Distinct()
                            .CountAsync();
        }

        public async Task<int> GetPendingEscrowCountAsync()
        {
            try
            {
                return await _db.TblTokenTransactions
                            .Where(tx => !tx.IsReleased
                                      && tx.CreatedAt >= _CutoffUtc)
                            .CountAsync();
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
                                .Where(f => f.FlaggedDate >= _CutoffUtc)
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

        public async Task<int> GetFlaggedReviewsCountAsync()
        {
            try
            {
                return await _db.TblReviews
                                .Where(r => r.IsFlagged
                                         && r.FlaggedDate >= _CutoffUtc)
                                .CountAsync();
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
                                .Where(rr => rr.IsFlagged
                                           && rr.FlaggedDate >= _CutoffUtc)
                                .CountAsync();
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
                                .Where(m => m.IsFlagged
                                         && !m.IsApproved
                                         && m.SentDate >= _CutoffUtc)
                                .CountAsync();
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
                                .Where(u => u.IsHeld
                                         && u.HeldAt >= _CutoffUtc)
                                .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching held accounts count");
                return 0;
            }
        }
    }
}
