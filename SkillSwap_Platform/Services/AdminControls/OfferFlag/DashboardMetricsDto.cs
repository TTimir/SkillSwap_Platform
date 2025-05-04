namespace SkillSwap_Platform.Services.AdminControls.OfferFlag
{
    public class DashboardMetricsDto
    {
        public int TotalOffers { get; set; }
        public int FlaggedOffers { get; set; }
        public int PendingFlags { get; set; }
        public int ResolvedFlags { get; set; }

        public List<DateCount> FlagTrends { get; set; } = new();
        public List<ActionCount> ResolutionBreakdown { get; set; } = new();
        public List<FlaggedOffersSummary> MostFlaggedOffers { get; set; } = new();
        public List<RecentActionDto> RecentActions { get; set; } = new();
    }

    public record DateCount(DateTime Date, int Count);
    public record ActionCount(string Action, int Count);
    public record FlaggedOffersSummary(
        int OfferId,
        string Title,
        int TotalFlags,
        string Portfolio
    );

    public record RecentActionDto(string Action, string AdminUser, string OfferTitle, DateTime ActionDate);


}
