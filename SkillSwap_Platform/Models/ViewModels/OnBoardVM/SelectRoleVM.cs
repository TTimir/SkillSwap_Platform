using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class SelectRoleVM
    {
        [Required(ErrorMessage = "Please select at least one role.")]
        public string SelectedRole { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please tell us how you heard about us.")]
        [Display(Name = "How did you hear about us?")]
        public string ReferralSource { get; set; } = string.Empty;
    }
}
