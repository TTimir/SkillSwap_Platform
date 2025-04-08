namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeDashboardVM
    {
        public List<ExchangeDashboardItemVM> ExchangeItems { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class ExchangeDashboardItemVM
    {
        public TblExchange Exchange { get; set; }
        public List<TblExchangeHistory> History { get; set; }
        public int OfferOwnerId { get; set; }
        public int OtherUserId { get; set; }

        public string ContractUniqueId { get; set; }
        public string OfferTitle { get; set; }
        public DateTime ExchangeStartDate { get; set; }
        public string Status { get; set; }
        public string ExchangeMode { get; set; }
        public int? LastStatusChangedBy { get; set; }
        public string LastStatusChangedByName { get; set; }
        public string? Description { get; set; }
        public string OfferImageUrl { get; set; }

        public bool CanLaunchMeeting { get; set; }
        public int RecentMeetingLaunchCount { get; set; }

    }
}
