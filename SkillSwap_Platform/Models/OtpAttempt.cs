using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class OtpAttempt
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public DateTime AttemptedAt { get; set; }

    public bool WasSuccessful { get; set; }

    public string Method { get; set; } = null!;

    public string IpAddress { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}
