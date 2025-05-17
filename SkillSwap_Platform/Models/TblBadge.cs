using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblBadge
{
    public int BadgeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Tier { get; set; }

    public string IconUrl { get; set; } = null!;

    public virtual ICollection<TblBadgeAward> TblBadgeAwards { get; set; } = new List<TblBadgeAward>();
}
