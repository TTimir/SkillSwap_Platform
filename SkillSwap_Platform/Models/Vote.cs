using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class Vote
{
    public int VoteId { get; set; }

    public int UserId { get; set; }

    public string TargetType { get; set; } = null!;

    public int TargetId { get; set; }

    public byte VoteValue { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
