namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeDashboardVM
    {
        public List<ExchangeDashboardItemVM> ActiveExchangeItems { get; set; }
        public List<ExchangeDashboardItemVM> CompletedExchangeItems { get; set; }
        public List<ExchangeDashboardItemVM> DeclinedExchangeItems { get; set; }
        public TblExchange SelectedExchange { get; set; }
        public int CompletedCurrentPage { get; set; }
        public int CompletedTotalPages { get; set; }
        public int DeclinedCurrentPage { get; set; }
        public int DeclinedTotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool IsGrowthUser { get; set; }
        public List<int> PurchasedCertificates { get; set; } = new();
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

        public string Category { get; set; }
        public decimal? Token { get; set; }

        public bool OfferIsDeleted { get; set; }

        public bool IsOnlineMeetingCompleted { get; set; }
        public bool IsMeetingEnded { get; set; }
        public DateTime? MeetingScheduledDateTime { get; set; }
        public int? InpersonMeetingDurationMinutes { get; set; }

        public bool InPersonOwnerVerified { get; set; }
        public bool InPersonOtherPartyVerified { get; set; }
        public string? StatusChangeReason { get; set; }
        public int? StatusChangeBy { get; set; }

    }
}
