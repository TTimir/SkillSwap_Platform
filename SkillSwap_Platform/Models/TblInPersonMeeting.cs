using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblInPersonMeeting
{
    public int InPersonMeetingId { get; set; }

    public int ExchangeId { get; set; }

    public DateTime? MeetingScheduledDateTime { get; set; }

    public string? MeetingLocation { get; set; }

    public string? MeetingNotes { get; set; }

    public int? InpersonMeetingDurationMinutes { get; set; }

    public string? InpersonMeetingOtpOfferOwner { get; set; }

    public string? InpersonMeetingOtpOtherParty { get; set; }

    public bool IsInpersonMeetingVerified { get; set; }

    public DateTime? InpersonMeetingVerifiedDate { get; set; }

    public bool IsInpersonMeetingVerifiedByOfferOwner { get; set; }

    public bool IsInpersonMeetingVerifiedByOtherParty { get; set; }

    public DateTime? InpersonMeetingVerifiedDateOfferOwner { get; set; }

    public DateTime? InpersonMeetingVerifiedDateOtherParty { get; set; }

    public bool IsMeetingEnded { get; set; }

    public DateTime CreatedDate { get; set; }

    public int CreatedByUserId { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;
}
