namespace SkillSwap_Platform.Services.AdminControls.Escrow.EscrowDashboard
{
    public class DashboardSummaryVm
    {
        public decimal NetIncome { get; set; }
        public decimal Withdrawn { get; set; }
        public decimal PendingClearance { get; set; }
        public decimal AvailableForWithdrawal { get; set; }
    }
}
