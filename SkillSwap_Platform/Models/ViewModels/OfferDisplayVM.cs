using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using System.ComponentModel.DataAnnotations;

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
        public double UserRating { get; set; }
        public int ReviewCount { get; set; }
        public string RecommendedPercentage { get; set; }
        public double JobSuccessRate { get; set; }
        public List<string> WillingSkills { get; set; }
        public bool IsOnline { get; set; }
        public bool IsFlagged { get; set; }

        public List<UserLanguageVM> UserLanguages { get; set; }
        public List<UserDetailsVM> UserDetails { get; set; }
        public List<CompareOfferVM> CompareOffers { get; set; } = new List<CompareOfferVM>();
        public List<TblOffer> RelatedOffers { get; set; } = new List<TblOffer>();

        public int OfferOwnerId { get; set; }

        // Add a flag or property to indicate if the exchange is completed
        public bool IsExchangeCompleted { get; set; }
        public int ActiveExchangeCount { get; set; }
        public int Views { get; set; }

        // Nest a review view model (if you want to handle review inputs here)
        public OfferExchangeReviewVM Review { get; set; }
        public IEnumerable<TblReview> Reviews { get; set; }

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

    public class OfferExchangeReviewVM
    {
        public int ExchangeId { get; set; }
        public int OfferId { get; set; }

        [Required(ErrorMessage = "Please provide a rating.")]
        [Range(1, 5, ErrorMessage = "A valid rating between 1 and 5 is required.")]
        public int Rating { get; set; }  // e.g. a score between 1 and 5
        [Required(ErrorMessage = "Comments are required.")]
        [StringLength(300, ErrorMessage = "Comments cannot exceed 1000 characters.")]
        public string Comments { get; set; }
        [Required(ErrorMessage = "Your name is required.")]
        public string ReviewerName { get; set; }

        [Required(ErrorMessage = "Your email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        public string ReviewerEmail { get; set; }
        public bool RememberMe { get; set; }
    }
}
