using Skill_Swap.Models;
using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblRole
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<TblUser> Users { get; set; } = new List<TblUser>();
    public virtual ICollection<TblUserRole> TblUserRoles { get; set; } = new HashSet<TblUserRole>();
}
