using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class VerificationRequest
{
    public long Id { get; set; }

    public string UserId { get; set; } = null!;

    public string GovernmentIdType { get; set; } = null!;

    public string GovernmentIdNumber { get; set; } = null!;

    public string GovernmentIdDocumentPath { get; set; } = null!;

    public string CertificatesJson { get; set; } = null!;

    public string EducationJson { get; set; } = null!;

    public string? ExperienceJson { get; set; }

    public int Status { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public string? ReviewComments { get; set; }
}
