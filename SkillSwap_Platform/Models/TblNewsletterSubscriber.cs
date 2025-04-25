using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblNewsletterSubscriber
{
    public int NewsletterSubscriberId { get; set; }

    public string Email { get; set; } = null!;

    public DateTime SubscribedAtUtc { get; set; }
}
