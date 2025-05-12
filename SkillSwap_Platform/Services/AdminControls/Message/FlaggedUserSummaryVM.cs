namespace SkillSwap_Platform.Services.AdminControls.Message
{
    public class FlaggedUserSummaryVM
    {
        public int SenderUserId { get; set; }
        public string SenderUserName { get; set; } = "";
        public int TotalFlags { get; set; }
        public DateTime LastFlaggedDate { get; set; }
        public int ApprovedCount { get; set; }
        public int DismissedCount { get; set; }
        public string LastAction { get; set; } = "";  // "Approved" or "Dismissed"
    }
}
