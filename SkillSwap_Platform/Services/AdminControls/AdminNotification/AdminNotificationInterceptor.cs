using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using System.Text;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using Humanizer;

namespace SkillSwap_Platform.Services.AdminControls.AdminNotification
{
    public class AdminNotificationInterceptor : SaveChangesInterceptor
    {
        private readonly IEmailService _email;
        private readonly IHttpContextAccessor _http;
        private readonly SkillSwapDbContext _db;

        public AdminNotificationInterceptor(
            IEmailService email,
            IHttpContextAccessor httpAccessor,
            SkillSwapDbContext db)
        {
            _email = email;
            _http = httpAccessor;
            _db = db;
        }

        public override async ValueTask<InterceptionResult<int>>
            SavingChangesAsync(DbContextEventData eventData,
                               InterceptionResult<int> result,
                               CancellationToken cancellationToken = default)
        {
            var ctx = eventData.Context!;
            // 1) collect all *new* entities in the ChangeTracker
            var added = ctx.ChangeTracker
                           .Entries()
                           .Where(e => e.State == EntityState.Added)
                           .Select(e => e.Entity)
                           .ToList();

            // 2) build a list of “events” we need to email
            var notifications = new List<(string Subject, string Body)>();

            foreach (var e in added)
            {
                switch (e)
                {
                    case TblOfferFlag f:
                        notifications.Add(BuildFlagNotification("Offer", f.FlaggedDate));
                        break;
                    case TblReview r when r.IsFlagged:
                        notifications.Add(BuildFlagNotification("Review", r.FlaggedDate));
                        break;
                    case TblReviewReply rr when rr.IsFlagged:
                        notifications.Add(BuildFlagNotification("Review Reply", rr.FlaggedDate));
                        break;
                    case TblUserCertificate c:
                        notifications.Add((
                          Subject: "🆕 Certificate Pending",
                          Body: $@"
                        <p>A new user certificate (ID {c.CertificateId}) is awaiting your approval.</p>
                        <p><a href=""{AdminLink("/Admin/Certificates")}"">Review now</a></p>"
                        ));
                        break;
                    case VerificationRequest vr when vr.Status == (int)VerificationStatus.Pending:
                        notifications.Add((
                          Subject: "🆕 Verification Request",
                          Body: $@"
                        <p>User {vr.UserId} has requested verification.</p>
                        <p><a href=""{AdminLink("/Admin/VerificationRequests")}"">Review now</a></p>"
                        ));
                        break;
                    case TblTokenTransaction tx
                        when tx.TxType == "Hold" && !tx.IsReleased:
                        notifications.Add((
                          Subject: "🆕 Escrow Transaction",
                          Body: $@"
                        <p>Transaction {tx.TransactionId} is now on hold for {tx.Amount} tokens.</p>
                        <p><a href=""{AdminLink("/Admin/Escrow")}"">Review now</a></p>"
                        ));
                        break;
                    case OtpAttempt oa when !oa.WasSuccessful:
                        notifications.Add((
                          Subject: "⚠️ OTP Failure",
                          Body: $@"
                        <p>User {oa.UserId} failed an OTP at {oa.AttemptedAt:yyyy-MM-dd HH:mm} UTC.</p>"
                        ));
                        break;
                }
            }

            // 3) let EF save to the database
            var saved = await base.SavingChangesAsync(eventData, result, cancellationToken);

            var notifyRoles = await _db.TblRoles
             .Where(r => r.RoleName == "Admin" || r.RoleName == "Moderator")
             .Select(r => r.RoleId)
             .ToListAsync(cancellationToken);

            var toEmails = await _db.TblUserRoles
                        .Where(ur => notifyRoles.Contains(ur.RoleId))
                        .Select(ur => ur.User.Email)
                        .Distinct()
                        .ToListAsync(cancellationToken);

            foreach (var note in notifications)
            {
                foreach (var to in toEmails)
                {
                    _db.AdminNotifications.Add(new Models.AdminNotification
                    {
                        ToEmail = to,
                        Subject = note.Subject,
                        Body = WrapInStandardTemplate(note.Body),
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync(cancellationToken);

            return saved;
        }

        private (string Subject, string Body) BuildFlagNotification(string what, DateTime? when)
        {
            when ??= DateTime.UtcNow;
            return (
              Subject: $"🚩 New {what} Flagged",
              Body: $@"
            <p>{what} was flagged at {when?.ToLocalTime().ToString("yyyy-MM-dd HH:mm tt")} IST.</p>
            <p><a href=""{AdminLink($"/AdminDashboard/Index")}"">Review it now</a></p>"
            );
        }

        private string WrapInStandardTemplate(string innerHtml)
        {
            var sb = new StringBuilder();
            sb.Append("<html><body>");
            sb.Append("<h2>SkillSwap Admin Notification</h2>");
            sb.Append(innerHtml);
            sb.Append("<hr/><p><em>Please keep this email confidential.  If you did not expect this notification, contact <a href=\"mailto:skillswap360@gmail.com\">skillswap360@gmail.com</a> immediately.</em></p>");
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