using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserHoldHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime HeldAt { get; set; }

    public string? HeldCategory { get; set; }

    public string? HeldReason { get; set; }

    public DateTime? HeldUntil { get; set; }

    public int? HeldByAdmin { get; set; }

    public DateTime? ReleaseAt { get; set; }

    public string? ReleaseReason { get; set; }

    public int? ReleasedByAdmin { get; set; }

    public virtual TblUser? ReleasedByAdminNavigation { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
