using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblLanguage
{
    public int LanguageId { get; set; }

    public int UserId { get; set; }

    public string Language { get; set; } = null!;

    public string? Proficiency { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
