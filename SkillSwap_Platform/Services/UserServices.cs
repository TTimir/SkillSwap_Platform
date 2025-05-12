using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using System.Security.Cryptography;

namespace SkillSwap_Platform.Services
{
    public class UserServices : IUserServices
    {
        private readonly SkillSwapDbContext _dbcontext;
        private readonly IMemoryCache _cache; // ✅ Inject Memory Cache
        private readonly IPasswordHasher<TblUser> _hasher;

        public UserServices(SkillSwapDbContext context, IMemoryCache cache, IPasswordHasher<TblUser> hasher)
        {
            _dbcontext = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache;
            _hasher = hasher;
        }

        public async Task<bool> RegisterUserAsync(TblUser user, string password)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            await using var transaction = await _dbcontext.Database.BeginTransactionAsync(); // ✅ Start transaction

            try
            {
                // ✅ Ensure username/email is unique before insert
                bool exists = await _dbcontext.TblUsers.AnyAsync(u => u.UserName == user.UserName || u.Email == user.Email);
                if (exists)
                {
                    throw new InvalidOperationException("Username or Email already exists.");
                }

                // ✅ Hash the password before storing it
                user.Salt = PasswordHelper.GenerateSalt();
                user.PasswordHash = PasswordHelper.HashPassword(password, user.Salt);

                // IMPORTANT: Only generate a new TOTP secret if one isn't already provided.
                if (string.IsNullOrWhiteSpace(user.TotpSecret))
                {
                    user.TotpSecret = TotpHelper.GenerateSecretKey();
                }

                _dbcontext.TblUsers.Add(user);
                var saveResult = await _dbcontext.SaveChangesAsync() > 0;

                if (!saveResult)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                await transaction.CommitAsync(); // ✅ Commit transaction if successful
                return true;
            }
            catch (Exception ex) // Catch all possible errors
            {
                await transaction.RollbackAsync(); // ✅ Rollback on failure
                throw new InvalidOperationException($"User registration failed: {ex.Message}");
            }
        }

        public async Task<string> GetTotpQrCodeAsync(string email)
        {
            if (_cache.TryGetValue($"QR_{email}", out string cachedQrCode))
            {
                return cachedQrCode;
            }

            var user = await _dbcontext.TblUsers
                .Where(u => u.Email == email && u.IsVerified) // ✅ Ensure user is verified
                .Select(u => new { u.TotpSecret })
                .FirstOrDefaultAsync();

            if (user == null || string.IsNullOrEmpty(user.TotpSecret)) return null;

            string qrCodeUrl = TotpHelper.GenerateQrCodeUrl(user.TotpSecret, email);

            // ✅ Store in cache for 5 minutes to reduce DB load
            _cache.Set($"QR_{email}", qrCodeUrl, TimeSpan.FromMinutes(5));

            return qrCodeUrl;
        }

        public async Task<bool> VerifyTotpAsync(string email, string otp)
        {
            var user = await _dbcontext.TblUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || string.IsNullOrEmpty(user.TotpSecret)) return false;

            // ✅ Prevent brute-force OTP attempts
            if (user.FailedOtpAttempts >= 5 && user.LockoutEndTime.HasValue && user.LockoutEndTime > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Too many failed attempts. Try again later.");
            }

            bool isValid = TotpHelper.VerifyTotpCode(user.TotpSecret, otp, out string totpFailure);
            if (!isValid)
            {
                user.FailedOtpAttempts++;
                if (user.FailedOtpAttempts >= 5)
                {
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(15); // Lock for 15 minutes
                }
                await _dbcontext.SaveChangesAsync();
                await Task.Delay(5000); // ✅ Introduce 5-second delay
                return false;
            }

            // ✅ Reset failed attempts if successful
            user.FailedOtpAttempts = 0;
            user.LockoutEndTime = null;
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AuthenticateUserAsync(HttpContext httpContext, string login, string password)
        {
            var user = await ValidateUserCredentialsAsync(login, password);
            if (user == null) return false;

            // ✅ Retrieve user roles
            var roles = await GetUserRolesAsync(user.UserId);

            // 🔴 Secure: Regenerate session ID to prevent session fixation attacks
            httpContext.Session.Clear();
            httpContext.Session.SetInt32("UserId", user.UserId);
            httpContext.Session.SetString("UserName", user.UserName);
            httpContext.Session.SetString("Email", user.Email);

            // ✅ Store roles only if they exist
            if (roles.Count > 0)
            {
                httpContext.Session.SetObjectAsJson("UserRoles", roles);
            }
            return true;
        }

