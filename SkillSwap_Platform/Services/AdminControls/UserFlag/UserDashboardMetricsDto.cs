using SkillSwap_Platform.Services.AdminControls.OfferFlag;

namespace SkillSwap_Platform.Services.AdminControls.UserFlag
{
    public class UserDashboardMetricsDto
    {
        public int TotalUsers { get; set; }
        public int FlaggedUsers { get; set; }
        public int PendingFlags { get; set; }
        public int ResolvedFlags { get; set; }

        public List<DateCount> FlagTrends { get; set; } = new();
        public List<ActionCount> ResolutionBreakdown { get; set; } = new();
        public List<FlaggedUserSummary> MostFlaggedUsers { get; set; } = new();
        public List<RecentActionDto> RecentUserActions { get; set; } = new();
    }
}
