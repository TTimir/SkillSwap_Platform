using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class AdditionalInfoVM
    {
        [Display(Name = "Social Media Links")]
        public string? SocialMediaLinks { get; set; }

        [Display(Name = "KYC Document URL (Optional)")]
        public string? KycDocumentPath { get; set; }

        [Display(Name = "KYC Description (Optional)")]
        public string? KycDescription { get; set; }
    }
}
