﻿using Microsoft.EntityFrameworkCore;
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

            // only users who haven’t been credited in the last 5 minutes
            var cutoff = nowUtc.AddMinutes(-5);
            var usersToProcess = await db.UserMiningProgresses
                .AsNoTracking()
                .Where(p => p.IsMiningAllowed && p.LastEmittedUtc <= cutoff)
                .Join(db.TblUsers,
                    prog => prog.UserId,
                    user => user.UserId,
                    (prog, user) => new { prog, user })
                .Where(x => !x.user.IsEscrowAccount
                            && !x.user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                  .Select(x => x.prog)
                .ToListAsync(ct);

            foreach (var prog in usersToProcess)
            {
                try
                {
                    // attach so EF can track updates
                    db.UserMiningProgresses.Attach(prog);

                    // FIRST-RUN INITIALIZATION
                    if (prog.LastEmittedUtc == DateTime.MinValue)
                    {
                        prog.LastEmittedUtc = nowUtc;
                        prog.EmittedToday = 0;
                        _logger.LogInformation("🆕 Initialized start time for user {UserId}", prog.UserId);
                        continue;
                    }

                    var user = await db.TblUsers.FindAsync(new object[] { prog.UserId }, ct);
                    if (user == null)
                    {
                        _logger.LogWarning("User {UserId} disappeared—skipping", prog.UserId);
                        continue;
                    }

                    if (user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        || user.IsEscrowAccount)
                    {
                        _logger.LogInformation("⏭️ Skipped mining for {UserId}: Admin or Escrow", prog.UserId);
                        continue;
                    }

                    if (!prog.IsMiningAllowed) continue;

                    // how many minutes since last emit?
                    var minsElapsed = (decimal)((nowUtc - prog.LastEmittedUtc).TotalMinutes);
                    if (minsElapsed < 5) continue;

                    // total tokens to credit = minsElapsed × rate
                    decimal toCredit = minsElapsed * rate;

                    // enforce daily cap
                    var availableCap = settings.DailyCap - prog.EmittedToday;
                    if (availableCap <= 0) continue;
                    if (toCredit > availableCap)
                        toCredit = availableCap;

                    if (toCredit <= 0) continue;

                    // 1) update user balance
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crediting user {UserId}", prog.UserId);
                }
            }

            try
            {
                var affected = await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx,
                    "❌ DbUpdateException saving mining changes. Entries:\n{Entries}\nInner: {Inner}",
                    string.Join(", ", dbEx.Entries.Select(e => e.Entity.GetType().Name)),
                    dbEx.InnerException?.Message);
                throw;  // or swallow if you want the service to keep running
            }
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