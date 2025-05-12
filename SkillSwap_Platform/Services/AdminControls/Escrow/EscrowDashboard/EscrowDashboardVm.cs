namespace SkillSwap_Platform.Services.AdminControls.Escrow.EscrowDashboard
{
    public class EscrowDashboardVm
    {
        public decimal TotalEscrowed { get; set; }
        public decimal TotalReleased { get; set; }
        public decimal TotalRefunded { get; set; }
        public decimal TotalPending { get; set; }

        // reuse your existing EscrowHistoryVm + paging
        public PagedResult<EscrowHistoryVm> RecentTransactions { get; set; } = new();
    }
}
