using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.PlatformMetrics
{
    public class PerformanceService : IPerformanceService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<PerformanceService> _logger;

        public PerformanceService(SkillSwapDbContext db, ILogger<PerformanceService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PlatformMetricsDto> GetCurrentMetricsAsync()
        {
            var dto = new PlatformMetricsDto();

            try
            {
                // ── Reference dates ───────────────────────────────────────────────────
                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);
                var weekAgo = today.AddDays(-7);
                var monthAgo = today.AddDays(-30);

                // ── 1) Growth ─────────────────────────────────────────────────────────
                dto.TotalUsers = await _db.TblUsers.CountAsync(u => !u.IsHeld);
                dto.NewUsersToday = await _db.TblUsers.CountAsync(u => u.CreatedDate >= today);
                dto.NewUsersThisWeek = await _db.TblUsers.CountAsync(u => u.CreatedDate >= weekAgo);

                // ── 2) Engagement ────────────────────────────────────────────────────
                dto.DAU = await _db.TblUsers.CountAsync(u => u.LastActive >= today);
                dto.WAU = await _db.TblUsers.CountAsync(u => u.LastActive >= weekAgo);
                dto.MAU = await _db.TblUsers.CountAsync(u => u.LastActive >= monthAgo);

                // Optional: stickiness ratio = DAU/MAU*100
                dto.Stickiness = dto.MAU > 0
                    ? Math.Round(dto.DAU / (double)dto.MAU * 100, 2)
                    : 0;

                // ── 3) Retention ──────────────────────────────────────────────────────
                var sign1Count = await _db.TblUsers
                                      .CountAsync(u => u.CreatedDate >= yesterday
                                                     && u.CreatedDate < today);

                var ret1Count = await _db.TblUsers
                                      .CountAsync(u => u.CreatedDate >= yesterday
                                                     && u.CreatedDate < today
                                                     && u.LastActive >= today);

                dto.Day1Retention = sign1Count > 0
                    ? Math.Round(ret1Count / (double)sign1Count * 100, 2)
                    : 0;

                var sign7Count = await _db.TblUsers
                                      .CountAsync(u => u.CreatedDate >= weekAgo
                                                     && u.CreatedDate < weekAgo.AddDays(1));

                var ret7Count = await _db.TblUsers
                                      .CountAsync(u => u.CreatedDate >= weekAgo
                                                     && u.CreatedDate < weekAgo.AddDays(1)
                                                     && u.LastActive >= weekAgo);

                dto.Day7Retention = sign7Count > 0
                    ? Math.Round(ret7Count / (double)sign7Count * 100, 2)
                    : 0;

                // ── 4) Monetization ──────────────────────────────────────────────────
                dto.TotalRevenueMonth = await _db.TblTokenTransactions
                                            .Where(tx => tx.CreatedAt >= monthAgo
                                                      && tx.TxType == "Payment")
                                            .SumAsync(tx => (decimal?)tx.Amount)
                                          ?? 0m;

                dto.ARPU = dto.MAU > 0
                    ? Math.Round(dto.TotalRevenueMonth / dto.MAU, 2)
                    : 0m;

                // ── 5) Swap & Offer Feature Usage ────────────────────────────────────
                var totalSwapsSuccessful = await _db.TblExchanges
                                                 .CountAsync(x => x.IsSuccessful);

                dto.SwapsPerMAU = dto.MAU > 0
                    ? Math.Round(totalSwapsSuccessful / (double)dto.MAU, 2)
                    : 0;

                var offersToday = await _db.TblOffers
                                        .CountAsync(o => o.CreatedDate >= today);

                dto.OffersPer1kDAU = dto.DAU > 0
                    ? Math.Round(offersToday / (double)dto.DAU * 1000, 2)
                    : 0;

                // ── 6) Churn & LTV ────────────────────────────────────────────────────
                var mauLastMonth = await _db.TblUsers
                                      .CountAsync(u => u.LastActive >= monthAgo
                                                     && u.LastActive < today);

                var returnedThisMonth = await _db.TblUsers
                                           .CountAsync(u => u.LastActive >= monthAgo
                                                          && u.LastActive < DateTime.UtcNow);

                dto.ChurnRate = mauLastMonth > 0
                    ? Math.Round((mauLastMonth - returnedThisMonth) / (double)mauLastMonth * 100, 2)
                    : 0;

                var totalPayments = await _db.TblTokenTransactions
                                         .Where(tx => tx.TxType == "Payment")
                                         .SumAsync(tx => (decimal?)tx.Amount)
                                     ?? 0m;

                dto.LTV = dto.TotalUsers > 0
                    ? Math.Round(totalPayments / dto.TotalUsers, 2)
                    : 0m;

                // ── 7) System Health Stubs (wire your APM/logging) ──────────────────
                dto.ErrorRate = 0;
                dto.P95LatencyMs = 0;

                // ── 8) Tokens & Exchanges ────────────────────────────

                // a) Token transactions  
                dto.TotalTokenTransactions = await _db.TblTokenTransactions.CountAsync();
                dto.TotalTokensProcessed = await _db.TblTokenTransactions
                                                  .SumAsync(tx => (decimal?)tx.Amount) ?? 0m;
                dto.AvgTokensPerTransaction = dto.TotalTokenTransactions > 0
                                             ? Math.Round(dto.TotalTokensProcessed / dto.TotalTokenTransactions, 2)
                                             : 0m;

                // b) Exchanges
                dto.TotalExchanges = await _db.TblExchanges.CountAsync();
                dto.TotalSuccessfulExchanges = await _db.TblExchanges
                                                       .CountAsync(x => x.IsSuccessful);
                dto.TotalCancelledExchanges = await _db.TblExchanges
                                                       .CountAsync(x => x.Status == "Canceled");

                dto.InPersonExchanges = await _db.TblExchanges
                                                .CountAsync(x => x.ExchangeMode == "In-Person");
                dto.DigitalExchanges = await _db.TblExchanges
                                               .CountAsync(x => x.ExchangeMode == "Online");

                dto.CancelRate = dto.TotalExchanges > 0
                    ? Math.Round(dto.TotalCancelledExchanges / (double)dto.TotalExchanges * 100, 2)
                    : 0;    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing platform metrics");
            }

            return dto;
        }
    }
}