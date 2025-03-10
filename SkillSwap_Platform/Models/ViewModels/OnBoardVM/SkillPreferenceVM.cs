using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class SkillPreferenceVM
    {
        [Display(Name = "Skill Areas You Want to Learn")]
        public string? DesiredSkillAreas { get; set; }

        [Display(Name = "Skill Areas You Offer")]
        public string? OfferedSkillAreas { get; set; }
    }
}
