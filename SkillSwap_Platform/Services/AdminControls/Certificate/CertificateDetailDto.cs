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

        public enum ReviewStatus { Pending, Approved, Rejected }
        public ReviewStatus Status { get; set; }
        public DateTime? ProcessedDateUtc { get; set; }
        public DateTime? RejectDate { get; set; }
        public string RejectionReason { get; set; }
    }
}
