using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblPasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime Expiration { get; set; }

    public bool IsUsed { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
