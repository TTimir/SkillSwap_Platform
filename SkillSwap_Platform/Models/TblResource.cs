using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblResource
{
    public int ResourceId { get; set; }

    public int OwnerUserId { get; set; }

    public int? ExchangeId { get; set; }

    public int? OfferId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string FilePath { get; set; } = null!;

    public string ResourceType { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
