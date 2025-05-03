using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Services.AdminControls.Message.SensitiveWord
{
    public class SensitiveWordVm
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Word { get; set; } = null!;

        [Required, StringLength(500)]
        public string WarningMessage { get; set; } = null!;
    }
}
