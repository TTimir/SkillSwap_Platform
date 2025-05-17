using Google;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using System.Drawing;
using Subscription = SkillSwap_Platform.Models.Subscription;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            SkillSwapDbContext db,
            ILogger<SubscriptionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Subscription> GetActiveAsync(int userId)
        {
            return await _db.Subscriptions
                .Where(s => s.UserId == userId && s.EndDate > DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(int userId, string planName, DateTime start, DateTime end)
        {
            var sub = new Subscription
            {
                UserId = userId,
                PlanName = planName,
                StartDate = start,
                EndDate = end,
                IsAutoRenew = true
            };

            _db.Subscriptions.Add(sub);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Recorded subscription {Plan} for user {User} from {Start} to {End}",
                planName, userId, start, end);
        }

        public async Task UpsertAsync(int userId, string planName, string billingCycle, DateTime start, DateTime end)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existing = await GetActiveAsync(userId);
                if (existing is null)
                    _db.Subscriptions.Add(existing = new Subscription { UserId = userId });

                existing.PlanName = planName;
                existing.BillingCycle = billingCycle;
                existing.StartDate = start;
                existing.EndDate = end;
                existing.IsAutoRenew = true;   // always turn auto-renew back on

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Upserted subscription {Plan} for user {UserId}", planName, userId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Error upserting subscription for user {UserId} plan {PlanName}",
                    userId, planName);
                throw;
            }
        }

        public async Task CancelAutoRenewAsync(int userId, string reason)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sub = await GetActiveAsync(userId);
                if (sub == null || !sub.IsAutoRenew)
                    return;

                // 1) Turn off auto-renew
                sub.IsAutoRenew = false;

                // 2) Record the cancellation reason
                var cancel = new CancellationRequest
                {
                    SubscriptionId = sub.Id,
                    RequestedAt = DateTime.UtcNow,
                    Reason = reason
                };
                _db.CancellationRequests.Add(cancel);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Cancelled auto-renew for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Error cancelling auto-renew for user {UserId}",
                    userId);
                throw;
            }
        }

        public async Task<bool> IsInPlanAsync(int userId, string planName)
        {
            var active = await GetActiveAsync(userId);
            if (active == null) return false;

            // you can define ordering here: Free<Plus<Pro<Growth
            var rank = new Dictionary<string, int>
            {
                ["Free"] = 0,
                ["Plus"] = 1,
                ["Pro"] = 2,
                ["Growth"] = 3
            };
            return rank.TryGetValue(active.PlanName, out var userRank)
                && rank.TryGetValue(planName, out var reqRank)
                && userRank >= reqRank;
        }

        public async Task<SubscriptionTier> GetTierAsync(int userId)
        {
            var sub = await GetActiveAsync(userId);
            if (sub == null) return SubscriptionTier.Free;

            return sub.PlanName switch
            {
                "Plus" => SubscriptionTier.Plus,
                "Pro" => SubscriptionTier.Pro,
                "Growth" => SubscriptionTier.Growth,
                _ => SubscriptionTier.Free
            };
        }
    }
}