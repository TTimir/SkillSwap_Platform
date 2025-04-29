namespace SkillSwap_Platform.Services.PasswordReset
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Generates a one‑time token, emails the user a reset link.
        /// </summary>
        Task SendResetLinkAsync(string email, string originUrl);

        /// <summary>
        /// Validates the token and updates the user’s password.
        /// </summary>
        Task<(bool Succeeded, string? Error)> ResetPasswordAsync(string token, string newPassword);
    }
}
