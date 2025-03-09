using System;
using System.Collections.Generic;

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

    public virtual TblUser ReceiverUser { get; set; } = null!;

    public virtual TblUser SenderUser { get; set; } = null!;

    public virtual ICollection<TblMessageAttachment> TblMessageAttachments { get; set; } = new List<TblMessageAttachment>();
}
