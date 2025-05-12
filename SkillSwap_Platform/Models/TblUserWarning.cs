using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserWarning
{
    public int WarningId { get; set; }

    public int UserId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
