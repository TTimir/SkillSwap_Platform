namespace SkillSwap_Platform.Services.AdminControls.Message
{
    public interface IMessageService
    {
        Task<PagedResult<HeldMessageVM>> GetHeldMessagesAsync(int page, int pageSize);
        Task<PagedResult<FlaggedUserSummaryVM>> GetFlaggedUserSummariesAsync(int page, int pageSize);
        Task<PagedResult<HeldMessageVM>> GetHeldMessagesBySenderAsync(int senderUserId, int page, int pageSize);

        Task ApproveMessageAsync(int messageId, int adminId);
        Task DismissMessageAsync(int messageId, int adminId);
    }
}
