namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class OfferFlagVm
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string SellerUserName { get; set; } = "";
        public int FlagCount { get; set; }
        public DateTime LastFlaggedAt { get; set; }
    }
}
