using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services
{
    public interface IUserServices
    {
        Task<bool> RegisterUserAsync(TblUser user, string password);
        Task<TblUser> ValidateUserCredentialsAsync(string login, string password);
        Task<TblUser> GetUserByUserNameOrEmailAsync(string login);
        Task<string> GetTotpQrCodeAsync(string email);
        Task<bool> VerifyTotpAsync(string email, string otp);
        Task<bool> AuthenticateUserAsync(HttpContext httpContext, string login, string password);

        // New method to retrieve roles as strings
        Task<TblUser> GetUserByIdAsync(int userId);
        Task<List<string>> GetUserRolesAsync(int userId);

    }
}
