using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblWorkingTime
{
    public int WorkingTimeId { get; set; }

    public int UserId { get; set; }

    public string ScheduleType { get; set; } = null!;

    public string? WorkingHours { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
