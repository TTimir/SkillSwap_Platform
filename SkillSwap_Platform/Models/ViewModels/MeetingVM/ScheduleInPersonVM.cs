using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.MeetingVM
{
    public class ScheduleInPersonVM
    {
        [Required]
        public int ExchangeId { get; set; }

        [Required]
        public int OtherUserId { get; set; }

        [Required(ErrorMessage = "Set Meeting Date and Time")]
        [Display(Name = "Meeting Date and Time")]
        public DateTime ScheduledDateTime { get; set; }

        [Required(ErrorMessage = "Location Required!")]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [Display(Name = "Meeting Notes")]
        public string Notes { get; set; }

        [Required(ErrorMessage = "Please specify the meeting duration in minutes.")]
        [Range(1, 720, ErrorMessage = "Meeting duration must be between 1 and 720 minutes.")]
        public int MeetingDurationMinutes { get; set; }
    }
}
