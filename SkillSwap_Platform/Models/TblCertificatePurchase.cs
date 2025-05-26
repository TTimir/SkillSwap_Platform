using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblCertificatePurchase
{
    public int PurchaseId { get; set; }

    public int UserId { get; set; }

    public int ExchangeId { get; set; }

    public DateTime PurchasedAt { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}
