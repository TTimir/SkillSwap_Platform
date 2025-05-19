using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.AdminControl
{
    public class RegisterAdminVM
    {
        [Display(Name = "Profile Picture")]
        public IFormFile? ProfileImage { get; set; }

        [Required(ErrorMessage ="Please enter eamil address."), EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage ="please add password."), DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "The Confirm Password field is required."), DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "The First Name field is required."), MaxLength(100)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "The Last Name field is required."), MaxLength(100)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Please select a role.")]
        [Display(Name = "Role")]
        public int SelectedRoleId { get; set; }

        [BindNever]
        [ValidateNever] 
        public List<SelectListItem> AvailableRoles { get; set; }

        [Required(ErrorMessage = "Please enter contact number.")]
        [Display(Name = "Contact No.")]
        public string ContactNo { get; set; }

        [Required]
        [Display(Name = "Email Confirmed")]
        public bool EmailConfirmed { get; set; } = false;

        [Required(ErrorMessage = "Please specify if the user is verified.")]
        [Display(Name = "Is Verified")]
        public bool IsVerified { get; set; }

        [Required(ErrorMessage = "Please specify if the user is active.")]
        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Required(ErrorMessage = "Please specify if the user is held.")]
        [Display(Name = "Is Held")]
        public bool IsHeld { get; set; }

        [Required(ErrorMessage = "Please specify if onboarding is completed.")]
        [Display(Name = "Onboarding Completed")]
        public bool IsOnboardingCompleted { get; set; }
    }
}
