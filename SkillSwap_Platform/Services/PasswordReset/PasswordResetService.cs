using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using System.Security.Cryptography;

namespace SkillSwap_Platform.Services.PasswordReset
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IUserServices _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;

        public PasswordResetService(
            SkillSwapDbContext db,
            IUserServices userService,
            IEmailService emailService,
            ILogger<PasswordResetService> logger)
        {
            _db = db;
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendResetLinkAsync(string email, string originUrl)
        {
            var user = await _userService.GetUserByUserNameOrEmailAsync(null, email);
            if (user == null)
                return; // don't reveal whether email exists

            // generate a secure random token
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var token = Convert.ToBase64String(tokenBytes);

            var reset = new TblPasswordResetToken
            {
                UserId = user.UserId,
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(2),
            };

            try
            {
                _db.TblPasswordResetTokens.Add(reset);
                await _db.SaveChangesAsync();

                var resetLink = $"{originUrl.TrimEnd('/')}/Home/ResetPassword?token={Uri.EscapeDataString(token)}";
                var htmlBody = $@"
                    <p>Hi {user.FirstName},</p>
                    <p>We received a request to reset your password. Click the link below to choose a new one:</p>
                    <p><a href=""{resetLink}"">Reset your password</a></p>
                    <p>This link will expire in 2 hours. If you didn't request this, you can safely ignore this email.</p>
                    <p>— The SkillSwap Team</p>";

                await _emailService.SendEmailAsync(
                    to: user.Email,
                    subject: "SkillSwap Password Reset",
                    body: htmlBody,
                    isBodyHtml: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating or emailing password reset token for {Email}", email);
                // swallow or rethrow depending on your policy
            }
        }

        public async Task<(bool Succeeded, string? Error)> ResetPasswordAsync(string token, string newPassword)
        {
            var now = DateTime.UtcNow;

            // 1) load the token row (no Include needed)
            var resetEntry = await _db.TblPasswordResetTokens
                .FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    t.Expiration > now);

            if (resetEntry == null)
                return (false, "Invalid or expired reset link.");

            // 2) load the user
            var user = await _db.TblUsers
                .FirstOrDefaultAsync(u => u.UserId == resetEntry.UserId);

            if (user == null)
                return (false, "User not found.");
            
            using var trx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 3) update password (hash+salt, etc.)
                user.Salt = PasswordHelper.GenerateSalt();
                user.PasswordHash = PasswordHelper.HashPassword(newPassword, user.Salt);

                // 4) mark the token used
                resetEntry.IsUsed = true;

                // update the user’s password (assumes IUserServices handles hashing)
                var updated = await _userService.UpdatePasswordAsync(user.UserId, newPassword);
                if (!updated)
                    throw new Exception("Password update failed.");

                await _db.SaveChangesAsync();
                await trx.CommitAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "Error resetting password for token {Token}", token);
                return (false, "An unexpected error occurred. Please try again.");
            }
        }
    }
}