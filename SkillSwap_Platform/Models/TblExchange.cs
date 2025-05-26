using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblExchange
{
    public int ExchangeId { get; set; }

    public int OfferId { get; set; }

    public DateTime ExchangeDate { get; set; }

    public DateTime LastStatusChangeDate { get; set; }

    public string Status { get; set; } = null!;

    public string ExchangeMode { get; set; } = null!;

    public bool IsSkillSwap { get; set; }

    public decimal TokensPaid { get; set; }

    public int? SkillIdRequester { get; set; }

    public int? SkillIdOfferOwner { get; set; }

    public string? Description { get; set; }

    public int? LastStatusChangedBy { get; set; }

    public string? StatusChangeReason { get; set; }

    public decimal? DigitalTokenExchange { get; set; }

    public bool IsSuccessful { get; set; }

    public int? OfferOwnerId { get; set; }

    public int? OtherUserId { get; set; }

    public bool IsInOnlineExchange { get; set; }

    public bool IsInPersonExchange { get; set; }

    public string? ThisMeetingLink { get; set; }

    public bool IsInpersonMeetingVerified { get; set; }

    public bool IsCompletedByOfferOwner { get; set; }

    public bool IsCompletedByOtherParty { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletionDate { get; set; }

    public DateTime? RequestDate { get; set; }

    public DateTime? ResponseDate { get; set; }

    public bool IsInpersonMeetingVerifiedByOfferOwner { get; set; }

    public bool IsInpersonMeetingVerifiedByOtherParty { get; set; }

    public bool IsMeetingEnded { get; set; }

    public bool TokensSettled { get; set; }

    public int? ContractId { get; set; }

    public bool TokensHeld { get; set; }

    public DateTime? TokenHoldDate { get; set; }

    public DateTime? TokenReleaseDate { get; set; }

    public virtual TblContract? Contract { get; set; }

    public virtual TblUser? LastStatusChangedByNavigation { get; set; }

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblSkill? SkillIdOfferOwnerNavigation { get; set; }

    public virtual TblSkill? SkillIdRequesterNavigation { get; set; }

    public virtual ICollection<TblCertificatePurchase> TblCertificatePurchases { get; set; } = new List<TblCertificatePurchase>();

    public virtual ICollection<TblEscrow> TblEscrows { get; set; } = new List<TblEscrow>();

    public virtual ICollection<TblExchangeHistory> TblExchangeHistories { get; set; } = new List<TblExchangeHistory>();

    public virtual ICollection<TblInPersonMeeting> TblInPersonMeetings { get; set; } = new List<TblInPersonMeeting>();

    public virtual ICollection<TblReview> TblReviews { get; set; } = new List<TblReview>();

    public virtual ICollection<TblSupportTicket> TblSupportTickets { get; set; } = new List<TblSupportTicket>();

    public virtual ICollection<TblTokenTransaction> TblTokenTransactions { get; set; } = new List<TblTokenTransaction>();

    public virtual ICollection<TblUserReport> TblUserReports { get; set; } = new List<TblUserReport>();
}
