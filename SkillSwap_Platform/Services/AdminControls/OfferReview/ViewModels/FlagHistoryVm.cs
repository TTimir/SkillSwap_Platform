namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class FlagHistoryVm
    {
        public string EntityType { get; set; }  // “Review” or “Reply”
        public int? EntityId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; }
        public string ReviewAuthorUserName { get; set; } = "";   // who wrote it
        public string FlaggedByUserName { get; set; } = "";    // who reported
        public DateTime FlaggedDate { get; set; }         // when flagged
        public string Excerpt { get; set; } = "";    // first ~50 chars
        public string AdminUserName { get; set; } = "";
        public string AdminAction { get; set; } = "";
        public DateTime? AdminActionDate { get; set; }
        public string AdminReason { get; set; } = "";
    }
}
