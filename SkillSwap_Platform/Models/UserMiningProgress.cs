using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class UserMiningProgress
{
    public int UserId { get; set; }

    public DateTime LastEmittedUtc { get; set; }

    public decimal EmittedToday { get; set; }

    public bool IsMiningAllowed { get; set; }
}
