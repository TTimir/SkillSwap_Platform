using SkillSwap_Platform.Models.ViewModels.PaymentGatway;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class DashboardVM
    {
        // Top-level counts
        public int ServicesOffered { get; set; }
        public int NewServicesOffered { get; set; }
        public int CompletedServices { get; set; }
        public int NewServicesCompleted { get; set; }
        public int InQueueServices { get; set; }
        public int NewInQueue { get; set; }
        public int TotalReviews { get; set; }
        public int NewReviews { get; set; }

        // For the bar-chart of top offers
        public List<string> OfferViewLabels { get; set; } = new();
        public List<int> OfferViewData { get; set; } = new();

        // For the doughnut chart of category breakdown
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryData { get; set; } = new();
        public Subscription CurrentSubscription { get; set; }

        // Lists
        public IEnumerable<ServiceSummary> MostViewedServices { get; set; }
        public IEnumerable<ExchangeSummary> RecentPurchases { get; set; }
        public IEnumerable<ActivityItem> RecentActivity { get; set; }

        /// <summary>
        /// True if the user may see charts (Pro+ or Growth)
        /// </summary>
        public bool CanBasicAnalytics { get; set; }

        /// <summary>
        /// True if the user may see the full history section (Growth only)
        /// </summary>
        public bool CanAdvancedAnalytics { get; set; }
        public string BillingCycle { get; set; }
        //public string BillingCycle
        //{
        //    get
        //    {
        //        if (CurrentSubscription == null) return "";
        //        var span = CurrentSubscription.EndDate - CurrentSubscription.StartDate;
        //        // anything longer than 45 days we’ll call “Yearly”
        //        return span.TotalDays > 45
        //            ? "Yearly"
        //            : "Monthly";
        //    }
        //}
        public bool IsAutoRenew { get; set; }
        public DateTime? CancellationDateUtc { get; set; }
        public IList<SubscriptionHistoryItem> BillingHistory { get; set; }

    }

    public class ServiceSummary
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public double Rating { get; set; }
        public string Location { get; set; }
        public string ThumbnailUrl { get; set; }
        public string DetailsUrl { get; set; }
        public string PortfolioJson { get; set; } = "[]";
    }

    public class ExchangeSummary
    {
        public string OtherUser { get; set; }
        public string ServiceTitle { get; set; }
        public DateTime InitiatedDate { get; set; }
        public decimal Amount { get; set; }
        public string OtherUserAvatarUrl { get; set; }
    }

    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string BadgeColorClass { get; set; }
    }
}
