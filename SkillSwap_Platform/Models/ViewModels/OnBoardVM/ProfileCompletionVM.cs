using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class ProfileCompletionVM
    {

        [Display(Name = "Profile Image URL")]
        public string? ProfileImageUrl { get; set; }

        [Display(Name = "Profile Image")]
        public IFormFile? ProfileImageFile { get; set; }

        [Display(Name = "Personal Website")]
        public string? PersonalWebsite { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [Display(Name = "City")]
        public string? City { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        [Display(Name = "Country")]
        public string? Country { get; set; }
        [Required(ErrorMessage = "Designation is required.")]
        [Display(Name = "Designation")] 
        public string Designation { get; set; }
        [Required(ErrorMessage = "Zip is required.")]
        [Display(Name = "Zip Code")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Please enter a valid 6-digit Indian postal code.")]
        public string Zip { get; set; }

        [Required(ErrorMessage = "Please introduce yourself")]
        [Display(Name = "About Me")]
        public string? AboutMe { get; set; }
    }
}
