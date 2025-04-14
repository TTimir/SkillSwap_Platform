using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblMeeting
{
    public int MeetingId { get; set; }

    public int CreatorUserId { get; set; }

    public int OtherUserId { get; set; }

    public int? OfferId { get; set; }

    public DateTime MeetingStartTime { get; set; }

    public DateTime? MeetingEndTime { get; set; }

    public int DurationMinutes { get; set; }

    public string MeetingLink { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime ActualStartTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? MeetingNotes { get; set; }

    public string? MeetingType { get; set; }

    public string? Location { get; set; }

    public int ExchangeId { get; set; }

    public int MeetingSessionNumber { get; set; }

    public int? MeetingRating { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;
}
