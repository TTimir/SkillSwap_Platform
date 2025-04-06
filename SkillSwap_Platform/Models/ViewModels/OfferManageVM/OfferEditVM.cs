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

        [Required(ErrorMessage = "Set partner's skill level!")]
        [Display(Name = "Partner's Skill Level")]
        public string RequiredSkillLevel { get; set; }
        [Required(ErrorMessage = "Describe your scope of work!")]
        [Display(Name = "Scope Of Work")]
        public string ScopeOfWork { get; set; } // What is included in the skill exchange?
        [Required(ErrorMessage = "How many rounds?")]
        [Display(Name = "Assistance Rounds")]
        public int? AssistanceRounds { get; set; } // Number of revisions allowed
        [Required(ErrorMessage = "Choose a collaboration type!")]
        [Display(Name = "Collaboration Type")] 
        public string CollaborationMethod { get; set; }

        public List<SelectListItem> CollaborationOptions { get; set; }
        // For multi-select offered skills stored as comma-separated IDs
        [Required(ErrorMessage = "I know you're good at one.")]
        [Display(Name = "Offered Skill")]
        public List<int> SelectedSkillIds { get; set; }

        [Required(ErrorMessage = "Which device is needed, buddy?")]
        [Display(Name = "Devices")] 
        public List<string> SelectedDevices { get; set; }
        public List<SelectListItem> DeviceOptions { get; set; }
        [Required(ErrorMessage = "List any tools!")]
        [Display(Name = "Tools")] 
        public string Tools { get; set; }
        public List<SelectListItem> WillingSkillOptions { get; set; } = new List<SelectListItem>();
        [Required(ErrorMessage = "Willing Skill is required.")]
        [Display(Name = "Intrested in")]
        public string SelectedWillingSkill { get; set; }

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
        public string? FinalPortfolioOrder { get; set; }
    }
}
