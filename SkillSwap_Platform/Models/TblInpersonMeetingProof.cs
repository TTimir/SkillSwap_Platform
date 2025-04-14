using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblInpersonMeetingProof
{
    public int ProofId { get; set; }

    public int ExchangeId { get; set; }

    public string? StartProofImageUrl { get; set; }

    public DateTime? StartProofDateTime { get; set; }

    public string? StartProofLocation { get; set; }

    public string? EndProofImageUrl { get; set; }

    public DateTime? EndProofDateTime { get; set; }

    public string? EndProofLocation { get; set; }

    public DateTime? UploadedDate { get; set; }

    public int CapturedByUserId { get; set; }

    public string? CapturedByUsername { get; set; }

    public string? EndMeetingNotes { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;
}
