using SkillSwap_Platform.Models.ViewModels.OfferFilterVM;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class HomePageVM
    {
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
        public List<OfferCardVM> TrendingOffers { get; set; }
        public List<FreelancerCardVM> HighestRatedFreelancers { get; set; }
    }

    public class FreelancerCardVM
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string ProfileImage { get; set; }
        public string Location { get; set; }
        public double Rating { get; set; }
        public double? JobSuccess { get; set; }
        public double? Recommendation { get; set; }
        public List<string> OfferedSkillAreas { get; set; } = new List<string>();
    }
}
