using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblContract
{
    public int ContractId { get; set; }

    public int MessageId { get; set; }

    public int OfferId { get; set; }

    public int SenderUserId { get; set; }

    public int ReceiverUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public decimal? TokenOffer { get; set; }

    public string? ContractDocument { get; set; }

    public bool SignedBySender { get; set; }

    public bool SignedByReceiver { get; set; }

    public DateTime? FinalizedDate { get; set; }

    public string? AdditionalTerms { get; set; }

    public string? OfferedSkill { get; set; }

    public string FlowDescription { get; set; } = null!;

    public bool SenderAgreementAccepted { get; set; }

    public DateTime? SenderAcceptanceDate { get; set; }

    public string? SenderSignature { get; set; }

    public virtual TblMessage Message { get; set; } = null!;

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblUser ReceiverUser { get; set; } = null!;

    public virtual TblUser SenderUser { get; set; } = null!;
}
