using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblTokenTransaction
{
    public int TransactionId { get; set; }

    public int? ExchangeId { get; set; }

    public int? FromUserId { get; set; }

    public int? ToUserId { get; set; }

    public decimal Amount { get; set; }

    public string TxType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? Description { get; set; }

    public bool IsReleased { get; set; }

    public virtual TblExchange? Exchange { get; set; }

    public virtual TblUser? FromUser { get; set; }

    public virtual TblUser? ToUser { get; set; }
}
