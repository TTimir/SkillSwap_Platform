using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblExperience
{
    public int ExperienceId { get; set; }

    public int UserId { get; set; }

    public string? CompanyName { get; set; }

    public string? Role { get; set; }

    public string? Position { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? Years { get; set; }

    public string? Description { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
