using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.NotificationTrack;
using System.Security.Cryptography;
using System.Security.Policy;

namespace SkillSwap_Platform.Services.PasswordReset
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IUserServices _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly INotificationService _notif;
        public PasswordResetService(
            SkillSwapDbContext db,
            IUserServices userService,
            IEmailService emailService,
            ILogger<PasswordResetService> logger, 
            INotificationService notif)
        {
            _db = db;
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
            _notif = notif;
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

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = user.UserId,
                    Title = "Password Changed",
                    Message = "You successfully changed your account password",
                    Url = null,
                });

                await trx.CommitAsync();

                var htmlBody = @"
                    <!DOCTYPE html>
                    <html lang=""en"">
                    <head>
                      <meta charset=""UTF-8"">
                      <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    </head>
                    <body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
                      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                        <tr>
                          <td align=""center"" style=""padding:20px;"">
                            <!-- Main Card Container -->
                            <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
                              
                              <!-- Header (glassy green bar + SkillSwap text) -->
                              <tr>
                                <td style=""border-top:4px solid rgba(0, 168, 143, 0.8);padding:20px 20px 10px;"">
                                  <h1 style=""margin:0;font-size:24px;color:#00A88F;font-family:Arial,sans-serif;"">
                                    SkillSwap
                                  </h1>
                                </td>
                              </tr>
                    
                              <!-- Body -->
                              <tr>
                                <td style=""padding:20px;color:#333333;line-height:1.5;"">
                                  <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">
                                    Your SkillSwap Password Has Changed
                                  </h2>
                                  <p style=""margin:0 0 10px;"">
                                    Dear <strong>{userName}</strong>,
                                  </p>
                                  <p style=""margin:0 0 15px;"">
                                    We wanted to let you know that your SkillSwap password was successfully changed on
                                    <strong>{DateTime.UtcNow:MMMM d, yyyy 'at' h:mm tt} UTC</strong>.
                                  </p>
                                  <p style=""margin:0 0 15px;"">
                                    If you did not request this change, please
                                    <p style=""color:#00A88F;text-decoration:none;font-weight:bold;"">
                                      contact our support team
                                    </p>
                                    immediately.
                                  </p>
                                </td>
                              </tr>
                    
                              <!-- Divider -->
                              <tr>
                                <td style=""padding:0 20px;"">
                                  <hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;"">
                                </td>
                              </tr>
                    
                              <!-- Footer Links (SkillSwap green) -->
                              <!-- Legal / Trademark Text -->
                              <tr>
                                <td style=""background-color:#00A88F;padding:10px 20px;color:#e0f7f1;font-size:11px;line-height:1.4;text-align:center;"">
                                  <p style=""margin:5px 0;"">
                                    SkillSwap and the SkillSwap logo are trademarks of SkillSwap Inc. All other trademarks are the property of their respective owners.
                                  </p>
                                  <p style=""margin:5px 0;"">
                                    Registered Office: by SkillSwap.
                                  </p>
                                </td>
                              </tr>
                    
                            </table>
                            <!-- End Main Card Container -->
                          </td>
                        </tr>
                      </table>
                    </body>
                    </html>";

                await _emailService.SendEmailAsync(
                    to: user.Email,
                    subject: "Your SkillSwap password has changed",
                    body    : htmlBody,
                    isBodyHtml: true
                );

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