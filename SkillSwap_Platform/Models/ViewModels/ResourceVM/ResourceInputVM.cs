using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.ResourceVM
{
    public class ResourceInputVM
    {
        [Required]
        [Display(Name = "Resource Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Resource Type")]
        public string ResourceType { get; set; }

        [Display(Name = "URL")]
        public string InputUrl { get; set; }

        [Required(ErrorMessage = "Please select a file to upload.")]
        [Display(Name = "Upload File")]
        public IFormFile File { get; set; }

        // Contextual IDs passed from the selection page.
        [Required]
        public int OwnerUserId { get; set; }
        [Required]
        public int ExchangeId { get; set; }
        [Required]
        public int OfferId { get; set; }
    }
}