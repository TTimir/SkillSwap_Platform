﻿using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services
{
    public interface IUserServices
    {
        Task<bool> RegisterUserAsync(TblUser user, string password);
        Task<TblUser> ValidateUserCredentialsAsync(string login, string password);
        Task<TblUser> GetUserByUserNameOrEmailAsync(string userName, string email);
        Task<string> GetTotpQrCodeAsync(string email);
        Task<bool> VerifyTotpAsync(string email, string otp);
        Task<bool> AuthenticateUserAsync(HttpContext httpContext, string login, string password);
        Task<bool> UpdatePasswordAsync(int userId, string newPassword);
        Task<TblPasswordResetToken> GeneratePasswordResetTokenAsync(int userId);
        Task<int?> ValidateAndConsumePasswordResetTokenAsync(string token);
        // New method to retrieve roles as strings
        Task<TblUser> GetUserByIdAsync(int userId);
        Task<TblUser> GetUserByUsername(string username);
        Task<List<string>> GetUserRolesAsync(int userId);

    }
}
