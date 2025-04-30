namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class FlaggedReviewDetailsVm
    {
        public int ReviewId { get; set; }
        public int ReplyId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; } = "";
        public string ReviewerUserName { get; set; } = "";
        public int Rating { get; set; }
        public string Comment { get; set; } = "";

        public int? ParentReviewId { get; set; }

        public string? ParentReviewComment { get; set; }

        public List<ReviewFlagLog> Flags { get; set; } = new();
    }
}
