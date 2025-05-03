using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.UserFlag
{
    public interface IUserFlagService
    {
        Task FlagUserAsync(int flaggedUserId, int byUserId, string reason);
        Task<PagedResult<TblUserFlag>> GetPendingFlagsAsync(int page, int pageSize);
        Task DismissFlagAsync(int flagId, int adminId, string adminReason);
        Task RemoveUserAsync(int flagId, int adminId, string adminReason);
        Task<PagedResult<UserFlagHistoryVM>> GetAllFlagHistoriesAsync(int page, int pageSize);
        Task<PagedResult<FlaggedUserSummary>> GetFlaggedUserSummariesAsync(int page, int pageSize);


        /// <summary>
        /// Returns true if there exists a user-flag record for this pair that has not yet been actioned.
        /// </summary>
        Task<bool> HasPendingFlagAsync(int flaggedUserId, int flaggedByUserId);

        Task<PagedResult<TblUserFlag>> GetFlagsForUserAsync(int flaggedUserId, int page, int pageSize);
    }
}
