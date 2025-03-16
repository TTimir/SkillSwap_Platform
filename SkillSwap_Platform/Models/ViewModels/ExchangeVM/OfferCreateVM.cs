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
        [Range(1, 365, ErrorMessage = "Enter a value between 1 and 365 days.")]
        public int TimeCommitmentDays { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [Display(Name = "Category")]
        public string? Category { get; set; }

        //[Display(Name = "Digital Token Value")]
        //[Range(0, 10000, ErrorMessage = "Please enter a valid digital token value.")]
        //public decimal? DigitalTokenValue { get; set; }

        [Display(Name = "Portfolio Files (Optional)")]
        public List<IFormFile>? PortfolioFiles { get; set; }
        // New properties for user skills:
        [Display(Name = "Select Your Offered Skill for this Offer")]
        public int? SelectedSkillId { get; set; }
        [BindNever] 
        public List<SelectListItem> UserSkills { get; set; }
    }
}
