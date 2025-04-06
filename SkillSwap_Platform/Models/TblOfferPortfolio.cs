using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOfferPortfolio
{
    public int PortfolioId { get; set; }

    public int OfferId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual TblOffer Offer { get; set; } = null!;
}
