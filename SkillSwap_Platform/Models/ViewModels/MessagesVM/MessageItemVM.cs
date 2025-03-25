namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class MessageItemVM
    {
        public int MessageId { get; set; }
        public int CurrentUserID { get; set; }
        public int SenderUserID { get; set; }
        public string SenderName { get; set; }
        public string SenderProfileImage { get; set; }
        public DateTime SentDate { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; }
        public bool ShowHeader { get; set; }
        public MessageStatus Status { get; set; }
        public int? ReplyMessageId { get; set; }
        public string ReplyPreview { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsApproved { get; set; } = true;
        public IEnumerable<TblMessageAttachment> Attachments { get; set; } = new List<SkillSwap_Platform.Models.TblMessageAttachment>();
        // Computed property: true if the current user sent this message.
        public bool IsSentByCurrent => SenderUserID == CurrentUserID;
    }

    public enum MessageStatus
    {
        Sending,
        Delivered, // delivered but not read yet
        Seen,      // delivered and read (seen)
        Failed
    }

}
