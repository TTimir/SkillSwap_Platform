namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class ReviewFlagVm
    {
        public int ReviewId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; } = "";
        public string ReviewerUserName { get; set; } = "";
        public int Rating { get; set; }
        public int FlagCount { get; set; }
        public DateTime LastFlaggedAt { get; set; }
    }
}
