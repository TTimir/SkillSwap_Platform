using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class MiningLog
{
    public long Id { get; set; }

    public int UserId { get; set; }

    public DateTime EmittedUtc { get; set; }

    public decimal Amount { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
