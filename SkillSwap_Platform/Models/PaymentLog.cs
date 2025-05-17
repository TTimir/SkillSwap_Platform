using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class PaymentLog
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string OrderId { get; set; } = null!;

    public string PaymentId { get; set; } = null!;

    public DateTime ProcessedAt { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
