namespace SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM
{

    public class HistoryItemVm
    {
        public long RequestId { get; set; }
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string Username { get; set; }
        public DateTime Timestamp { get; set; }
        public string Event { get; set; }   // "Submitted", "Approved", "Rejected"
        public string Comments { get; set; }   // only for Approve/Reject
    }
}
