namespace SkillSwap_Platform.Services.AdminControls.Message
{
    public class FlaggedWordDetail
    {
        public string Word { get; set; } = "";
        public string WarningMessage { get; set; } = "";
    }
    public class HeldMessageVM
    {
        public int MessageId { get; set; }
        public string SenderName { get; set; } = default!;
        public string ReceiverName { get; set; } = default!;
        public DateTime SentDate { get; set; }
        public string Content { get; set; } = default!;
        public IReadOnlyList<string> FlaggedWords { get; set; } = Array.Empty<string>();
        public IList<FlaggedWordDetail> FlaggedWordDetails { get; set; } = new List<FlaggedWordDetail>();

        public string Status { get; set; } = "";    // Held | Approved | Dismissed
        public string? AdminUser { get; set; }         // who approved/dismissed
        public DateTime? ActionDate { get; set; }         // when
    }
}
