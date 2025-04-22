using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblReviewReply
{
    public int ReplyId { get; set; }

    public int ReviewId { get; set; }

    public int ReplierUserId { get; set; }

    public string Comments { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? FlaggedDate { get; set; }

    public bool IsFlagged { get; set; }

    public int? FlaggedByUserId { get; set; }

    public virtual TblUser ReplierUser { get; set; } = null!;

    public virtual TblReview Review { get; set; } = null!;
}
