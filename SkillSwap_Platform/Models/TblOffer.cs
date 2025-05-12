using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblOffer
{
    public int OfferId { get; set; }

    public int UserId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

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

    public bool IsDeleted { get; set; }

    public DateTime? DeletedDate { get; set; }

    public string? Device { get; set; }

    public string? Tools { get; set; }

    public string? ScopeOfWork { get; set; }

    public int? AssistanceRounds { get; set; }

    public bool? ProvidesSourceFiles { get; set; }

    public int? DeliveryTimeDays { get; set; }

    public string? CollaborationMethod { get; set; }

    public string WillingSkill { get; set; } = null!;

    public double? JobSuccessRate { get; set; }

    public double? RecommendedPercentage { get; set; }

    public int Views { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Address { get; set; }

    public bool IsFlagged { get; set; }

    public virtual ICollection<TblContract> TblContracts { get; set; } = new List<TblContract>();

    public virtual ICollection<TblExchange> TblExchanges { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblMessage> TblMessages { get; set; } = new List<TblMessage>();

    public virtual ICollection<TblOfferFaq> TblOfferFaqs { get; set; } = new List<TblOfferFaq>();

    public virtual ICollection<TblOfferFlag> TblOfferFlags { get; set; } = new List<TblOfferFlag>();

    public virtual ICollection<TblOfferPortfolio> TblOfferPortfolios { get; set; } = new List<TblOfferPortfolio>();

    public virtual ICollection<TblReview> TblReviews { get; set; } = new List<TblReview>();

    public virtual ICollection<TblUserWishlist> TblUserWishlists { get; set; } = new List<TblUserWishlist>();

    public virtual TblUser User { get; set; } = null!;
}
