using SkillSwap_Platform.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillSwap_Platform.Models;

public partial class TblMessage
{
    public int MessageId { get; set; }

    public int SenderUserId { get; set; }

    public int ReceiverUserId { get; set; }

    public string Content { get; set; } = null!;

    public string? MeetingLink { get; set; }

    public DateTime SentDate { get; set; }

    public bool IsRead { get; set; }

    public string? ReplyPreview { get; set; }

    public int? ReplyToMessageId { get; set; }

    public bool IsFlagged { get; set; }

    public bool IsApproved { get; set; }

    public int? ApprovedByAdminId { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? OfferId { get; set; }

    public string MessageType { get; set; } = null!;

    public int? ResourceId { get; set; }

    public int? ExchangeId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedOn { get; set; }

    public int? DeletedByUserId { get; set; }

    [NotMapped]
    public OfferDisplayVM OfferPreview { get; set; }
    public virtual TblUser? ApprovedByAdmin { get; set; }

    public virtual TblOffer? Offer { get; set; }

    public virtual TblUser ReceiverUser { get; set; } = null!;

    public virtual TblUser SenderUser { get; set; } = null!;

    public virtual ICollection<TblContract> TblContracts { get; set; } = new List<TblContract>();

    public virtual ICollection<TblMessageAttachment> TblMessageAttachments { get; set; } = new List<TblMessageAttachment>();

    public virtual ICollection<UserSensitiveWord> UserSensitiveWords { get; set; } = new List<UserSensitiveWord>();
}
