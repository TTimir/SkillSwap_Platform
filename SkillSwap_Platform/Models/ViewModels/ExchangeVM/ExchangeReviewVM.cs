using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeReviewVM
    {
        public int ExchangeId { get; set; }
        public string OfferTitle { get; set; }
        [Required(ErrorMessage = "Please provide a rating.")]
        [Range(1, 5, ErrorMessage = "A valid rating between 1 and 5 is required.")]
        public int Rating { get; set; }  // e.g. a score between 1 and 5
        [Required(ErrorMessage = "Comments are required.")]
        [StringLength(300, ErrorMessage = "Comments cannot exceed 1000 characters.")]
        public string Comments { get; set; }
        [Required(ErrorMessage = "Your name is required.")]
        public string ReviewerName { get; set; }

        [Required(ErrorMessage = "Your email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        public string ReviewerEmail { get; set; }
        public bool RememberMe { get; set; }
    }
}
