using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.OnBoardVM
{
    public class CertificateDetailsVM
    {
        [Required(ErrorMessage = "Certificate Name is required.")]
        [Display(Name = "Certificate Name")]
        public string CertificateName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Certificate File is required.")]
        [Display(Name = "Certificate File")]
        public IFormFile? CertificateFile { get; set; }

        [Display(Name = "Certificate File URL")]
        public string CertificateFilePath { get; set; } = string.Empty;

        [Required(ErrorMessage = "Certificate Start Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Certificate Date")]
        public DateTime CertificateStartDate { get; set; }

        [Display(Name = "Certificate From")]
        public string? CertificateFrom { get; set; }

        [Required(ErrorMessage = "Verification ID is required.")]
        [Display(Name = "Verification ID")]
        public string VerificationId { get; set; } = string.Empty;
    }
}
