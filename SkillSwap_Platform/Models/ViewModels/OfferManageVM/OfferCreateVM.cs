using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class OfferCreateVM : IValidatableObject
    {
        [Required(ErrorMessage = "No title in mind?")]
        [Display(Name = "Offer Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "How many tokens, mate?")]
        [Display(Name = "Token Cost")]
        [Range(0, 10000, ErrorMessage = "Please enter a valid token cost.")]
        public decimal TokenCost { get; set; }
        [Required(ErrorMessage = "Describe your work!")]
        [Display(Name = "Scope Of Work")]
        public string ScopeOfWork { get; set; } // What is included in the skill exchange?
        [Required(ErrorMessage = "How many rounds?")]
        [Display(Name = "Assistance Rounds")] 
        public int AssistanceRounds { get; set; } // Number of revisions allowed

        [Required(ErrorMessage = "At least one day needed.")]
        [Display(Name = "Time Commitment (Days)")]
        [Range(1, 365, ErrorMessage = "Common you also can not learn in 0 days")]
        public int TimeCommitmentDays { get; set; }
        [Required(ErrorMessage = "What’s your availability?")]
        [Display(Name = "Availability")]
        public string FreelanceType { get; set; } = string.Empty;
        public List<SelectListItem> FreelanceTypeOptions { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "Set partner's skill level!")]
        [Display(Name = "Partner's Skill Level")]
        public string RequiredSkillLevel { get; set; } = string.Empty;
        public List<SelectListItem> RequiredSkillLevelOptions { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "In which category does your skill fall, bro?")]
        [Display(Name = "Offer Category")]
        public string Category { get; set; } = string.Empty;
        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "Pick required devices!")]
        [Display(Name = "Devices")]
        public List<string> SelectedDevices { get; set; } = new List<string>();

        [Required(ErrorMessage = "List any tools!")]
        [Display(Name = "Tools")] 
        public string Tools { get; set; }
        [Required(ErrorMessage = "Choose a collaboration type!")]
        [Display(Name = "Collaboration Type")] 
        public string CollaborationMethod { get; set; }

        [Display(Name = "Portfolio Files (Optional)")]
        public List<IFormFile>? PortfolioFiles { get; set; }
        // New property for multiple skill selection.
        [Required(ErrorMessage = "I know you're good at one.")]
        [Display(Name = "Offered Skill")]
        public List<int> SelectedSkillIds { get; set; } = new List<int>();
        // Dropdown options for willing skills
        public List<SelectListItem> WillingSkillOptions { get; set; } = new List<SelectListItem>();
        // The selected willing skill value
        [Required(ErrorMessage = "Willing Skill is required.")]
        [Display(Name = "Intrested in")]
        public string SelectedWillingSkill { get; set; }

        [Display(Name = "Latitude")]
        public double? Latitude { get; set; }

        [Display(Name = "Longitude")]
        public double? Longitude { get; set; }
        [Display(Name = "Address")]
        public string? Address { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Address)
             && (!Latitude.HasValue || !Longitude.HasValue))
            {
                yield return new ValidationResult(
                    "You must either enter a manual address or fetch your GPS coordinates.",
                    new[] { nameof(Address), nameof(Latitude), nameof(Longitude) }
                );
            }
        }

        [BindNever] 
        public List<SelectListItem> UserSkills { get; set; } = new List<SelectListItem>();
        [BindNever]
        public List<SelectListItem> DeviceOptions { get; set; } = new List<SelectListItem>();

        [BindNever]
        public List<SelectListItem> CollaborationOptions { get; set; } = new List<SelectListItem>();

    }
}
