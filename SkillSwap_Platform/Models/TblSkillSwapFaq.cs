using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblSkillSwapFaq
{
    public int FaqId { get; set; }

    public string Section { get; set; } = null!;

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
}
