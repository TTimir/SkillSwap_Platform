using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class Tag
{
    public Guid TagId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
