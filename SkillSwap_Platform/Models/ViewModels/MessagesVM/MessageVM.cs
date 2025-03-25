namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class MessageVM
    {
        public int MessageID { get; set; }
        public int SenderUserID { get; set; }
        public int ReceiverUserID { get; set; }
        public string? Content { get; set; }
        public string? MeetingLink { get; set; }
        public DateTime SentDate { get; set; }
        public bool IsRead { get; set; }

        // For display or binding attachments if needed
        public List<TblMessageAttachment>? Attachments { get; set; }
    }
}
