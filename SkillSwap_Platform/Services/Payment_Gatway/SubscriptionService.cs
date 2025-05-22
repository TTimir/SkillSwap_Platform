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
                BillingCycle = "initial",
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
                Message = $"Your {planName} plan is now active until {end.ToLocalTime().ToString("MMMM d, yyyy")}.",
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
                var htmlBody6 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        <tr>
          <td style=""border-top:4px solid rgba(41,157,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#299D8F;"">Subscription Activated</h1>
          </td>
        </tr>
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">🎉 Hi {user.FirstName}, your subscription is live! 🎉</h2>
            <div style=""background:#f0f4f8;border-radius:8px;padding:1em;margin:1em 0;"">
              <p><strong>Subscription ID:</strong> {sub.Id}</p>
              <p><strong>Plan:</strong> {planName}</p>
              <p><strong>Start:</strong> {start.ToLocalTime().ToString("MMMM d, yyyy")}</p>
              <p><strong>End:</strong> {end.ToLocalTime().ToString("MMMM d, yyyy")}</p>
            </div>
            <p style=""margin:0;"">
              Manage your subscription anytime in your <a href=""/Account"" style=""color:#299D8F;text-decoration:underline;"">Account</a>.
            </p>
          </td>
        </tr>
        <tr>
          <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
        </tr>
        <tr>
          <td style=""background-color:#299D8F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e6f4f1;font-size:14px;"">
              Thank you for choosing <strong>Swapo</strong>!
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>
";
                await _emailSender.SendEmailAsync(user.Email, subject, htmlBody6, isBodyHtml: true);
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

                // 2) Check if this is simply a renewal extension
                bool isRenewalExtension = existing != null
                    && string.Equals(existing.PlanName, planName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.BillingCycle, billingCycle, StringComparison.OrdinalIgnoreCase)
                    && existing.EndDate > DateTime.UtcNow;

                if (isRenewalExtension)
                {
                    // EXTENSION: extend the EndDate by one cycle
                    var extensionStart = existing.EndDate;
                    existing.EndDate = billingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase)
                        ? extensionStart.AddYears(1)
                        : extensionStart.AddMonths(1);
                }
                else
                {
                    // 3) Brand-new or different-plan subscription
                    if (existing is null)
                    {
                        existing = new Subscription { UserId = userId };
                        _db.Subscriptions.Add(existing);
                    }

                    existing.PlanName = planName;
                    existing.BillingCycle = billingCycle;
                    existing.StartDate = start;  // usually DateTime.UtcNow
                    existing.EndDate = end;    // start.AddMonths or AddYears
                }

                existing.IsAutoRenew = true;   // keep auto-renew on

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // NOTIFICATION
                await _notif.AddAsync(new TblNotification
                {
                    UserId = userId,
                    Title = isRenewalExtension ? "Subscription Activated" : "Subscription Renewed",
                    Message = isRenewalExtension
                        ? $"Your {planName} plan is now active until {end.ToLocalTime().ToString("MMMM d, yyyy")}."
                        : $"Your {planName} plan has been renewed through {end.ToLocalTime().ToString("MMMM d, yyyy")}.",
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
                    var action = isRenewalExtension ? "activated" : "renewed";
                    var subject = isRenewalExtension
                        ? $"[{supportLabel} · {sla}] ✅ Your {planName} subscription is now active!"
                        : $"[{supportLabel} · {sla}] 🔁 Your {planName} subscription has been renewed!";
                    var htmlBody7 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        <tr>
          <td style=""border-top:4px solid rgba(41,157,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#299D8F;"">{(isRenewalExtension ? "🎉" : "🔁")} Subscription {action}!</h1>
          </td>
        </tr>
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <p style=""margin:0 0 15px;"">
              Your <strong>{planName}</strong> plan ({billingCycle}) has been {action}.
            </p>
            <div style=""background:#f0f4f8;border-radius:8px;padding:1em;margin:1em 0;"">
              <p><strong>Subscription ID:</strong> {existing.Id}</p>
              <p><strong>Start:</strong> {start:MMMM d, yyyy}</p>
              <p><strong>End:</strong> {end:MMMM d, yyyy}</p>
            </div>
            <p style=""margin:0;"">
              Review or upgrade anytime in your <a href=""/Account"" style=""color:#299D8F;text-decoration:underline;"">Account</a>.
            </p>
          </td>
        </tr>
        <tr>
          <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
        </tr>
        <tr>
          <td style=""background-color:#299D8F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e6f4f1;font-size:14px;"">
              Thanks for staying with <strong>Swapo</strong>!
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>
";
                    await _emailSender.SendEmailAsync(user.Email, subject, htmlBody7, isBodyHtml: true);
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
                    Message = $"Your subscription will now end on {sub.EndDate.ToLocalTime().ToString("MMMM d, yyyy")}.",
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
                    var htmlBody8 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        <tr>
          <td style=""border-top:4px solid rgba(231,111,81,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#E76F51;"">Auto-Renew Canceled</h1>
          </td>
        </tr>
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <p style=""margin:0 0 15px;"">
              We’ve turned off auto-renew for Subscription ID <strong>{sub.Id}</strong>.
            </p>
            <div style=""background:#fdecea;border-radius:8px;padding:1em;margin:1em 0;"">
              <p><strong>End Date:</strong> {sub.EndDate:MMMM d, yyyy}</p>
              <p><strong>Reason:</strong> {reason}</p>
            </div>
            <p style=""margin:0;"">
              If this was a mistake, re-enable auto-renew in your <a href=""/Account"" style=""color:#E76F51;text-decoration:underline;"">Account</a>.
            </p>
          </td>
        </tr>
        <tr>
          <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
        </tr>
        <tr>
          <td style=""background-color:#E76F51;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#fceae9;font-size:14px;"">
              We’re here if you need anything!
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>
";
                    await _emailSender.SendEmailAsync(user.Email, subject, htmlBody8, isBodyHtml: true);
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

        // Services/Payment_Gatway/SubscriptionService.cs
        public async Task RecordPaymentAsync(string orderId, string paymentId, decimal paidAmount, string desiredPlanName, string billingCycle)
        {
            // 1) Find the subscription by GatewayOrderId
            var sub = await _db.Subscriptions
                .Where(s => s.GatewayOrderId == orderId)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (sub == null)
                throw new InvalidOperationException($"No subscription found for order {orderId}");

            // 2) Record the payment details
            sub.GatewayPaymentId = paymentId;
            sub.PaidAmount = paidAmount;
            await _db.SaveChangesAsync();

            // 3) Upgrade/renew the subscription
            // — only extend from EndDate if cycle hasn’t changed and sub still active
            bool sameCycle =
                    string.Equals(sub.BillingCycle, billingCycle, StringComparison.OrdinalIgnoreCase)
                    && sub.EndDate > DateTime.UtcNow;

            var start = sameCycle
                    ? sub.EndDate
                    : DateTime.UtcNow;

            var end = billingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase)
                    ? start.AddYears(1)
                    : start.AddMonths(1);

            await UpsertAsync(sub.UserId, desiredPlanName, billingCycle, start, end);
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