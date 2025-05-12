using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOnboardingProgress
{
    public int UserId { get; set; }

    public bool RoleSelected { get; set; }

    public bool ProfileCompleted { get; set; }

    public bool SkillsFilled { get; set; }

    public bool SkillPreferencesSet { get; set; }

    public bool CertificateUploaded { get; set; }

    public bool SocialAndKycCompleted { get; set; }

    public decimal TotalTokensGiven { get; set; }
}
