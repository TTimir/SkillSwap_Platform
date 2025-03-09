using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class UserLoginVM
    {
        [Required(ErrorMessage ="Please provide your email.")]
        [Display(Name = "Username or Email")]
        public string LoginName { get; set; }

        [Required(ErrorMessage ="Please provide password to match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }


        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}
