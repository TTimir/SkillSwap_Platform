namespace SkillSwap_Platform.Models.ViewModels.NotificationVM
{
    public class NotificationTrackVm
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }                // who should see it
        public string Title { get; set; }              // e.g. “Password Changed”
        public string Message { get; set; }            // e.g. “You successfully changed your password.”
        public string Url { get; set; }                // link target (e.g. to details)
        public DateTime CreatedAt { get; set; }        // timestamp
        public bool IsRead { get; set; } = false;
        public string IconUrl { get; set; } = "/images/resource/notif-1.png";
    }
}
