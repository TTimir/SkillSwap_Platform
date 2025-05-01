using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblReviewModerationHistory
{
    public int HistoryId { get; set; }

    public int? FlagId { get; set; }

    public int ReviewId { get; set; }

    public int? ReplyId { get; set; }

    public int AdminId { get; set; }

    public string Action { get; set; } = null!;

    public string Notes { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual TblUser Admin { get; set; } = null!;

    public virtual TblReviewReply? Reply { get; set; }

    public virtual TblReview Review { get; set; } = null!;
}
