using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblEducation
{
    public int EducationId { get; set; }

    public int UserId { get; set; }

    public string InstitutionName { get; set; } = null!;

    public string Degree { get; set; } = null!;

    public string? FieldOfStudy { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? DegreeName { get; set; }

    public string? UniversityName { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
