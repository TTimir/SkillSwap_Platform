namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class ActiveFlagVm
    {
        public string EntityType { get; set; }   // "Review" or "Reply"
        public int EntityId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; }
        public string AuthorUserName { get; set; }
        public int ReporterUserId { get; set; }
        public string ReporterUserName { get; set; }
        public DateTime FlaggedAt { get; set; }
        public string Reason { get; set; }
    }
}
