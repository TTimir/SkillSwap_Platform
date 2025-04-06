using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class SensitiveWord
{
    public int Id { get; set; }

    public string Word { get; set; } = null!;

    public string WarningMessage { get; set; } = null!;

    public virtual ICollection<UserSensitiveWord> UserSensitiveWords { get; set; } = new List<UserSensitiveWord>();
}
