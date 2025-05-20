namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway
{
    public class BillingDashboardPoint
    {
        public string Period { get; set; }    // e.g. "2025-01", "2025-02"
        public int Count { get; set; }
    }

    public class AdminBillingDashboardVM
    {
        public List<BillingDashboardPoint> ActiveSubscriptions { get; set; }
        public List<BillingDashboardPoint> NewSubscriptions { get; set; }
        public List<BillingDashboardPoint> Cancellations { get; set; }
        public List<BillingDashboardPoint> Renewals { get; set; }
        // optionally revenue, auto-renew toggles, etc.
    }
}
