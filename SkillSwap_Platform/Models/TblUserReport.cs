using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserReport
{
    public int ReportId { get; set; }

    public int ReportedUserId { get; set; }

    public int ReporterUserId { get; set; }

    public string Description { get; set; } = null!;

    public DateTime ReportDate { get; set; }

    public string Status { get; set; } = null!;

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    public string? ActionTaken { get; set; }

    public virtual TblUser ReportedUser { get; set; } = null!;

    public virtual TblUser ReporterUser { get; set; } = null!;

    public virtual TblUser? ReviewedByNavigation { get; set; }
}
