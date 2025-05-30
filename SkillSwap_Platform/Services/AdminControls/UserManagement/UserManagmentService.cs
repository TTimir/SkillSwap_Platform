using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Certificate;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;
using System.Drawing;

namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public class UserManagmentService : IUserManagmentService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<UserManagmentService> _logger;
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subs;
        public UserManagmentService(SkillSwapDbContext db, ILogger<UserManagmentService> logger, IEmailService emailService, ISubscriptionService subscription)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
            _subs = subscription;
        }

        private async Task<PagedResult<T>> GetPagedAsync<T>(
           IQueryable<T> query,
           int page,
           int pageSize)
        {
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = items
            };
        }

        public Task<PagedResult<UserManagementDto>> GetActiveUsersAsync(
            int page, int pageSize, string term)
        {
            // 1) Base: only un-held users
            var query = _db.TblUsers
                .Where(u => !u.IsHeld);

            // 2) Term filter
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                query = query.Where(u =>
                    u.UserName.Contains(term) ||
                    u.Email.Contains(term));
            }

            // 3) Projection
            var baseQuery = query
                .Select(u => new UserManagementDto
                {
                    UserId = u.UserId,
                    CreatedAt = u.CreatedDate,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    UserName = u.UserName,
                    Email = u.Email,
                    IsHeld = u.IsHeld,
                    HeldAt = u.LockoutEndTime
                });

            // 4) Paging
            return GetPagedAsync(baseQuery, page, pageSize);
        }

        // 3) Held users, paged
        public Task<PagedResult<UserManagementDto>> GetHeldUsersAsync(
            int page, int pageSize, string term)
        {
            // start with held users
            var query = _db.TblUsers
                .Where(u => u.IsHeld);

            // if a search term was provided, filter on username or email
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                query = query.Where(u =>
                    u.UserName.Contains(term) ||
                    u.Email.Contains(term));
            }

            // now project into DTO
            var baseQuery = query
                .Select(u => new UserManagementDto
                {
                    UserId = u.UserId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    UserName = u.UserName,
                    Email = u.Email,
                    IsHeld = u.IsHeld,
                    HeldAt = u.HeldAt,
                    HeldUntil = u.HeldUntil,
                    HeldReason = u.HeldReason,
                    TotalHolds = _db.TblUserHoldHistories.Count(h => h.UserId == u.UserId)
                });

            // and finally page it
            return GetPagedAsync(baseQuery, page, pageSize);
        }

        public async Task<bool> HoldUserAsync(int userId, string category, string reason, DateTime? until, int? adminId = null)
        {
            try
            {
                var user = await _db.TblUsers.FindAsync(userId);
                if (user == null) return false;

                user.IsHeld = true;
                user.HeldAt = DateTime.UtcNow;
                user.HeldCategory = category;
                user.HeldReason = reason;
                user.HeldUntil = until;

                user.ReleasedAt = null;
                user.ReleaseReason = null;
                user.ReleasedByAdmin = null;
                user.TotalHolds++;

                _db.TblUsers.Update(user);
                await _db.SaveChangesAsync();

                var history = new TblUserHoldHistory
                {
                    UserId = user.UserId,
                    HeldAt = user.HeldAt.Value,
                    HeldCategory = category,
                    HeldReason = reason,
                    HeldUntil = until,
                    HeldByAdmin = adminId
                };
                _db.TblUserHoldHistories.Add(history);
                await _db.SaveChangesAsync();

                // send notification
                var holdUntilText = until.HasValue
                    ? until.Value.ToLocalTime().ToString("dd MMM yyyy hh:mm tt") + " IST"
                    : "until further notice";
                // ——— In HoldUserAsync ———
                // 1) figure out their active tier & SLA
                var activeSub = await _subs.GetActiveAsync(user.UserId);
                var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };

                // 2) build a prefixed subject
                var subject = $"[{supportLabel} · {sla}] Important: Your Swapo Account Is Temporarily On Hold";
                var body = $@"
                    <p>Hi {user.FirstName},</p>

                    <p>We hope you’re doing well. We wanted to let you know that as of <strong>{user.HeldAt?.ToLocalTime().ToString("dd MMMM, yyyy hh:mm tt")} IST</strong>, your Swapo account has been placed on hold for the following reason:</p>

                    <blockquote style=""border-left:4px solid #ccc; padding-left:1em; margin:1em 0;"">
                      {reason}
                    </blockquote>

                    <p>This hold will remain in effect <strong>{holdUntilText}</strong>. We understand this may disrupt your workflow, and we sincerely apologize for any inconvenience.</p>

                    <p><strong>If our investigation finds no cause for concern, or once any identified violation of our Terms is resolved, your account will be released immediately and you’ll be able to log in again. We’ll notify you at that time.</strong></p>

                    <p>If you have questions or need further clarification, please reply directly to this email or reach out to our support team at 
                    <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>

                    <p>Thank you for your patience and understanding.</p>

                    <p>Warm regards,<br/>
                    The Swapo Team</p>";

                await _emailService.SendEmailAsync(user.Email, subject, body, isBodyHtml: true);


                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error holding user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ReleaseUserAsync(int userId, string? reason = null, int? adminId = null)
        {
            try
            {
                var user = await _db.TblUsers.FindAsync(userId);
                if (user == null) return false;

                // flip off the hold flag
                user.IsHeld = false;

                // clear all of the hold metadata
                user.HeldAt = null;
                user.HeldUntil = null;
                user.HeldCategory = null;
                user.HeldReason = null;

                user.ReleasedAt = DateTime.UtcNow;
                user.ReleaseReason = reason;
                user.ReleasedByAdmin = adminId;

                _db.TblUsers.Update(user);
                await _db.SaveChangesAsync();

                var hist = await _db.TblUserHoldHistories
                    .Where(h => h.UserId == userId && h.ReleaseAt == null)
                    .OrderByDescending(h => h.HeldAt)
                    .FirstOrDefaultAsync();

                if (hist != null)
                {
                    hist.ReleaseAt = DateTime.UtcNow;
                    hist.ReleaseReason = reason;
                    hist.ReleasedByAdmin = adminId;
                    _db.TblUserHoldHistories.Update(hist);
                }

                await _db.SaveChangesAsync();

                // send notification
                // 1) figure out their active tier & SLA
                var activeSub = await _subs.GetActiveAsync(user.UserId);
                var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };

                // 2) build a prefixed subject
                var subject = $"[{supportLabel} · {sla}] Good News: Your Swapo Account Hold Has Been Lifted";
                var body = $@"
                    <p>Hi {user.FirstName},</p>

                    <p>We’re pleased to let you know that as of <strong>{user.ReleasedAt?.ToLocalTime().ToString("dd MMMM, yyyy hh:mm tt")} IST</strong>, the temporary hold on your Swapo account has been lifted.</p>

                    <p>You can now <a href=""/Home/Login"">log in</a> and resume all your swaps right away.</p>

                    {(string.IsNullOrWhiteSpace(reason)
                                      ? "<p>No additional notes were provided.</p>"
                                      : $@"<p><strong>Administrator’s note:</strong> {reason}</p>")}

                    <p>If there’s anything else we can help with, don’t hesitate to contact us at 
                    <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>

                    <p>Welcome back!<br/>
                    — The Swapo Team</p>";

                await _emailService.SendEmailAsync(user.Email, subject, body, isBodyHtml: true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing user {UserId}", userId);
                return false;
            }
        }

        public async Task<int> ReleaseExpiredHoldsAsync()
        {
            // find all the users whose hold has expired
            var now = DateTime.UtcNow;
            var toRelease = await _db.TblUsers
                .Where(u => u.IsHeld && u.HeldUntil <= now)
                .ToListAsync();

            foreach (var u in toRelease)
            {
                u.IsHeld = false;
                u.HeldAt = null;
                u.HeldUntil = null;
                u.HeldCategory = null;
                u.HeldReason = null;
                u.ReleasedAt = now;
                u.ReleaseReason = "Automatically expired";
                u.ReleasedByAdmin = null;
            }

            if (toRelease.Count > 0)
            {
                _db.TblUsers.UpdateRange(toRelease);
                await _db.SaveChangesAsync();
            }

            // notify them
            foreach (var user in toRelease)
            {
                // 1) figure out their active tier & SLA
                var activeSub = await _subs.GetActiveAsync(user.UserId);
                var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };

                // 2) build a prefixed subject
                var subject = $"[{supportLabel} · {sla}] Your Swapo Account Hold Has Expired";
                var body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        
        <!-- Header -->
        <tr>
          <td style=""border-top:4px solid rgba(0,168,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#00A88F;"">Account Update</h1>
          </td>
        </tr>

        <!-- Main Heading -->
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">Hold Expired</h2>
            <p style=""margin:0 0 15px;"">
              Hi <strong>{user.FirstName}</strong>,
            </p>
            <p style=""margin:0 0 15px;"">
              The temporary hold on your Swapo account has just expired as of 
              <strong>{now.ToLocalTime().ToString("dd MMM yyyy hh:mm tt")} IST</strong>.
            </p>
          </td>
        </tr>

        <!-- Divider -->
        <tr>
          <td style=""padding:0 20px;"">
            <hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""background-color:#00A88F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e0f7f1;font-size:14px;"">
              Thank you for being a valued member of <strong>Swapo</strong>. Your creativity and passion make our community thrive!
            </p>
            <p style=""margin:5px 0;color:#e0f7f1;font-size:13px;"">
              We appreciate you—keep sharing your skills and inspiring others.
            </p>
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>
";
                
                try
                {
                    await _emailService.SendEmailAsync(
                                      user.Email,
                                      subject,
                                      body,
                                      isBodyHtml: true
                                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send auto-expiry notification to user {UserId}", user.UserId);
                }
            }

            return toRelease.Count;
        }

        public Task<PagedResult<HoldHistoryEntryDto>> GetHoldHistoryAsync(
            int page,
            int pageSize,
            int? userId = null)
        {
            // 1) start with the history table
            var baseQuery = _db.TblUserHoldHistories
                .AsNoTracking()
                .OrderByDescending(h => h.HeldAt)
                .AsQueryable();

            // 2) optionally filter by user
            if (userId.HasValue)
                baseQuery = baseQuery.Where(h => h.UserId == userId.Value);

            // 3) project into your DTO
            var dtoQuery = baseQuery.Select(h => new HoldHistoryEntryDto
            {
                Id = h.Id,
                UserId = h.UserId,
                UserName = h.User.FirstName + " " + h.User.LastName
                                 + $" ({h.User.UserName})",
                HeldAt = h.HeldAt,
                HeldCategory = h.HeldCategory,
                HeldReason = h.HeldReason,
                HeldUntil = h.HeldUntil,
                HeldByAdmin = h.HeldByAdmin != null
                    ? _db.TblUsers
                         .Where(a => a.UserId == h.HeldByAdmin)
                         .Select(a => a.UserName)
                         .FirstOrDefault()
                    : "System",
                ReleaseAt = h.ReleaseAt,
                ReleaseReason = h.ReleaseReason ?? "Not yet released",
                ReleasedByAdmin = h.ReleasedByAdmin != null
                    ? _db.TblUsers
                         .Where(a => a.UserId == h.ReleasedByAdmin)
                         .Select(a => a.UserName)
                         .FirstOrDefault()
                    : "System",
            });

            // 4) hand off to your existing pagination helper
            return GetPagedAsync(dtoQuery, page, pageSize);
        }
    }
}
