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

        /// <summary>
        /// Looks up the user ID (or full user record) associated with a valid, unexpired reset-token.
        /// </summary>
        Task<int?> GetUserIdByTokenAsync(string token);

        /// <summary>
        /// Returns true if <paramref name="newPassword"/> already matches any existing user’s password,
        /// excluding the user whose ID is <paramref name="excludingUserId"/>.
        /// </summary>
        Task<bool> IsPasswordInUseAsync(string newPassword, int excludingUserId);
    }
}
