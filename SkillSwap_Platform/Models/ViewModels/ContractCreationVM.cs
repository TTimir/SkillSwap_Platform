using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using SkillSwap_Platform.Services.Contracts;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class ContractCreationVM
    {
        public int ContractId { get; set; }

        public string ContractUniqueId { get; set; }

        [Required]
        public int MessageId { get; set; }

        [Required]
        public int OfferId { get; set; }

        [Required]
        public int SenderUserId { get; set; }

        [Required]
        public int ReceiverUserId { get; set; }

        [Required(ErrorMessage = "Token Offer is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Token Offer must be 0 or a positive number.")]
        public decimal? TokenOffer { get; set; }

        [Required(ErrorMessage = "Please select an offered skill.")]
        public string OfferedSkill { get; set; }
        public string? Category { get; set; }
        public int? AssistanceRounds { get; set; }
        public string? LearningObjective { get; set; }  // or Description
        public string? OppositeExperienceLevel { get; set; }
        public string? ModeOfLearning { get; set; }
        public string? OfferOwnerAvailability { get; set; }
        
        [BindNever]
        public IEnumerable<SelectListItem>? SenderOfferedSkills { get; set; }

        public string FlowDescription { get; set; }

        public int Version { get; set; }
        public DateTime? ContractDate { get; set; }
        public string? SenderUserName { get; set; }
        public string? ReceiverUserName { get; set; }

        // Agreement checkboxes
        public bool SenderAgreementAccepted { get; set; }
        public bool ReceiverAgreementAccepted { get; set; }

        public string? AdditionalTerms { get; set; }

        // Document fields
        [Required(ErrorMessage = "Your Name is required.")]
        public string SenderName { get; set; }

        [Required(ErrorMessage = "Learning Duration (Days) is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Learning Days must be at least 1.")]
        public int LearningDays { get; set; }

        public string SenderAddress { get; set; }
        public string SenderEmail { get; set; }

        public DateTime? CompletionDate { get; set; }
        public DateTime? SenderAcceptanceDate { get; set; }
        public DateTime? ReceiverAcceptanceDate { get; set; }

        // For creation we now initialize these as empty strings.
        public string SenderSignature { get; set; } = "";
        public string ReceiverSignature { get; set; } = "";
        public string AccountSenderName { get; set; }

        public string OfferOwnerSkill { get; set; }

        public string SenderPlace { get; set; } = "";
        public string ReceiverPlace { get; set; } = "";

        public bool IsPreview { get; set; } = true;

        public string? senderFullName { get; set; }
        public string? receiverFullName { get; set; }

        public string ReceiverName { get; set; }

        public bool RevealReceiverDetails { get; set; }
        public string ReceiverEmail { get; set; }
        public string DisplayReceiverEmail
        {
            get
            {
                // Only reveal actual email if allowed.
                return RevealReceiverDetails ? ReceiverEmail : "[counterparty@email]";
            }
        }
        public string ReceiverAddress { get; set; }
        public string DisplayReceiverAddress
        {
            get
            {
                return RevealReceiverDetails ? ReceiverAddress : "[Counterparty Address]";
            }
        }


        public DateTime? OriginalCreatedDate { get; set; }
        public string? Status { get; set; }

        // Mode and ActionContext will determine which fields are required.
        [Required]
        public string Mode { get; set; }  // e.g., "Edit", "ReadOnly", "Sign"
        [Required]
        public string ActionContext { get; set; }  // e.g., "ModifyOnly", "Signing"
        public bool HideSenderAcceptance { get; set; }
        public bool HideReceiverAcceptance { get; set; }
        public bool IsPdfDownload { get; set; } = false;

    }
}
