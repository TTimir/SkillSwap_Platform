using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillSwap_Platform.Models;

public partial class Subscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string PlanName { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsAutoRenew { get; set; }

    public string BillingCycle { get; set; } = null!;

    [NotMapped]
    public bool IsActive => EndDate > DateTime.UtcNow;

    public virtual ICollection<CancellationRequest> CancellationRequests { get; set; } = new List<CancellationRequest>();
}
