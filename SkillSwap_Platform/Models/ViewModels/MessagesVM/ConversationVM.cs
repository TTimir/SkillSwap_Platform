using Microsoft.AspNetCore.Mvc.Rendering;

namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class ConversationVM
    {
        public int CurrentUserID { get; set; }
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; }
        public string OtherUserProfileImage { get; set; }
        public bool OtherUserIsOnline { get; set; }
        public List<TblMessage> Messages { get; set; } = new List<TblMessage>();
        public IEnumerable<ChatMemberVM> ChatMembers { get; set; }

        public int? OfferId { get; set; }
        public int OfferOwnerId { get; set; }
    }
}
