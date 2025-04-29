using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblNotification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? Url { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
}
