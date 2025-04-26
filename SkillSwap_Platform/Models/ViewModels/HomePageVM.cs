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
        public List<CategoryOffers> TrendingOffersByCategory { get; set; } = new List<CategoryOffers>();
        public int TotalSkills { get; set; }
        public List<CategoryCardVm> PopularCategories { get; set; } = new();
        public List<HowItWorksVM> HowItWorksValues { get; set; } = new();


        public string TalentsDisplayValue { get; set; } = "";
        public string TalentsSuffix { get; set; } = "";


        public string ExchangeDisplayValue { get; set; } = "";
        public string ExchangeSuffix { get; set; } = "";

        public double GlobalAverageRating { get; set; }
        public int AverageRating { get; set; }
        public int SwapSatisfactionPercent { get; set; }
        public int EarlyAdopterCount { get; set; }

        public List<string> TopSkills { get; set; } = new();
        public List<string> TrendingSkills { get; set; } = new();
        public List<string> TopCountrySkills { get; set; } = new();
        public List<string> ProjectCatalog { get; set; } = new();
        public List<OfferCardVM> GoodSwaps { get; set; } = new();


        public string UserCountryIso { get; set; }      // e.g. "US"
        public string UserCountryName { get; set; }     // e.g. "United States"

    }

    public class CategoryOffers
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategorySlug { get; set; } // e.g. "development-it" to use in IDs
        public List<OfferCardVM> Offers { get; set; }
    }

    public class FreelancerCardVM
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public string ProfileImage { get; set; }
        public string Location { get; set; }
        public int ReviewCount { get; set; }
        public double Rating { get; set; }
        public double? JobSuccess { get; set; }
        public double? Recommendation { get; set; }
        public List<string> OfferedSkillAreas { get; set; } = new List<string>();
    }

    public class CategoryCardVm
    {
        public string Name { get; set; }          // e.g. "Design & Creative"
        public string Slug { get; set; }          // e.g. "design-creative"
        public string IconClass { get; set; }     // e.g. "flaticon-web-design-1"
        public int SkillCount { get; set; }       // aggregate count
        public string Description { get; set; }   // for mobile slider subtitle
    }

    public class HowItWorksVM
    {
        public string Category { get; set; } = "howitworks";
        public string TalentsDisplayValue { get; set; } = "0";
        public string TalentsSuffix { get; set; } = "";
        public int SwapSatisfactionPercent { get; set; }
        public string ExchangeDisplayValue { get; set; } = "0";
        public string ExchangeSuffix { get; set; } = "";
        public int GlobalSuccessRate { get; set; }
        public string SwapsCompletedValue { get; set; } = "0";
        public string SwapsCompletedSuffix { get; set; } = "";

        // Swap Success Rate (adjusted)
        public int AdjustedSuccessRate { get; set; }

        public List<FreelancerCardVM> CommunitySpotlight { get; set; } = new();

    }
}
