using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.MeetingVM
{
    public class EndMeetingDetailsVM
    {
        [Required]
        public int ExchangeId { get; set; }

        [Required]
        public int OtherUserId { get; set; }

        // The time when the meeting ended or when proof was captured.
        [Required(ErrorMessage = "Please specify the end meeting date and time.")]
        [Display(Name = "Meeting End Time")]
        public DateTime EndMeetingDateTime { get; set; }

        // The location from which the end proof is captured.
        [Required(ErrorMessage = "Please provide your end proof location.")]
        [Display(Name = "End Proof Location")]
        public string EndProofLocation { get; set; }

        // Optional: Any notes regarding the meeting conclusion.
        [Display(Name = "Meeting End Notes")]
        public string EndMeetingNotes { get; set; }

        // Optional: The captured proof image as a base64 string.
        [Required(ErrorMessage = "Please capture a photo as your end meeting proof.")]
        public string CapturedProof { get; set; }
    }
}
