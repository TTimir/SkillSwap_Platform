using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.AdminControl
{
    public class RegisterConfirmVM
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, Display(Name = "Verification Code")]
        public string Otp { get; set; }

        // optional: show when it expires
        public DateTime ExpiresAt { get; set; }
    }
}
