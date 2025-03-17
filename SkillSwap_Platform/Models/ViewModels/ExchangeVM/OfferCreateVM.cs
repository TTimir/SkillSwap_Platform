using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class OfferCreateVM
    {
        [Required(ErrorMessage = "Title is required.")]
        [Display(Name = "Offer Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [Display(Name = "Offer Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Token Cost")]
        [Range(0, 10000, ErrorMessage = "Please enter a valid token cost.")]
        public decimal TokenCost { get; set; }

        [Required]
        [Display(Name = "Time Commitment (Days)")]
        [Range(1, 365, ErrorMessage = "Common you also can not learn in 0 days")]
        public int TimeCommitmentDays { get; set; }
        [Required]
        [Display(Name = "Availability Type")]
        public string FreelanceType { get; set; } = string.Empty;
        public List<SelectListItem> FreelanceTypeOptions { get; set; } = new List<SelectListItem>();

        [Required]
        [Display(Name = "Required Skill Level")]
        public string RequiredSkillLevel { get; set; } = string.Empty;
        public List<SelectListItem> RequiredSkillLevelOptions { get; set; } = new List<SelectListItem>();

        [Required]
        [Display(Name = "Required Language")]
        public int? RequiredLanguageId { get; set; }
        public List<SelectListItem> UserLanguages { get; set; } = new List<SelectListItem>();

        [Required]
        [Display(Name = "Required Language Proficiency")]
        public string RequiredLanguageLevel { get; set; } = string.Empty;
        public List<SelectListItem> RequiredLanguageLevelOptions { get; set; } = new List<SelectListItem>();

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;
        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();


        //[Display(Name = "Digital Token Value")]
        //[Range(0, 10000, ErrorMessage = "Please enter a valid digital token value.")]
        //public decimal? DigitalTokenValue { get; set; }

        [Display(Name = "Portfolio Files (Optional)")]
        public List<IFormFile>? PortfolioFiles { get; set; }
        // New property for multiple skill selection.
        [Required(ErrorMessage = "Select Your Offered Skill.")]
        [Display(Name = "Select Your Offered Skill for this Offer")]
        public List<int> SelectedSkillIds { get; set; } = new List<int>();
        [BindNever] 
        public List<SelectListItem> UserSkills { get; set; } = new List<SelectListItem>();
    }
}
