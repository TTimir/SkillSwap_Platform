namespace SkillSwap_Platform.Models.ViewModels.AdminControl.TokenMining
{
    public class UserMiningProgressVM
    {
        public int TotalUsersMiningAllowed { get; set; }
        public int TotalUsersMiningBlocked { get; set; }
        public decimal TotalTokensEmittedToday { get; set; }
    }

    public class TokenEmissionSettingsVM
    {
        public int Id { get; set; }
        public decimal TotalPool { get; set; }
        public DateTime StartDateUtc { get; set; }
        public int DripDays { get; set; }
        public int HalvingPeriodDays { get; set; }
        public decimal DailyCap { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class MiningLogVM
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public decimal Amount { get; set; }
        public DateTime EmittedUtc { get; set; }
    }

    public class RecentMiningLogVM
    {
        public int UserId { get; set; }
        public decimal TotalMinedAmount { get; set; }
        public DateTime LastEmittedUtc { get; set; }
    }

    public class UserMiningStatusVM
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsMiningAllowed { get; set; }
    }

    public class AdminMiningDashboardVM
    {
        public UserMiningProgressVM MiningSummary { get; set; }
        public TokenEmissionSettingsVM CurrentEmissionSettings { get; set; }
        public List<RecentMiningLogVM> RecentTopMiners { get; set; } = new();
        public List<UserMiningStatusVM> UserMiningStatusList { get; set; } = new();
    }
}