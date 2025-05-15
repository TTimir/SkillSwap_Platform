using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM;

namespace SkillSwap_Platform.Services.ProfileVerification
{
    public interface IVerificationService
    {
        Task SubmitAsync(string userId, SubmitRequestVm vm);
        Task<IList<AdminListVm>> GetPendingAsync();
        Task<AdminDetailsVm> GetDetailsAsync(long requestId);
        Task ApproveAsync(long requestId, string adminId, string comments);
        Task RejectAsync(long requestId, string adminId, string comments);
        Task<IList<HistoryItemVm>> GetHistoryAsync();
        Task RevokeAsync(long requestId, string adminId, string comments);
    }
}
