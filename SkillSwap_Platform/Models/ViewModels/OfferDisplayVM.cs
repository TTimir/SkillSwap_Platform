namespace SkillSwap_Platform.Models.ViewModels
{
    public class OfferDisplayVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; }
        public int TokenCost { get; set; }
        public int TimeCommitmentDays { get; set; }
        public string Category { get; set; }
        public string FreelanceType { get; set; }
        public string RequiredSkillLevel { get; set; }
        public string CollaborationMethod { get; set; }
        public List<string> PortfolioUrls { get; set; } = new List<string>();
        public DateTime CreatedDate { get; set; }
        public List<string> SkillNames { get; set; }
        public string Device { get; set; }
        public string Tools { get; set; }
        public string ScopeOfWork { get; set; }
        public int AssistanceRounds { get; set; }
        public string RecommendedPercentage { get; set; }
        public double JobSuccessRate { get; set; }
        public List<string> WillingSkills { get; set; }
        public bool IsOnline { get; set; }

        public List<UserLanguageVM> UserLanguages { get; set; }
        public List<UserDetailsVM> UserDetails { get; set; }
        public List<CompareOfferVM> CompareOffers { get; set; } = new List<CompareOfferVM>();

    }

    public class UserLanguageVM
    {
        public string Language { get; set; }
        public string Proficiency { get; set; }
    }

    public class UserDetailsVM
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProfileImage { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Location { get; set; }
        public string City { get; set; }
        public string Country { get; set; }

    }
}
