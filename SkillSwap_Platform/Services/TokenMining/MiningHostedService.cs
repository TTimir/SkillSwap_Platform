using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.TokenMining
{
    public class MiningHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<MiningHostedService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

        public MiningHostedService(IServiceProvider services,
                                   ILogger<MiningHostedService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // wait until the whole app is up
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.WhenAll(
                    IssueTokensForActiveUsers(stoppingToken),
                    ResetDailyCapsIfNeeded(stoppingToken)
                );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Exception in mining loop");
                }
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task IssueTokensForActiveUsers(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
            var nowUtc = DateTime.UtcNow;

            // load settings (assume only one row)
            var settings = await db.TokenEmissionSettings.FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Emission settings not configured.");

            // if mining is paused, skip entirely
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("⏸️ Mining is currently paused (IsEnabled = false)");
                return;
            }

            // compute base rate R = P / (U×E×D)
            // but here we derive R dynamically per-minute:
            // R0 = TotalPool / (ExpectedUserMinutes)
            // For simplicity, assume ExpectedUserMinutes = 2000 users × 20 min/day × DripDays
            // You can replace with real analytics later.

            decimal expectedUserMinutes = 2000m * 20m * settings.DripDays;
            decimal baseRatePerMin = settings.TotalPool / expectedUserMinutes;

            // figure out how many "halvings" have occurred
            var daysSinceStart = (nowUtc - settings.StartDateUtc).TotalDays;
            int halvings = (int)(daysSinceStart / settings.HalvingPeriodDays);
            decimal rate = baseRatePerMin / (decimal)Math.Pow(2, halvings);

            _logger.LogDebug("Computed rate={Rate:N6} TK/min after {Halvings} halvings",
                             rate, halvings);

            // only users who haven’t been credited in the last 5 minutes
            var cutoff = nowUtc.AddMinutes(-5);
            var usersToProcess = await db.UserMiningProgresses
                .Where(p => p.IsMiningAllowed && p.LastEmittedUtc <= cutoff)
                .ToListAsync(ct);

            _logger.LogDebug("Found {Count} users to process", usersToProcess.Count);

            foreach (var prog in usersToProcess)
            {
                try
                {
                    if (!prog.IsMiningAllowed) continue;

                    // how many minutes since last emit?
                    var minsElapsed = (decimal)((nowUtc - prog.LastEmittedUtc).TotalMinutes);
                    if (minsElapsed < 5) continue;

                    // total tokens to credit = minsElapsed × rate
                    decimal toCredit = Math.Floor(minsElapsed) * rate;

                    // enforce daily cap
                    var availableCap = settings.DailyCap - prog.EmittedToday;
                    if (availableCap <= 0) continue;
                    if (toCredit > availableCap)
                        toCredit = availableCap;

                    if (toCredit <= 0) continue;

                    // 1) update user balance
                    var user = await db.TblUsers.FindAsync(prog.UserId);
                    if (user == null)
                    {
                        _logger.LogWarning("User {UserId} not found, skipping", prog.UserId);
                        continue;
                    }
                    user.DigitalTokenBalance += toCredit;

                    // 2) update progress
                    prog.LastEmittedUtc = nowUtc;
                    prog.EmittedToday += toCredit;

                    // 3) log it
                    db.MiningLogs.Add(new MiningLog
                    {
                        UserId = prog.UserId,
                        EmittedUtc = nowUtc,
                        Amount = toCredit
                    });

                    // 4) ALSO record in the normal token-transactions table
                    db.TblTokenTransactions.Add(new TblTokenTransaction
                    {
                        FromUserId = null,                  // mining is “created” by system
                        ToUserId = prog.UserId,
                        Amount = toCredit,
                        TxType = "Time-Based Mining Reward",
                        Description = $"Time-based reward ({Math.Floor(minsElapsed)} min at {rate:N6}/min)",
                        CreatedAt = nowUtc,
                        ExchangeId = null,                  // not from an exchange
                        IsReleased = true                   // immediately “released”
                    });

                    _logger.LogInformation("Credited {Amount:N4} TK to user {UserId} → balance now {Bal:N4}",
                                               toCredit, prog.UserId, user.DigitalTokenBalance);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crediting user {UserId}", prog.UserId);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        private async Task ResetDailyCapsIfNeeded(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
            var todayUtc = DateTime.UtcNow.Date;

            // find anyone who hasn’t mined since yesterday (toReset    )
            var stale = await db.UserMiningProgresses
                                .Where(p => p.LastEmittedUtc < todayUtc && p.EmittedToday > 0)
                                .ToListAsync(ct);
            if (stale.Count == 0) return;

            foreach (var prog in stale)
                prog.EmittedToday = 0;

            await db.SaveChangesAsync(ct);
        }
    }
}