using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class UserSensitiveWord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int MessageId { get; set; }

    public int SensitiveWordId { get; set; }

    public DateTime DetectedOn { get; set; }

    public virtual TblMessage Message { get; set; } = null!;

    public virtual SensitiveWord SensitiveWord { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}
