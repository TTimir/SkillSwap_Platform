using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class PrivacySensitiveWord
{
    public int Id { get; set; }

    public string Word { get; set; } = null!;
}
