using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblExchangeHistory
{
    public int HistoryId { get; set; }

    public int ExchangeId { get; set; }

    public int OfferId { get; set; }

    public string ChangedStatus { get; set; } = null!;

    public int? ChangedBy { get; set; }

    public DateTime ChangeDate { get; set; }

    public string? Reason { get; set; }

    public virtual TblUser? ChangedByNavigation { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;
}
