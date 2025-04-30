using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblReview
{
    public int ReviewId { get; set; }

    public int ExchangeId { get; set; }

    public int ReviewerId { get; set; }

    public int RevieweeId { get; set; }

    public double Rating { get; set; }

    public string? Comments { get; set; }

    public DateTime CreatedDate { get; set; }

    public int OfferId { get; set; }

    public int UserId { get; set; }

    public string? ReviewerName { get; set; }

    public string? ReviewerEmail { get; set; }

    public bool RememberMe { get; set; }

    public int HelpfulCount { get; set; }

    public int NotHelpfulCount { get; set; }

    public bool IsFlagged { get; set; }

    public DateTime? FlaggedDate { get; set; }

    public int? FlaggedByUserId { get; set; }

    public virtual TblExchange Exchange { get; set; } = null!;

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblUser Reviewee { get; set; } = null!;

    public virtual TblUser Reviewer { get; set; } = null!;

    public virtual ICollection<TblReviewModerationHistory> TblReviewModerationHistories { get; set; } = new List<TblReviewModerationHistory>();

    public virtual ICollection<TblReviewReply> TblReviewReplies { get; set; } = new List<TblReviewReply>();

    public virtual TblUser User { get; set; } = null!;
}
