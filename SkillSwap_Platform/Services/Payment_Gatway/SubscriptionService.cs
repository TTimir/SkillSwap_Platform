using Google;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.NotificationTrack;
using System.Drawing;
using Subscription = SkillSwap_Platform.Models.Subscription;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _emailSender;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly INotificationService _notif;

        public SubscriptionService(
            SkillSwapDbContext db,
            IEmailService emailSender,
            ILogger<SubscriptionService> logger,
            INotificationService notification)
        {
            _db = db;
            _emailSender = emailSender;
            _logger = logger;
            _notif = notification;
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

            // NOTIFICATION
            await _notif.AddAsync(new TblNotification
            {
                UserId = userId,
                Title = "Subscription Activated",
                Message = $"Your {planName} plan is now active until {end.ToString("MMMM d, yyyy")}.",
                Url = "/UserDashboard/Index",
                CreatedAt = DateTime.UtcNow
            });

            // --- EMAIL NOTIFICATION: ACTIVATION ---
            var user = await _db.TblUsers.FindAsync(userId);
            if (user != null)
            {
                // 1) figure out their active tier & SLA
                var activeSub = await GetActiveAsync(user.UserId);
                var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };

                // 2) build a prefixed subject
                var subject = $"[{supportLabel} · {sla}] ✅ Your {planName} subscription is now active!";
                var html = $@"
                    <div style=""font-family:Arial,sans-serif;line-height:1.6;color:#333;"">
                      <h1 style=""color:#2a9d8f;"">🎉 Hi {user.FirstName}, your subscription is live! 🎉</h1>
                      <p>Your <strong style=""color:#264653;"">{planName}</strong> plan has been <strong>activated</strong>.</p>
                      <div style=""background:#f0f4f8;border-radius:8px;padding:1em;margin:1.5em 0;"">
                        <p><strong>Subscription ID:</strong> {sub.Id}</p>
                        <p><strong>Start Date:</strong> {start:MMMM d, yyyy}</p>
                        <p><strong>End Date:</strong> {end:MMMM d, yyyy}</p>
                      </div>
                      <p>Manage your subscription anytime in your 
                         <a href=""/UserDashboard/Index"" style=""color:#2a9d8f;text-decoration:none;"">Account Settings</a>.
                      </p>
                      <p style=""margin-top:2em;"">Cheers,<br/><strong>The SkillSwap Team</strong></p>
                    </div>";
                await _emailSender.SendEmailAsync(user.Email, subject, html);
            }
        }

        public async Task UpsertAsync(int userId, string planName, string billingCycle, DateTime start, DateTime end)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existing = await GetActiveAsync(userId);
                if (existing is null)
                    _db.Subscriptions.Add(existing = new Subscription { UserId = userId });

                var isNew = existing is null;

                if (isNew)
                    _db.Subscriptions.Add(existing = new Subscription { UserId = userId });

                existing.PlanName = planName;
                existing.BillingCycle = billingCycle;
                existing.StartDate = start;
                existing.EndDate = end;
                existing.IsAutoRenew = true;   // always turn auto-renew back on

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // NOTIFICATION
                await _notif.AddAsync(new TblNotification
                {
                    UserId = userId,
                    Title = isNew ? "Subscription Activated" : "Subscription Renewed",
                    Message = isNew
                        ? $"Your {planName} plan is now active until {end:MMMM d, yyyy}."
                        : $"Your {planName} plan has been renewed through {end:MMMM d, yyyy}.",
                    Url = "/UserDashboard/Index",
                    CreatedAt = DateTime.UtcNow
                });

                // --- EMAIL NOTIFICATION: RENEWAL or INITIAL ---
                var user = await _db.TblUsers.FindAsync(userId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };
                    var action = isNew ? "activated" : "renewed";
                    var subject = isNew
                        ? $"[{supportLabel} · {sla}] ✅ Your {planName} subscription is now active!"
                        : $"[{supportLabel} · {sla}] 🔁 Your {planName} subscription has been renewed!";
                    var html = $@"
                        <div style=""font-family:Arial,sans-serif;line-height:1.6;color:#333;"">
                          <h1 style=""color:#2a9d8f;"">{(isNew ? "🎉" : "🔁")} Hi {user.FirstName}, your subscription has been {action}! </h1>
                          <p>Your <strong style=""color:#264653;"">{planName}</strong> plan ({billingCycle}) is now {action}.</p>
                          <div style=""background:#f0f4f8;border-radius:8px;padding:1em;margin:1.5em 0;"">
                            <p><strong>Subscription ID:</strong> {existing.Id}</p>
                            <p><strong>Start Date:</strong> {start:MMMM d, yyyy}</p>
                            <p><strong>End Date:</strong> {end:MMMM d, yyyy}</p>
                          </div>
                          <p>Review or upgrade anytime via your 
                             <a href=""/UserDashboard/Index"" style=""color:#2a9d8f;text-decoration:none;"">Account Settings</a>.
                          </p>
                          <p style=""margin-top:2em;"">Thanks for staying with us,<br/><strong>The SkillSwap Team</strong></p>
                        </div>";
                    await _emailSender.SendEmailAsync(user.Email, subject, html);
                }
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

                // NOTIFICATION
                await _notif.AddAsync(new TblNotification
                {
                    UserId = userId,
                    Title = "Auto-Renew Cancelled",
                    Message = $"Your subscription will now end on {sub.EndDate:MMMM d, yyyy}.",
                    Url = "/UserDashboard/Index",
                    CreatedAt = DateTime.UtcNow
                });

                // --- EMAIL NOTIFICATION: CANCELLATION ---
                var user = await _db.TblUsers.FindAsync(userId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] ❌ Your subscription auto-renew has been cancelled";
                    var html = $@"
                        <div style=""font-family:Arial,sans-serif;line-height:1.6;color:#333;"">
                          <h1 style=""color:#e76f51;"">Notice: Auto-renew turned off</h1>
                          <p>We’ve cancelled auto-renew for your subscription <strong>ID {sub.Id}</strong>.</p>
                          <div style=""background:#fdecea;border-radius:8px;padding:1em;margin:1.5em 0;"">
                            <p><strong>End Date Remains:</strong> {sub.EndDate:MMMM d, yyyy}</p>
                            <p><strong>Reason:</strong> {reason}</p>
                          </div>
                          <p style=""margin-top:2em;"">If this was a mistake, you can re-enable auto-renew in your 
                             <a href=""/UserDashboard/Index"" style=""color:#e76f51;text-decoration:none;"">Account Settings</a>.
                          </p>
                          <p style=""margin-top:2em;"">Regards,<br/><strong>The SkillSwap Team</strong></p>
                        </div>";
                    await _emailSender.SendEmailAsync(user.Email, subject, html);
                }
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