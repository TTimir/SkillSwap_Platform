namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class ChatMemberVM
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string ProfileImage { get; set; }
        public string Designation { get; set; }
        public string LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
