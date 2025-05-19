using Google;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using SkillSwap_Platform.Services.AdminControls.AdminSearch;

namespace SkillSwap_Platform.Services.AdminControls
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private const int OtpFailureWindowDays = 14;               // ← your global window
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
                    .Where(f => f.FlaggedDate >= _CutoffUtc
                                && f.AdminAction == null)      // only un‐handled flags
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
                // only reviews flagged in the last 7 days and not yet actioned by admin
                var reviewCount = await _db.TblReviews
                    .Where(r => r.IsFlagged
                                && r.FlaggedDate >= _CutoffUtc
                                && (r.DeletionReason == null || r.DeletedByAdminId == null))
                    .CountAsync();

                // only reply-flags flagged in the last 7 days and not yet actioned
                var replyCount = await _db.TblReviewReplies
                    .Where(rr => rr.IsFlagged
                                 && rr.FlaggedDate >= _CutoffUtc
                                 && (rr.DeletionReason == null || rr.DeletedByAdminId == null))
                    .CountAsync();

                return reviewCount + replyCount;
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
                            .AsNoTracking()
                            .Where(m =>
                                   m.IsFlagged
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

        public async Task<int> GetActiveFlaggedUsersCountAsync()
        {
            try
            {
                // only flags in the window
                var cutoff = _CutoffUtc;
                return await _db.TblUserFlags
                    .AsNoTracking()
                    .Where(f => f.FlaggedDate >= cutoff)
                    .GroupBy(f => f.FlaggedUserId)
                    // include this user only if their *latest* flag still has AdminAction == null
                    .Where(g => g.Max(x => x.FlaggedDate)
                               == g.Where(x => x.AdminAction == null)
                                   .Max(x => x.FlaggedDate))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active flagged-users count");
                return 0;
            }
        }

        public async Task<int> GetPendingVerificationRequestsCountAsync()
        {
            try
            {
                // Status == 0 means “Pending”
                return await _db.VerificationRequests
                                .AsNoTracking()
                                .CountAsync(r => r.Status == (int)VerificationStatus.Pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending verification requests count");
                return 0;
            }
        }

        public async Task<int> GetPendingAdminNotificationsCountAsync()
        {
            try
            {
                // any notification still without a SentAtUtc is “pending” (i.e. failed or not yet retried)
                return await _db.AdminNotifications
                                .CountAsync(n => n.SentAtUtc == null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending admin notifications count");
                return 0;
            }
        }

        #region Admin Overview Dashboard

        public async Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync(DateTime start, DateTime end)
        {
            try
            {
                var user = await GetUserMetricsAsync(start, end);
                var offer = await GetOfferMetricsAsync(start, end);
                var escrow = await GetEscrowMetricsAsync(start, end);
                var mod = await GetModerationMetricsAsync(start, end);
                var swap = await GetSwapMetricsAsync(start, end);
                var revenue = await GetRevenueMetricsAsync(start, end);
                var topViewed = await GetMostViewedServicesAsync(5);
                var recentBuys = await GetRecentPurchasesAsync(5);
                var recentActs = await GetRecentActivitiesAsync(start, end, 10);

                return new AdminDashboardMetricsDto
                {
                    // User Metrics
                    TotalUsers = user.TotalUsers,
                    NewUsersToday = user.NewUsersToday,
                    NewUsersThisWeek = user.NewUsersThisWeek,
                    NewUsersThisMonth = user.NewUsersThisMonth,
                    ActiveUsersLast7Days = user.ActiveUsersLast7Days,
                    HeldAccounts = user.HeldAccounts,

                    // Offer Metrics
                    TotalOffers = offer.TotalOffers,
                    NewOffersToday = offer.NewOffersToday,
                    NewOffersThisWeek = offer.NewOffersThisWeek,
                    CompletedSwaps = offer.CompletedSwaps,
                    CanceledSwaps = offer.CanceledSwaps,
                    PendingOffers = offer.PendingOffers,

                    // Escrow Metrics
                    ActiveEscrows = escrow.ActiveEscrows,
                    ReleasedEscrows = escrow.ReleasedEscrows,
                    RefundedEscrows = escrow.RefundedEscrows,
                    DisputedEscrows = escrow.DisputedEscrows,
                    TotalTokensHeld = escrow.TotalTokensHeld,
                    TotalTokensReleased = escrow.TotalTokensReleased,
                    AverageSettlementHours = escrow.AverageSettlementHours,

                    // Moderation Summary
                    PendingOfferFlags = mod.PendingOfferFlags,
                    PendingUserFlags = mod.PendingUserFlags,
                    ResolvedOfferFlags = mod.ResolvedOfferFlags,
                    ResolvedUserFlags = mod.ResolvedUserFlags,
                    FlagTrends = mod.FlagTrends,

                    // Swap Activity
                    TotalSwapsExecuted = swap.TotalSwapsExecuted,
                    SwapSuccessRate = swap.SwapSuccessRate,
                    AverageSwapValue = swap.AverageSwapValue,
                    ResolutionBreakdown = mod.ResolutionBreakdown,

                    // Revenue & Fees
                    FeesCollected = revenue.FeesCollected,
                    PayoutsToUsers = revenue.PayoutsToUsers,
                    MonthlyRecurringRevenue = revenue.MonthlyRecurringRevenue,
                    TotalTokensInCirculation = await _db.TblUsers.SumAsync(u => u.DigitalTokenBalance),

                    MostViewedServices = topViewed,
                    RecentPurchasedServices = recentBuys,
                    RecentActivities = recentActs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Admin Dashboard metrics");
                throw;
            }
        }

        // --- USER ---
        private async Task<UserMetrics> GetUserMetricsAsync(DateTime start, DateTime end)
        {
            var total = await _db.TblUsers.CountAsync();
            var today = DateTime.UtcNow.Date;
            var newToday = await _db.TblUsers.CountAsync(u => u.CreatedDate >= today);
            var newWeek = await _db.TblUsers.CountAsync(u => u.CreatedDate >= today.AddDays(-7));
            var newMonth = await _db.TblUsers.CountAsync(u => u.CreatedDate >= today.AddMonths(-1));
            var active7d = await _db.TblUsers.CountAsync(u => u.LastActive >= today.AddDays(-7));
            var held = await _db.TblUsers.CountAsync(u => u.IsHeld);
            return new UserMetrics(total, newToday, newWeek, newMonth, active7d, held);
        }

        // --- OFFER ---
        private async Task<OfferMetrics> GetOfferMetricsAsync(DateTime start, DateTime end)
        {
            var total = await _db.TblOffers.CountAsync(o => !o.IsDeleted);
            var today = DateTime.UtcNow.Date;
            var newToday = await _db.TblOffers.CountAsync(o => o.CreatedDate >= today && !o.IsDeleted);
            var newWeek = await _db.TblOffers.CountAsync(o => o.CreatedDate >= today.AddDays(-7) && !o.IsDeleted);
            var completed = await _db.TblExchanges.CountAsync(x => x.IsSuccessful);
            var canceled = await _db.TblExchanges.CountAsync(x => x.Status == "Canceled");
            var pending = await _db.TblOffers
                               .Where(o => !o.IsDeleted)
                               .GroupJoin(
                                   _db.TblExchanges.Where(x => x.Status == "Accepted"),
                                   o => o.OfferId,
                                   x => x.OfferId,
                                   (o, exs) => exs
                               )
                               .CountAsync(exs => !exs.Any());
            return new OfferMetrics(total, newToday, newWeek, completed, canceled, pending);
        }

        // --- ESCROW ---
        private async Task<EscrowMetrics> GetEscrowMetricsAsync(DateTime start, DateTime end)
        {
            var active = await _db.TblEscrows.CountAsync(e => e.ReleasedAt == null && e.RefundedAt == null && e.DisputedAt == null);
            var released = await _db.TblEscrows.CountAsync(e => e.ReleasedAt != null);
            var refunded = await _db.TblEscrows.CountAsync(e => e.RefundedAt != null);
            var disputed = await _db.TblEscrows.CountAsync(e => e.DisputedAt != null);
            var heldAmt = await _db.TblEscrows.Where(e => e.ReleasedAt == null).SumAsync(e => e.Amount);
            var relAmt = await _db.TblEscrows.Where(e => e.ReleasedAt != null).SumAsync(e => e.Amount);
            // only consider escrows that have actually been released
            var releasedEscrows = _db.TblEscrows
                .Where(e => e.ReleasedAt != null);

            // 1) total up the minute differences (coalesced to 0 if anything odd happens)
            var totalMinutes = await releasedEscrows
                .SumAsync(e =>
                    // DateDiffMinute returns int?
                    EF.Functions.DateDiffMinute(e.CreatedAt, e.ReleasedAt.Value)
                );

            // 2) count how many were released
            var count = await releasedEscrows.CountAsync();

            // 3) compute your average hours
            double avgHours = count > 0
                ? (totalMinutes / (double)count) / 60.0
                : 0.0;
            return new EscrowMetrics(active, released, refunded, disputed, heldAmt, relAmt, avgHours);
        }

        // --- MODERATION ---
        private async Task<ModerationMetrics> GetModerationMetricsAsync(DateTime start, DateTime end)
        {
            var offerDates = await _db.TblOfferFlags
                .Where(f => f.FlaggedDate >= start && f.FlaggedDate <= end)
                .Select(f => f.FlaggedDate.Date)
                .ToListAsync();
            var userDates = await _db.TblUserFlags
                .Where(f => f.FlaggedDate >= start && f.FlaggedDate <= end)
                .Select(f => f.FlaggedDate.Date)
                .ToListAsync();

            var trends = offerDates.Concat(userDates)
                .GroupBy(d => d)
                .Select(g => new DateCount(g.Key, g.Count()))
                .OrderBy(dc => dc.Date)
                .ToList();

            var pOffer = await _db.TblOfferFlags
                .Where(f => f.AdminAction == null)
                .Select(f => f.OfferId)
                .Distinct()
                .CountAsync();
            var pUser = await _db.TblUserFlags
                .Where(f => f.AdminAction == null)
                .Select(f => f.FlaggedUserId)
                .Distinct()
                .CountAsync();
            var rOffer = await _db.TblOfferFlags.CountAsync(f => f.AdminAction != null);
            var rUser = await _db.TblUserFlags.CountAsync(f => f.AdminAction != null);

            var offerActions = _db.TblOfferFlags
                .Where(f => f.AdminAction != null)
                .Select(f => f.AdminAction!);
            var userActions = _db.TblUserFlags
                .Where(f => f.AdminAction != null)
                .Select(f => f.AdminAction!);
            var resolutionList = await offerActions
                .Concat(userActions)
                .GroupBy(a => a)
                .Select(g => new ResolutionCount(g.Key, g.Count()))
                .ToListAsync();

            return new ModerationMetrics(
                PendingOfferFlags: pOffer,
                PendingUserFlags: pUser,
                ResolvedOfferFlags: rOffer,
                ResolvedUserFlags: rUser,
                FlagTrends: trends,
                ResolutionBreakdown: resolutionList
            );
        }

        // --- SWAP ---
        private async Task<SwapMetrics> GetSwapMetricsAsync(DateTime start, DateTime end)
        {
            var totalExec = await _db.TblExchanges.CountAsync(x => x.IsSuccessful);
            var totalInit = await _db.TblExchanges.CountAsync();
            var rate = totalInit > 0 ? (double)totalExec / totalInit * 100 : 0;
            var avgValue = await _db.TblExchanges.Where(x => x.IsSuccessful).AverageAsync(x => x.TokensPaid);
            return new SwapMetrics(totalExec, rate, avgValue);
        }

        // --- REVENUE ---
        private async Task<RevenueMetrics> GetRevenueMetricsAsync(DateTime start, DateTime end)
        {
            // 1) Compute the first day of *this* month once, in C#
            var firstOfThisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            // 2) Sum “Hold” transactions (your fees collected)
            decimal fees = await _db.TblTokenTransactions
                .Where(tx => tx.TxType == "Hold")
                .SumAsync(tx => tx.Amount);

            // 3) Sum “Release” transactions **since the first of the month** (your MRR)
            decimal mrr = await _db.TblTokenTransactions
                .Where(tx => tx.TxType == "Release" && tx.CreatedAt >= firstOfThisMonth)
                .SumAsync(tx => tx.Amount);

            // 4) Sum “Refund” transactions if you need that too
            decimal refunds = await _db.TblTokenTransactions
                .Where(tx => tx.TxType == "Refund")
                .SumAsync(tx => tx.Amount);

            return new RevenueMetrics(
                FeesCollected: fees,
                PayoutsToUsers: mrr,
                MonthlyRecurringRevenue: refunds
            );
        }

        // 1) Top‐viewed offers (assumes TblOffers.ViewCount & TblOffers.ImageUrl exist)
        private async Task<List<ViewedServiceDto>> GetMostViewedServicesAsync(int take)
        {
            var raw = await _db.TblOffers
    .Where(o => !o.IsDeleted)
    .OrderByDescending(o => o.Views)
    .Take(take)
    .Select(o => new {
        o.OfferId,
        o.Title,
        o.Portfolio,
        o.Views
    })
    .ToListAsync();

            // define your fallback images
            var defaultImages = new[]{
    "/template_assets/images/listings/No_Offer_img_1.jpg",
    "/template_assets/images/listings/No_Offer_img_2.jpg"
};

            return raw.Select(o => {
                // try to parse the JSON portfolio field
                List<string> urls = new();
                if (!string.IsNullOrWhiteSpace(o.Portfolio))
                {
                    try
                    {
                        urls = JsonConvert.DeserializeObject<List<string>>(o.Portfolio)
                               ?? new List<string>();
                    }
                    catch { /* ignore parse errors */ }
                }

                // pick either the first real URL or a deterministic fallback
                var img = urls.Any()
                    ? urls.First()
                    : defaultImages[o.OfferId % defaultImages.Length];

                return new ViewedServiceDto
                {
                    OfferId = o.OfferId,
                    Title = o.Title,
                    ImageUrl = img,
                    ViewCount = o.Views
                };
            })
            .ToList();
        }

        // 2) Recent purchases (successful exchanges)
        private async Task<List<RecentPurchaseDto>> GetRecentPurchasesAsync(int take)
        {
            return await _db.TblExchanges
                .Where(x => x.IsSuccessful)
                .OrderByDescending(x => x.ExchangeDate)
                .Take(take)
                .Join(
                    _db.TblUsers,
                    ex => ex.OtherUserId,
                    u => u.UserId,
                    (ex, u) => new { ex, Buyer = u }
                )
                .Select(t => new RecentPurchaseDto
                {
                    ExchangeId = t.ex.ExchangeId,
                    OfferTitle = t.ex.Offer!.Title,
                    BuyerName = t.Buyer.UserName,
                    Date = t.ex.ExchangeDate,
                    Amount = t.ex.TokensPaid
                })
                .ToListAsync();
        }

        // 3) Recent activity feed (mix of flags, escrows, etc.)
        private async Task<List<RecentActivityItem>> GetRecentActivitiesAsync(DateTime start, DateTime end, int take)
        {
            // 1) Recent offer‐flag events
            var offerFlags = await _db.TblOfferFlags
                .Where(f => f.FlaggedDate >= start && f.FlaggedDate <= end)
                .OrderByDescending(f => f.FlaggedDate)
                .Take(take)
                .Select(f => new RecentActivityItem(
                    $"Offer “{f.Offer!.Title}” flagged",
                    f.FlaggedDate
                ))
                .ToListAsync();

            // 2) Recent user‐flag events
            var userFlags = await _db.TblUserFlags
                .Where(f => f.FlaggedDate >= start && f.FlaggedDate <= end)
                .OrderByDescending(f => f.FlaggedDate)
                .Take(take)
                .Select(f => new RecentActivityItem(
                    $"User “{f.FlaggedUser!.UserName}” flagged",
                    f.FlaggedDate
                ))
                .ToListAsync();

            // 3) Recent successful swaps
            var purchases = await _db.TblExchanges
                .Include(x => x.Offer)   // ensure Offer.Title is available
                .Where(x => x.IsSuccessful
                            && x.ExchangeDate >= start
                            && x.ExchangeDate <= end)
                .OrderByDescending(x => x.ExchangeDate)
                .Take(take)
                .Select(x => new RecentActivityItem(
                    $"Swap complete: {x.Offer!.Title}",
                    x.ExchangeDate
                ))
                .ToListAsync();

            // 4) Merge all three lists, re‐sort by timestamp, then take the top N
            return offerFlags
                .Concat(userFlags)
                .Concat(purchases)
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .ToList();
        }
    }

    // Internal DTOs
    internal record UserMetrics(int TotalUsers, int NewUsersToday, int NewUsersThisWeek, int NewUsersThisMonth, int ActiveUsersLast7Days, int HeldAccounts);
    internal record OfferMetrics(int TotalOffers, int NewOffersToday, int NewOffersThisWeek, int CompletedSwaps, int CanceledSwaps, int PendingOffers);
    internal record EscrowMetrics(int ActiveEscrows, int ReleasedEscrows, int RefundedEscrows, int DisputedEscrows, decimal TotalTokensHeld, decimal TotalTokensReleased, double AverageSettlementHours);
    internal record ModerationMetrics(
        int PendingOfferFlags,
        int PendingUserFlags,
        int ResolvedOfferFlags,
        int ResolvedUserFlags,
        List<DateCount> FlagTrends,
        List<ResolutionCount> ResolutionBreakdown
    );
    internal record SwapMetrics(int TotalSwapsExecuted, double SwapSuccessRate, decimal AverageSwapValue);
    internal record RevenueMetrics(decimal FeesCollected, decimal PayoutsToUsers, decimal MonthlyRecurringRevenue);
    #endregion

}
