namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway
{
    public class SubscriptionHistoryItem
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
        public bool IsAutoRenew { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
    }

    public class BillingHistoryVM
    {
        public IList<SubscriptionHistoryItem> BillingHistory { get; set; }
            = Array.Empty<SubscriptionHistoryItem>();

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
