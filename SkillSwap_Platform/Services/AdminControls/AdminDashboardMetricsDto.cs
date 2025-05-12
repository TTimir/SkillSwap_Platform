using SkillSwap_Platform.Services.AdminControls.OfferFlag;

namespace SkillSwap_Platform.Services.AdminControls
{
    public class AdminDashboardMetricsDto
    {
        // User Metrics
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersThisWeek { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int ActiveUsersLast7Days { get; set; }
        public int HeldAccounts { get; set; }

        // Offer Metrics
        public int TotalOffers { get; set; }
        public int NewOffersToday { get; set; }
        public int NewOffersThisWeek { get; set; }
        public int CompletedSwaps { get; set; }
        public int CanceledSwaps { get; set; }
        public int PendingOffers { get; set; }

        // Escrow Metrics
        public int ActiveEscrows { get; set; }
        public int ReleasedEscrows { get; set; }
        public int RefundedEscrows { get; set; }
        public int DisputedEscrows { get; set; }
        public decimal TotalTokensHeld { get; set; }
        public decimal TotalTokensReleased { get; set; }
        public double AverageSettlementHours { get; set; }

        // Moderation Summary
        public int PendingOfferFlags { get; set; }
        public int PendingUserFlags { get; set; }
        public int ResolvedOfferFlags { get; set; }
        public int ResolvedUserFlags { get; set; }
        public List<DateCount> FlagTrends { get; set; } = new();
        public List<ResolutionCount> ResolutionBreakdown { get; init; } = new();

        // Swap Activity
        public int TotalSwapsExecuted { get; set; }
        public double SwapSuccessRate { get; set; }
        public decimal AverageSwapValue { get; set; }

        // Revenue & Fees
        public decimal FeesCollected { get; set; }
        public decimal PayoutsToUsers { get; set; }
        public decimal MonthlyRecurringRevenue { get; set; }
        public decimal TotalTokensInCirculation { get; set; }


        public int TotalOnlineMeetings { get; set; }
        public int TotalInPersonMeetings { get; set; }
        public int TotalResourceShare => TotalOnlineMeetings + TotalInPersonMeetings;


        // Support & Feedback
        public int OpenSupportTickets { get; set; }
        public double AverageTicketResolutionHours { get; set; }
        public List<SupportTicketSummary> RecentSupportTickets { get; set; } = new();

        // System Health & Integrations
        public List<WorkflowStatus> Workflows { get; set; } = new();
        public double ApiErrorRate { get; set; }
        public int BackgroundJobsQueued { get; set; }

        // Recent Activity Feed
        public List<RecentActivityItem> RecentActivities { get; set; } = new();

        // Quick Actions & Alerts
        public List<AlertItem> Alerts { get; set; } = new();
        public List<QuickActionItem> QuickActions { get; set; } = new();

        public List<ViewedServiceDto> MostViewedServices { get; set; } = new();
        public List<RecentPurchaseDto> RecentPurchasedServices { get; set; } = new();
    }

    public class ViewedServiceDto
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public int ViewCount { get; set; }
        public string Portfolio { get; set; }
    }

    public class RecentPurchaseDto
    {
        public int ExchangeId { get; set; }
        public string OfferTitle { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }

    public record DateCount(DateTime Date, int Count);
    public record ResolutionCount(string Label, int Count);
    public record SupportTicketSummary(int TicketId, string Title, DateTime CreatedAt);
    public record WorkflowStatus(string Name, DateTime LastRun, bool LastRunSuccessful);
    public record RecentActivityItem(string Description, DateTime Timestamp);
    public record AlertItem(string Message, string Link);
    public record QuickActionItem(string Label, string Url);
}
