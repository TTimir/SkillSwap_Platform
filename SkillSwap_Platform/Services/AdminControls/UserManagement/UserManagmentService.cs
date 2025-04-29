using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Certificate;
using SkillSwap_Platform.Services.Email;
using System.Drawing;

namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public class UserManagmentService : IUserManagmentService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<UserManagmentService> _logger;
        private readonly IEmailService _emailService;
        public UserManagmentService(SkillSwapDbContext db, ILogger<UserManagmentService> logger, IEmailService emailService)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
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
            int page, int pageSize)
        {
            var baseQuery = _db.TblUsers
                .Where(u => !u.IsHeld)    // only those not held
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

            return GetPagedAsync(baseQuery, page, pageSize);
        }

        // 3) Held users, paged
        public Task<PagedResult<UserManagementDto>> GetHeldUsersAsync(
            int page, int pageSize)
        {
            var baseQuery = _db.TblUsers
                .Where(u => u.IsHeld)
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
                    ? until.Value.ToString("dd MMM yyyy hh:mm tt") + " UTC"
                    : "until further notice";
                // ——— In HoldUserAsync ———
                var subject = "Important: Your SkillSwap Account Is Temporarily On Hold";
                var body = $@"
                    <p>Hi {user.FirstName},</p>

                    <p>We hope you’re doing well. We wanted to let you know that as of <strong>{user.HeldAt:dd MMM yyyy hh:mm tt} UTC</strong>, your SkillSwap account has been placed on hold for the following reason:</p>

                    <blockquote style=""border-left:4px solid #ccc; padding-left:1em; margin:1em 0;"">
                      {reason}
                    </blockquote>

                    <p>This hold will remain in effect until <strong>{holdUntilText}</strong>. We understand this may disrupt your workflow, and we sincerely apologize for any inconvenience.</p>

                    <p><strong>If our investigation finds no cause for concern, or once any identified violation of our Terms is resolved, your account will be released immediately and you’ll be able to log in again. We’ll notify you at that time.</strong></p>

                    <p>If you have questions or need further clarification, please reply directly to this email or reach out to our support team at 
                    <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>

                    <p>Thank you for your patience and understanding.</p>

                    <p>Warm regards,<br/>
                    The SkillSwap Team</p>";

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
                var subject = "Good News: Your SkillSwap Account Hold Has Been Lifted";
                var body = $@"
                    <p>Hi {user.FirstName},</p>

                    <p>We’re pleased to let you know that as of <strong>{user.ReleasedAt:dd MMM yyyy hh:mm tt} UTC</strong>, the temporary hold on your SkillSwap account has been lifted.</p>

                    <p>You can now <a href=""/Home/Login"">log in</a> and resume all your swaps right away.</p>

                    {(string.IsNullOrWhiteSpace(reason)
                                      ? "<p>No additional notes were provided.</p>"
                                      : $@"<p><strong>Administrator’s note:</strong> {reason}</p>")}

                    <p>If there’s anything else we can help with, don’t hesitate to contact us at 
                    <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>

                    <p>Welcome back!<br/>
                    — The SkillSwap Team</p>";

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
                var subject = "Your SkillSwap Account Hold Has Expired";
                var body = $@"
                    <p>Hi {user.FirstName},</p>
                    <p>The temporary hold on your SkillSwap account has just expired as of {now:dd MMM yyyy hh:mm tt} UTC.</p>
                    <p>You’re all set feel free to <a href=""/Home/Login"">log in</a> and continue swapping!</p>
                    <p>— The SkillSwap Team</p>";
                try
                {
                    await _emailService.SendEmailAsync(user.Email, subject, body, isBodyHtml: true);
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
