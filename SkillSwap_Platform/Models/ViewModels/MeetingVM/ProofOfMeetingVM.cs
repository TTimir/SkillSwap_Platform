using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.MeetingVM
{
    public class ProofOfMeetingVM
    {
        [Required]
        public int ExchangeId { get; set; }

        [Required]
        public DateTime ProofDateTime { get; set; }

        public string ProofLocation { get; set; }

        [Required]
        public string CapturedProof { get; set; }

        // These will be set on the server; exclude them from binding/validation.
        [BindNever, ValidateNever]
        public int CapturedByUserId { get; set; }

        [BindNever, ValidateNever]
        public string CapturedByUsername { get; set; }
    }
}
