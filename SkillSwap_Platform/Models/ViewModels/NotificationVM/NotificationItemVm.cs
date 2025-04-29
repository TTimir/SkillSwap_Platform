namespace SkillSwap_Platform.Models.ViewModels.NotificationVM
{
    public class NotificationItemVm
    {
        public int SenderUserId { get; set; }
        public string SenderName { get; set; } = "";
        public string ProfileImageUrl { get; set; } = "";
        public string HtmlContent { get; set; } = "";  // original HTML
        public string PreviewText { get; set; } = "";  // plain‑text snippet
        public DateTime SentDate { get; set; }
        public bool IsRead { get; set; }
        public string TimeAgo { get; set; } = "";
    }
}
