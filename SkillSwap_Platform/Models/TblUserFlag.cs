using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserFlag
{
    public int UserFlagId { get; set; }

    public int FlaggedUserId { get; set; }

    public int? FlaggedByUserId { get; set; }

    public DateTime FlaggedDate { get; set; }

    public string Reason { get; set; } = null!;

    public int? AdminUserId { get; set; }

    public string? AdminAction { get; set; }

    public string? AdminReason { get; set; }

    public DateTime? AdminActionDate { get; set; }

    public virtual TblUser? AdminUser { get; set; }

    public virtual TblUser? FlaggedByUser { get; set; }

    public virtual TblUser FlaggedUser { get; set; } = null!;
}
