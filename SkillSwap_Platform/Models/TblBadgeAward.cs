using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblBadgeAward
{
    public long AwardId { get; set; }

    public int UserId { get; set; }

    public int BadgeId { get; set; }

    public DateTime AwardedAt { get; set; }

    public virtual TblBadge Badge { get; set; } = null!;
}
