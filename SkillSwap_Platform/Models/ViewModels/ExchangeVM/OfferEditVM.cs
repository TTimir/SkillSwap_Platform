using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class OfferEditVM
    {
        public int OfferId { get; set; }
        public int UserId { get; set; }

        // Basic Info
        [Required(ErrorMessage = "No title in mind?")]
        [Display(Name = "Offer Title")] 
        public string Title { get; set; }
        [Required(ErrorMessage = "No details? Share something.")]
        [Display(Name = "Offer Description")]
        public string Description { get; set; }
        [Required(ErrorMessage = "How many tokens, mate?")]
        [Display(Name = "Token Cost")]
        [Range(0, 10000, ErrorMessage = "Please enter a valid token cost.")]
        public decimal TokenCost { get; set; }
        [Required(ErrorMessage = "At least one day needed.")]
        [Display(Name = "Time Commitment (Days)")]
        [Range(1, 365, ErrorMessage = "Common you also can not learn in 0 days")]
        public int TimeCommitmentDays { get; set; }

        // New fields from the ALTER query
        [Required(ErrorMessage = "In which category does your skill fall, bro?")]
        [Display(Name = "Offer Category")]
        public string Category { get; set; }
        [Display(Name = "Portfolio Files (Optional)")]
        public string? Portfolio { get; set; }  // JSON string

        // Extra fields from our dropdowns:
        [Required(ErrorMessage = "What’s your availability?")]
        [Display(Name = "Availability")]
        public string FreelanceType { get; set; }
        [Required(ErrorMessage = "What’s your skill proficiency?")]
        [Display(Name = "Partner's Skill Level")]
        public string RequiredSkillLevel { get; set; }
        [Required(ErrorMessage = "Pick a language, champ.")]
        [Display(Name = "Partner's Language")]
        public int? RequiredLanguageId { get; set; }
        [Required(ErrorMessage = "How’s your language game?")]
        [Display(Name = "Partner's Proficiency")]
        public string RequiredLanguageLevel { get; set; }

        // For multi-select offered skills stored as comma-separated IDs
        [Required(ErrorMessage = "I know you're good at one.")]
        [Display(Name = "Offered Skill")]
        public List<int> SelectedSkillIds { get; set; }

        // Collections for dropdowns
        [BindNever] 
        public List<SelectListItem> UserSkills { get; set; }
        public List<SelectListItem> UserLanguages { get; set; }
        public List<SelectListItem> FreelanceTypeOptions { get; set; }
        public List<SelectListItem> RequiredSkillLevelOptions { get; set; }
        public List<SelectListItem> RequiredLanguageLevelOptions { get; set; }
        public List<SelectListItem> CategoryOptions { get; set; }
        // For file uploads in edit we might allow new portfolio files.
        public IEnumerable<IFormFile> PortfolioFiles { get; set; }
        public string FinalPortfolioOrder { get; set; }
    }
}
