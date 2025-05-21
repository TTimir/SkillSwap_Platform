using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblSkill
{
    public int SkillId { get; set; }

    public string SkillName { get; set; } = null!;

    public string? SkillCategory { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<TblExchange> TblExchangeSkillIdOfferOwnerNavigations { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblExchange> TblExchangeSkillIdRequesterNavigations { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblUserCertificate> TblUserCertificates { get; set; } = new List<TblUserCertificate>();

    public virtual ICollection<TblUserSkill> TblUserSkills { get; set; } = new List<TblUserSkill>();
}
