using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class CancellationRequest
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    public DateTime RequestedAt { get; set; }

    public string Reason { get; set; } = null!;

    public virtual Subscription Subscription { get; set; } = null!;
}
