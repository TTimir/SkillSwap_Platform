namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public interface IUserManagmentService
    {
        Task<PagedResult<UserManagementDto>> GetActiveUsersAsync(int page, int pageSize);
        Task<PagedResult<UserManagementDto>> GetHeldUsersAsync(int page, int pageSize);

        Task<bool> HoldUserAsync(int userId, string category, string reason, DateTime? until, int? adminId = null);
        Task<bool> ReleaseUserAsync(int userId, string? reason = null, int? adminId = null);
        Task<int> ReleaseExpiredHoldsAsync();
        // in IUserManagmentService
        Task<PagedResult<HoldHistoryEntryDto>> GetHoldHistoryAsync(
            int page,
            int pageSize,
            int? userId = null);
    }
}
