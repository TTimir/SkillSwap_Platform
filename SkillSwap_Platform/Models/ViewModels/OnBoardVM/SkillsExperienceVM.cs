using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class SkillsExperienceVM
    {
        // Skills & Experience fields (for public profile)
        [Display(Name = "Education")]
        public string? Education { get; set; }

        [Display(Name = "Experience")]
        public string? Experience { get; set; }

        [Display(Name = "Languages")]
        public string? Languages { get; set; }

        // Skill Preferences fields
        [Display(Name = "Skills/Areas You Want to Learn")]
        public string? DesiredSkillAreas { get; set; }

        [Display(Name = "Skills/Areas You Offer")]
        public string? OfferedSkillAreas { get; set; }
    }
}