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

        // NEW: status only for all-certs view
        public string Status
            => !ApprovedDate.HasValue
         ? "Pending"
         : (IsApproved == true ? "Approved" : "Rejected");

        // bring these along from the EF entity
        public bool? IsApproved { get; set; }
        public DateTime? ApprovedDate { get; set; }
    }
}
