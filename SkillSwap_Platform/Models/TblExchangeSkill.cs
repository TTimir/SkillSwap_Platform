using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblExchangeSkill
{
    public int ExchangeSkillId { get; set; }

    public int ExchangeId { get; set; }

    public int SkillId { get; set; }

    public string? Role { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;

    public virtual TblSkill Skill { get; set; } = null!;
}
