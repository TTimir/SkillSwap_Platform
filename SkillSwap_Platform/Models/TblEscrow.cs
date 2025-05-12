using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblEscrow
{
    public int EscrowId { get; set; }

    public int ExchangeId { get; set; }

    public int BuyerId { get; set; }

    public int SellerId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public DateTime? DisputedAt { get; set; }

    public int? HandledByAdminId { get; set; }

    public string? AdminNotes { get; set; }

    public virtual TblUser Buyer { get; set; } = null!;

    public virtual TblExchange Exchange { get; set; } = null!;

    public virtual TblUser? HandledByAdmin { get; set; }

    public virtual TblUser Seller { get; set; } = null!;
}
