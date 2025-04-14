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
        public List<MessageItemVM> Messages { get; set; }
        public IEnumerable<ChatMemberVM> ChatMembers { get; set; }
        public TblExchange Exchange { get; set; }

        public int? OfferId { get; set; }
        public int OfferOwnerId { get; set; }
        public string ExchangeOfferOwnerName { get; set; }
        public string ExchangeOtherUserName { get; set; }
    }
}
