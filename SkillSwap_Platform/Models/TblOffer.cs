using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOffer
{
    public int OfferId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal TokenCost { get; set; }

    public int TimeCommitmentDays { get; set; }

    public DateTime CreatedDate { get; set; }

    public bool IsActive { get; set; }

    public string? Category { get; set; }

    public decimal? DigitalTokenValue { get; set; }

    public string? Portfolio { get; set; }

    public string? SkillIdOfferOwner { get; set; }

    public string? FreelanceType { get; set; }

    public string? RequiredSkillLevel { get; set; }

    public int? RequiredLanguageId { get; set; }

    public string? RequiredLanguageLevel { get; set; }

    public virtual ICollection<TblExchange> TblExchanges { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblOfferPortfolio> TblOfferPortfolios { get; set; } = new List<TblOfferPortfolio>();

    public virtual TblUser User { get; set; } = null!;
}
