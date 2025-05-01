namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class FlaggedOfferDetailsVm
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string SellerUserName { get; set; } = "";
        public List<OfferFlagLog> Flags { get; set; } = new();
    }
}
