using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using System.Text;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using Humanizer;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Skill_Swap.Models;
using System.Diagnostics;

namespace SkillSwap_Platform.Services.AdminControls.AdminNotification
{
    public class AdminNotificationInterceptor : SaveChangesInterceptor
    {
        private readonly IEmailService _email;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AdminNotificationInterceptor> _log;

        // NEW: cache admin emails & track when we last refreshed
        private List<string>? _adminEmails;
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public AdminNotificationInterceptor(
            IEmailService email,
            IHttpContextAccessor httpAccessor,
            ILogger<AdminNotificationInterceptor> log)
        {
            _email = email;
            _http = httpAccessor;
            _log = log;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            var ctx = eventData.Context!;
            var sw = Stopwatch.StartNew();

            // A) Refresh admin email list only every _cacheDuration
            if (_adminEmails == null || DateTime.UtcNow - _lastRefresh > _cacheDuration)
            {
                _adminEmails = ctx.Set<TblUserRole>()
                                  .Include(ur => ur.Role)
                                  .Include(ur => ur.User)
                                  .Where(ur => ur.Role.RoleName == "Admin"
                                            || ur.Role.RoleName == "Moderator")
                                  .Select(ur => ur.User.Email)
                                  .Distinct()
                                  .ToList();
                _lastRefresh = DateTime.UtcNow;
            }

            // B) Gather notifications from only relevant new entities
            var notifications = new List<(string Subject, string BodyHtml)>();

            // Offer flags
            foreach (var f in ctx.ChangeTracker.Entries<TblOfferFlag>()
                               .Where(e => e.State == EntityState.Added)
                               .Select(e => e.Entity))
            {
                notifications.Add(BuildFlagNotification("Offer", f.FlaggedDate));
            }

            // Review flags
            foreach (var r in ctx.ChangeTracker.Entries<TblReview>()
                               .Where(e => e.State == EntityState.Added && e.Entity.IsFlagged)
                               .Select(e => e.Entity))
            {
                notifications.Add(BuildFlagNotification("Review", r.FlaggedDate));
            }

            // Review Reply flags
            foreach (var rr in ctx.ChangeTracker.Entries<TblReviewReply>()
                                .Where(e => e.State == EntityState.Added && e.Entity.IsFlagged)
                                .Select(e => e.Entity))
            {
                notifications.Add(BuildFlagNotification("Review Reply", rr.FlaggedDate));
            }

            // Certificate pending
            foreach (var c in ctx.ChangeTracker.Entries<TblUserCertificate>()
                                .Where(e => e.State == EntityState.Added)
                                .Select(e => e.Entity))
            {
                notifications.Add((
                    Subject: "🆕 Certificate Pending",
                    BodyHtml: $@"
                        <p>Certificate ID {c.CertificateId} awaits approval.</p>
                        <p><a href=""{AdminLink("/Admin/Certificates")}"">Review now</a></p>"
                ));
            }

            // Verification requests
            foreach (var vr in ctx.ChangeTracker.Entries<VerificationRequest>()
                                 .Where(e => e.State == EntityState.Added
                                           && e.Entity.Status == (int)VerificationStatus.Pending)
                                 .Select(e => e.Entity))
            {
                notifications.Add((
                    Subject: "🆕 Verification Request",
                    BodyHtml: $@"
                        <p>User {vr.UserId} requested verification.</p>
                        <p><a href=""{AdminLink("/Admin/VerificationRequests")}"">Review now</a></p>"
                ));
            }

            // Escrow holds
            foreach (var tx in ctx.ChangeTracker.Entries<TblTokenTransaction>()
                                 .Where(e => e.State == EntityState.Added
                                           && e.Entity.TxType == "Hold"
                                           && !e.Entity.IsReleased)
                                 .Select(e => e.Entity))
            {
                notifications.Add((
                    Subject: "🆕 Escrow Transaction",
                    BodyHtml: $@"
                        <p>Transaction {tx.TransactionId} on hold for {tx.Amount} tokens.</p>
                        <p><a href=""{AdminLink("/Admin/Escrow")}"">Review now</a></p>"
                ));
            }

            // OTP failures
            foreach (var oa in ctx.ChangeTracker.Entries<OtpAttempt>()
                                 .Where(e => e.State == EntityState.Added
                                           && !e.Entity.WasSuccessful)
                                 .Select(e => e.Entity))
            {
                notifications.Add((
                    Subject: "⚠️ OTP Failure",
                    BodyHtml: $@"
                        <p>User {oa.UserId} failed OTP at {oa.AttemptedAt:yyyy-MM-dd HH:mm} UTC.</p>"
                ));
            }

            // C) Enqueue AdminNotification rows if any
            if (notifications.Count > 0)
            {
                foreach (var (subject, bodyHtml) in notifications)
                    foreach (var toEmail in _adminEmails!)
                    {
                        ctx.Set<SkillSwap_Platform.Models.AdminNotification>().Add(
                            new SkillSwap_Platform.Models.AdminNotification
                            {
                                ToEmail = toEmail,
                                Subject = subject,
                                Body = WrapInStandardTemplate(bodyHtml),
                                CreatedAtUtc = DateTime.UtcNow
                            });
                    }
            }

            sw.Stop();
            _log.LogDebug("AdminNotificationInterceptor took {Elapsed}ms", sw.ElapsedMilliseconds);

            return base.SavingChanges(eventData, result);
        }

        private (string Subject, string Body) BuildFlagNotification(string what, DateTime? when)
        {
            when ??= DateTime.UtcNow;
            return (
                Subject: $"🚩 New {what} Flagged",
                Body: $@"
                    <p>{what} flagged at {when.Value.ToLocalTime():yyyy-MM-dd HH:mm tt} IST.</p>
                    <p><a href=""{AdminLink("/AdminDashboard/Index")}"">Review</a></p>"
            );
        }

        private string WrapInStandardTemplate(string innerHtml)
        {
            var sb = new StringBuilder();
            sb.Append("<html><body>");
            sb.Append("<h2>SkillSwap Admin Notification</h2>");
            sb.Append(innerHtml);
            sb.Append("<hr/><p><em>If you did not expect this, contact ");
            sb.Append("<a href=\"mailto:skillswap360@gmail.com\">skillswap360@gmail.com</a>.</em></p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string AdminLink(string relativePath)
        {
            var req = _http.HttpContext!.Request;
            return $"{req.Scheme}://{req.Host}{relativePath}";
        }
    }
}