        public void LogoutUser(HttpContext httpContext)
        {
            httpContext.Session.Clear(); // Clear session on logout
        }

        public async Task<TblUser> ValidateUserCredentialsAsync(string login, string password)
        {
            var user = await _dbcontext.TblUsers
                .Where(u => u.UserName == login || u.Email == login)
                .Select(u => new TblUser
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    Email = u.Email,
                    PasswordHash = u.PasswordHash,
                    Salt = u.Salt,
                    FailedOtpAttempts = u.FailedOtpAttempts,
                    LockoutEndTime = u.LockoutEndTime
                })
                .FirstOrDefaultAsync();

            if (user == null) return null;

            // ✅ Prevent brute force login attempts
            if (user.FailedOtpAttempts >= 5 && user.LockoutEndTime.HasValue && user.LockoutEndTime > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Account is locked. Try again later.");
            }

            bool isValid = PasswordHelper.VerifyPassword(password, user.PasswordHash, user.Salt);
            bool isUpdated = false; // ✅ Flag to track DB updates
            if (!isValid)
            {
                user.FailedOtpAttempts++;
                isUpdated = true; // ✅ Mark as updated

                if (user.FailedOtpAttempts >= 5)
                {
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(15);   
                }

                await _dbcontext.SaveChangesAsync();
                return null;
            }

            // ✅ Only reset attempts if necessary
            if (user.FailedOtpAttempts > 0)
            {
                user.FailedOtpAttempts = 0;
                user.LockoutEndTime = null;
                isUpdated = true; // ✅ Mark as updated
            }

            if (isUpdated)
            {
                await _dbcontext.SaveChangesAsync();
            }

            return user;
        }

        /// <summary>
        /// Safely update a user’s password (hash + salt + new security stamp).
        /// </summary>
        public async Task<bool> UpdatePasswordAsync(int userId, string newPassword)
        {
            try
            {
                var user = await _dbcontext.TblUsers.FindAsync(userId);
                if (user == null) return false;

                // re‑salt & hash
                user.Salt = PasswordHelper.GenerateSalt();
                user.PasswordHash = PasswordHelper.HashPassword(newPassword, user.Salt);
                user.SecurityStamp = Guid.NewGuid().ToString();

                await _dbcontext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // log and swallow so callers get false
                // assume you injected ILogger<UserServices> _logger
                return false;
            }
        }

        /// <summary>
        /// Creates and persists a one‑time password reset token for the given user.
        /// </summary>
        public async Task<TblPasswordResetToken> GeneratePasswordResetTokenAsync(int userId)
        {
            // use a cryptographically strong random token
            var raw = RandomNumberGenerator.GetBytes(64);
            var tokenString = Convert.ToBase64String(raw);

            var prt = new TblPasswordResetToken
            {
                UserId = userId,
                Token = tokenString,
                Expiration = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };

            _dbcontext.TblPasswordResetTokens.Add(prt);
            await _dbcontext.SaveChangesAsync();
            return prt;
        }

        /// <summary>
        /// Validates a reset token, marks it used, and returns the userId if valid.
        /// </summary>
        public async Task<int?> ValidateAndConsumePasswordResetTokenAsync(string token)
        {
            var prt = await _dbcontext.TblPasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token);

            if (prt == null || prt.IsUsed || prt.Expiration < DateTime.UtcNow)
                return null;

            prt.IsUsed = true;
            await _dbcontext.SaveChangesAsync();
            return prt.UserId;
        }


        public async Task<TblUser> GetUserByUserNameOrEmailAsync(string userName, string email)
        {
            return await _dbcontext.TblUsers
                .FirstOrDefaultAsync(u => u.UserName == userName || u.Email == email);
        }

        // Implementation of the new GetUserRolesAsync method.
        public async Task<List<string>> GetUserRolesAsync(int userId)
        {
            // Assumes TblUserRole entity has a navigation property TblRole with property RoleName.
            return await _dbcontext.TblUserRoles
                .Where(ur => ur.UserId == userId && ur.Role != null)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();
        }

        public async Task<TblUser> GetUserByUsername(string username)
        {
            return await _dbcontext.TblUsers.FirstOrDefaultAsync(u => u.UserName == username);
        }

        public async Task<TblUser> GetUserByIdAsync(int userId)
        {
            return await _dbcontext.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
