namespace SkillSwap_Platform.Services.AdminControls.Certificate
{
    public class CertificateDetailDto : CertificateReviewDto
    {
        public int UserId { get; set; }
        public DateTime? CompleteDate { get; set; }
        public string CertificateFrom { get; set; }
        public int SkillId { get; set; }
        public string VerificationId { get; set; }
        public string CertificateFilePath { get; set; }
        public string RejectionReason { get; set; }
    }
}
