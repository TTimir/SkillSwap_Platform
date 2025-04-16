using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblContract
{
    public int ContractId { get; set; }

    public int MessageId { get; set; }

    public int OfferId { get; set; }

    public string ContractUniqueId { get; set; } = null!;

    public int SenderUserId { get; set; }

    public int ReceiverUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public DateTime? AcceptedDate { get; set; }

    public DateTime? DeclinedDate { get; set; }

    public decimal? TokenOffer { get; set; }

    public string? ContractDocument { get; set; }

    public bool SignedBySender { get; set; }

    public bool SignedByReceiver { get; set; }

    public DateTime? FinalizedDate { get; set; }

    public string? AdditionalTerms { get; set; }

    public string? OfferedSkill { get; set; }

    public string FlowDescription { get; set; } = null!;

    public string? SenderSignature { get; set; }

    public bool SenderAgreementAccepted { get; set; }

    public DateTime? SenderAcceptanceDate { get; set; }

    public string? ReceiverSignature { get; set; }

    public bool ReceiverAgreementAccepted { get; set; }

    public DateTime? ReceiverAcceptanceDate { get; set; }

    public DateTime? CompletionDate { get; set; }

    public int Version { get; set; }

    public string? SenderName { get; set; }

    public string? SenderSkill { get; set; }

    public string? ReceiverName { get; set; }

    public string? ReceiverSkill { get; set; }

    public string? SenderPlace { get; set; }

    public string? ReceiverPlace { get; set; }

    public string? Category { get; set; }

    public string? LearningObjective { get; set; }

    public string? OppositeExperienceLevel { get; set; }

    public string? ModeOfLearning { get; set; }

    public string? OfferOwnerAvailability { get; set; }

    public int? AssistanceRounds { get; set; }

    public string? SenderEmail { get; set; }

    public string? SenderAddress { get; set; }

    public string? ReceiverAddress { get; set; }

    public string? ReceiverEmail { get; set; }

    public string? SenderUserName { get; set; }

    public string? ReceiverUserName { get; set; }

    public int? ParentContractId { get; set; }

    public int? BaseContractId { get; set; }

    public DateTime? RequestDate { get; set; }

    public DateTime? ResponseDate { get; set; }

    public virtual TblMessage Message { get; set; } = null!;

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblUser ReceiverUser { get; set; } = null!;

    public virtual TblUser SenderUser { get; set; } = null!;
}
