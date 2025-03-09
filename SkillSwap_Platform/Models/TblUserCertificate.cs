using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserCertificate
{
    public int CertificateId { get; set; }

    public int UserId { get; set; }

    public string CertificateName { get; set; } = null!;

    public string? CertificateFilePath { get; set; }

    public DateTime SubmittedDate { get; set; }

    public bool IsApproved { get; set; }

    public int? ApprovedByAdminId { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? SkillId { get; set; }

    public string VerificationId { get; set; } = null!;

    public virtual TblUser? ApprovedByAdmin { get; set; }

    public virtual TblSkill? Skill { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
