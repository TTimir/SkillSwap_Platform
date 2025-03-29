using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class ContractCreationVM
    {
        [Required(ErrorMessage = "Message ID is required.")]
        public int MessageId { get; set; }

        [Required(ErrorMessage = "Offer ID is required.")]
        public int OfferId { get; set; }

        [Required(ErrorMessage = "Sender User ID is required.")]
        public int SenderUserId { get; set; }

        [Required(ErrorMessage = "Receiver User ID is required.")]
        public int ReceiverUserId { get; set; }

        [Required]
        public string SenderUserName { get; set; }

        [Required]
        public string ReceiverUserName { get; set; }

        [Required(ErrorMessage = "Token Offer is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Token Offer must be 0 or a positive number.")]
        public decimal? TokenOffer { get; set; }

        [Required(ErrorMessage = "Please select an offered skill.")]
        public string OfferedSkill { get; set; }

        // Sender's offered skills used for lookup.
        [BindNever]
        public IEnumerable<SelectListItem> SenderOfferedSkills { get; set; }

        // Additional Terms (optional)
        public string? AdditionalTerms { get; set; }

        [Required(ErrorMessage = "Please enter the process flow description.")]
        public string FlowDescription { get; set; }

        // Document fields – these are used to render the contract document.
        [Required(ErrorMessage = "Your Name is required.")]
        public string SenderName { get; set; }

        [Required(ErrorMessage = "Counterparty Name is required.")]
        public string ReceiverName { get; set; }

        [Required]
        public DateTime? ContractDate { get; set; }
        public bool SenderAgreementAccepted { get; set; }
        public bool ReceiverAgreementAccepted { get; set; }

        // Extended sender details – fetched from DB.
        public string SenderAddress { get; set; }

        public string SenderEmail { get; set; }

        public string ReceiverAddress { get; set; }

        public string ReceiverEmail { get; set; }

        [Required(ErrorMessage = "Learning Duration (Days) is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Learning Days must be at least 1.")]
        public int LearningDays { get; set; }

        // Computed: CompletionDate = ContractDate + LearningDays + 1 day backup.
        public DateTime? CompletionDate { get; set; }
        public DateTime? SenderAcceptanceDate { get; set; }
        public DateTime? ReceiverAcceptanceDate { get; set; }
        public string SenderSignature { get; set; }

        // The registered sender name from the DB (for validation).
        public string AccountSenderName { get; set; }

        // New property: Offer Owner Skill (the service the receiver will offer).
        public string OfferOwnerSkill { get; set; }

        // Flag for rendering mode (true = Preview, false = Create/Edit).
        public bool IsPreview { get; set; }
    }
}
