using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOfferFlag
{
    public int OfferFlagId { get; set; }

    public int OfferId { get; set; }

    public int FlaggedByUserId { get; set; }

    public DateTime FlaggedDate { get; set; }

    public string? Reason { get; set; }

    public int? AdminUserId { get; set; }

    public string? AdminAction { get; set; }

    public string? AdminReason { get; set; }

    public DateTime? AdminActionDate { get; set; }

    public virtual TblUser? AdminUser { get; set; }

    public virtual TblUser FlaggedByUser { get; set; } = null!;

    public virtual TblOffer Offer { get; set; } = null!;
}
