using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class Meeting
{
    public int MeetingId { get; set; }

    public int ContractId { get; set; }

    public string RoomName { get; set; } = null!;

    public DateTime ScheduledStartTime { get; set; }

    public DateTime? ActualStartTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual TblContract Contract { get; set; } = null!;
}
