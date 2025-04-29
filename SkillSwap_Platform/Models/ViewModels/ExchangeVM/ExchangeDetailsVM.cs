namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeDetailsVM
    {
        public TblExchange Exchange { get; set; }
        // The offer details are available through Exchange.Offer,
        // but you can also expose it separately if you prefer.
        public TblOffer Offer => Exchange.Offer;
        public TblContract Contract { get; set; }
        public IEnumerable<TblExchangeHistory> PagedHistory { get; set; }
        public List<TblMeeting> MeetingRecords { get; set; }
        public List<TblInPersonMeeting> InpersonMeetingRecords { get; set; }
        public string LastStatusChangedByName { get; set; }
        public string MeetingLocation { get; set; }

        public string MeetingScheduledTime { get; set; }
        public int TimelineCurrentPage { get; set; }
        public int TimelineTotalPages { get; set; }
        public string SearchTerm { get; set; }
        public string SortOrder { get; set; }  // "asc" or "desc"

        public IEnumerable<ExchangeEventVM> CombinedEvents { get; set; }
        public TblExchange SelectedExchange { get; set; }

    }
}
