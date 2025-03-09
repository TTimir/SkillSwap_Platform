using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserSkill
{
    public int UserId { get; set; }

    public int SkillId { get; set; }

    public int? ProficiencyLevel { get; set; }

    public bool IsOffering { get; set; }

    public virtual TblSkill Skill { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}
