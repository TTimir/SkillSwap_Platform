using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.TokenReserve
{
    public class AdminAdjustDto
    {
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; }    // “Refund”/“Correction”/“Promo”/“Other”
        public string Reason { get; set; }


        [Display(Name = "Normal User")]
        public int? NormalUserId { get; set; }

        [Display(Name = "System User")]
        public int? SystemUserId { get; set; }
    }
}
