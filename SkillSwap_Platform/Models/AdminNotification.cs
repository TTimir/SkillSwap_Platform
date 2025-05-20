using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class AdminNotification
{
    public int Id { get; set; }

    public string ToEmail { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
