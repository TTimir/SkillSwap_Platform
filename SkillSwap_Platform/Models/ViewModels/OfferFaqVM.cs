using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class OfferFaqVM
    {
        public int FaqId { get; set; }

        [Required]
        public int OfferId { get; set; }

        [Required, Display(Name = "Question"), StringLength(200)]
        public string Question { get; set; }

        [Required, Display(Name = "Answer"), StringLength(1000)]
        public string Answer { get; set; }
    }
}
