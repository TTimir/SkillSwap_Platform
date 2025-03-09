using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblKycUpload
{
    public int KycUploadId { get; set; }

    public int UserId { get; set; }

    public string? DocumentName { get; set; }

    public string? DocumentNumber { get; set; }

    public string? DocumentImageUrl { get; set; }

    public DateTime UploadedDate { get; set; }

    public bool IsVerified { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
