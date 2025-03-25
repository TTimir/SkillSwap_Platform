using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblMessageAttachment
{
    public int AttachmentId { get; set; }

    public int MessageId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public DateTime UploadedDate { get; set; }

    public long? FileSize { get; set; }

    public virtual TblMessage Message { get; set; } = null!;
}
