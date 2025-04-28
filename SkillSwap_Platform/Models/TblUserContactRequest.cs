using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserContactRequest
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Message { get; set; } = null!;

    public byte[]? AttachmentData { get; set; }

    public string? AttachmentFilename { get; set; }

    public string? AttachmentContentType { get; set; }

    public bool IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool HasSupportContacted { get; set; }

    public DateTime? ContactedAt { get; set; }

    public bool IsResolved { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
