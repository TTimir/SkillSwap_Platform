namespace SkillSwap_Platform.Services.AdminControls.Certificate
{
    public class CertificateReviewDto
    {
        public int CertificateId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string LoginName { get; set; }
        public string CertificateName { get; set; }
        public string CertificateFilePath { get; set; }
        public DateTime SubmittedDate { get; set; }

        // bring these along from the EF entity
        public bool? IsApproved { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? RejectDate { get; set; }   

        public ReviewStatus Status { get; set; }  

        public enum ReviewStatus
        {
            Pending,
            Approved,
            Rejected
        }
    }
}
