namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class InboxVM
    {
        public List<ChatSummaryVM> ChatSummaries { get; set; } = new List<ChatSummaryVM>();
    }

    public class ChatSummaryVM
    {
        public int OtherUserID { get; set; }
        public string OtherUserName { get; set; }
        public string OtherUserProfileImage { get; set; }
        public DateTime LastMessageDate { get; set; }
        public string LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
    }
}
