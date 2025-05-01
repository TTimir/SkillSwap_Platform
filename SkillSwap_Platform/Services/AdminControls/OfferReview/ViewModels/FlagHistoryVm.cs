namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class FlagHistoryVm
    {
        public string EntityType { get; set; }  // “Review” or “Reply”
        public int? EntityId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; }
        public string AuthorUserName { get; set; }
        public string AdminUserName { get; set; }
        public string Action { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
