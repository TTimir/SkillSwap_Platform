using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOfferFaq
{
    public int FaqId { get; set; }

    public int OfferId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public bool IsDeleted { get; set; }

    public virtual TblOffer Offer { get; set; } = null!;
}
