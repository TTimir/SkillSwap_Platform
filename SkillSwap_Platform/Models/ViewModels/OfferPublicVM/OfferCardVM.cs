namespace SkillSwap_Platform.Models.ViewModels.OfferFilterVM
{
    public class OfferCardVM
    {
        public int UserId { get; set; }
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; }
        public string Category { get; set; }
        public int TokenCost { get; set; }
        public int TimeCommitmentDays { get; set; }
        public string ScopeOfWork { get; set; }
        public string UserName { get; set; }
        public string UserProfileImage { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Location { get; set; }
        public string Country { get; set; }
        public string Thumbnail { get; set; }
        public List<string> PortfolioImages { get; set; }
    }
}
