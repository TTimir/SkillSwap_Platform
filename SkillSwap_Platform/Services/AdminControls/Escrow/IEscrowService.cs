using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.Escrow.EscrowDashboard;

namespace SkillSwap_Platform.Services.AdminControls.Escrow
{
    public interface IEscrowService
    {
        Task<PagedResult<TblEscrow>> GetAllAsync(int page, int pageSize);
        Task<PagedResult<EscrowHistoryVm>> GetHistoryAsync(int page, int pageSize);
        Task<EscrowDashboardVm> GetDashboardAsync(int historyPage, int historyPageSize);
        Task<TblEscrow?> GetByIdAsync(int escrowId);
        Task ReleaseAsync(int escrowId, int adminId, string notes);
        Task RefundAsync(int escrowId, int adminId, string notes);
        Task DisputeAsync(int escrowId, int adminId, string notes);
        // Services/AdminControls/Escrow/IEscrowService.cs
        Task<TblEscrow> CreateAsync(int exchangeId, int buyerId, int sellerId, decimal amount);

    }
}
