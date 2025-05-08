using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class NewsletterLog
{
    public int NewsletterLogId { get; set; }

    public string Subject { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime SentAtUtc { get; set; }

    public int RecipientCount { get; set; }

    public string SentByAdmin { get; set; } = null!;

    public string RecipientEmail { get; set; } = null!;

    public string? AttachmentNames { get; set; }
}
