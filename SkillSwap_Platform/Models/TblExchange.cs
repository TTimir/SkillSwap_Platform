using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblExchange
{
    public int ExchangeId { get; set; }

    public int OfferId { get; set; }

    public int RequesterId { get; set; }

    public DateTime ExchangeDate { get; set; }

    public DateTime LastStatusChangeDate { get; set; }

    public string Status { get; set; } = null!;

    public string ExchangeMode { get; set; } = null!;

    public bool IsSkillSwap { get; set; }

    public decimal TokensPaid { get; set; }

    public int? SkillIdRequester { get; set; }

    public int? SkillIdOfferOwner { get; set; }

    public string? Description { get; set; }

    public int? LastStatusChangedBy { get; set; }

    public string? StatusChangeReason { get; set; }

    public decimal? DigitalTokenExchange { get; set; }

    public virtual TblUser? LastStatusChangedByNavigation { get; set; }

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblUser Requester { get; set; } = null!;

    public virtual TblSkill? SkillIdOfferOwnerNavigation { get; set; }

    public virtual TblSkill? SkillIdRequesterNavigation { get; set; }

    public virtual ICollection<TblExchangeHistory> TblExchangeHistories { get; set; } = new List<TblExchangeHistory>();

    public virtual ICollection<TblReview> TblReviews { get; set; } = new List<TblReview>();

    public virtual ICollection<TblSupportTicket> TblSupportTickets { get; set; } = new List<TblSupportTicket>();
}
