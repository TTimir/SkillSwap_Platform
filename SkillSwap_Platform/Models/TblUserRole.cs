using SkillSwap_Platform.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Skill_Swap.Models;

public partial class TblUserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    // Navigation properties
    public virtual TblUser User { get; set; }
    public virtual TblRole Role { get; set; }


}
