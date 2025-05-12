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

        public List<TblInPersonMeeting> InPersonMeetings { get; set; }

        public int TotalSwapOffersCount { get; set; }
        public string SearchTerm { get; set; }
        public List<OfferInviteVM> SwapOffers { get; set; } = new();
        public LatestOfferVM LatestActiveOffer { get; set; }

    }

    public class OfferInviteVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string LatestStatus { get; set; }    // e.g. "Pending", "Accepted", "Declined"
        public DateTime LatestDate { get; set; }    // when that status was set
    }

    public class LatestOfferVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string ContractUniqueId { get; set; }
        public string CurrentStagePdfUrl { get; set; }
        public string Status { get; set; }   // e.g. “In Progress”, “Completed”, etc.
    }
}
