namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeEventVM
    {
        public int SrNo { get; set; }
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } // "Timeline" or "Meeting"
        public string StepOrMeetingType { get; set; } // Timeline step or Meeting type
        public string Description { get; set; } // Timeline reason or Meeting notes
        public TimeSpan? Duration { get; set; } // For meetings
        public int? SessionNumber { get; set; } // For meetings
        public string MeetingRank { get; set; } // For rank
        public DateTime? MeetingStartTime { get; set; }
        public string StatusChangedByName { get; set; }

    }
}
