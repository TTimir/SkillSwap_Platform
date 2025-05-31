namespace SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel
{
    public class OfferDetailDto
    {
        // core
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal TokenCost { get; set; }
        public int TimeCommitmentDays { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsDeleted { get; set; }

        // owner
        public string OwnerUserName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";

        // flags
        public List<OfferFlagDetailDto> Flags { get; set; } = new();

        // chat
        public List<ChatMessageDto> ChatHistory { get; set; } = new();

        // exchanges & history
        public List<ExchangeDetailDto> Exchanges { get; set; } = new();

        public List<ExchangeHistoryDto> ExchangeHistory { get; set; } = new();
        // contracts
        public List<ContractDto> Contracts { get; set; } = new();

        // --- meetings ---
        public List<MeetingDto> Meetings { get; set; } = new();

        // tokens
        public List<TokenTransactionDto> TokenTransactions { get; set; } = new();

        // resource sharing
        public List<ResourceShareDto> ResourceShares { get; set; } = new();


        // ▼ NEW ENGAGEMENT METRICS ▼
        public int ViewCount { get; set; }
        public int BookmarkCount { get; set; }
        public int PendingRequestsCount { get; set; }

        // ▼ NEW CONVERSION & PERFORMANCE ▼
        public double ConversionRate { get; set; }      // e.g. percentage
        public double AverageRating { get; set; }       // post-exchange

        // ▼ NEW DISPUTES & REFUNDS ▼
        public int OpenDisputes { get; set; }
        public decimal RefundedAmount { get; set; }
    }

    public class OfferFlagDetailDto
    {
        public int FlagId { get; set; }
        public string FlaggedByUserName { get; set; } = "";
        public DateTime FlaggedDate { get; set; }
        public string? AdminAction { get; set; }
        public DateTime? AdminActionDate { get; set; }
    }

    public class ChatMessageDto
    {
        public string SenderUserName { get; set; } = "";
        public string MessageType { get; set; } = "";   // e.g. "text", "image", "offer-invite"
        public string Content { get; set; } = "";   // the raw HTML or text blob
        public DateTime SentDate { get; set; }
    }

    public class ExchangeDetailDto
    {
        public int ExchangeId { get; set; }
        public string Mode { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ExchangeDate { get; set; }
        public DateTime LastStatusChangeDate { get; set; }
        public int? SkillIdRequester { get; set; }
        public int? SkillIdOfferOwner { get; set; }
        public string? StatusChangedBy { get; set; }
        public string? StatusChangeReason { get; set; }
        public string? Description { get; set; }
        public decimal TokensPaid { get; set; }
        public bool TokensSettled { get; set; }
        public DateTime? TokenHoldDate { get; set; }
        public DateTime? TokenReleaseDate { get; set; }
    }

    public class ExchangeHistoryDto
    {
        public int HistoryId { get; set; }
        public int ExchangeId { get; set; }
        public string ChangedStatus { get; set; } = "";
        public string ChangedBy { get; set; } = "";
        public DateTime ChangeDate { get; set; }
        public string? Reason { get; set; }
        public string? ActionType { get; set; }
        public DateTime? MeetingVerifiedDate { get; set; }
        public string? VerificationNote { get; set; }
    }

    public class ContractDto
    {
        public int ContractId { get; set; }
        public string ContractUniqueId { get; set; } = "";
        public int SenderUserId { get; set; }
        public int ReceiverUserId { get; set; }
        public DateTime? RequestDate { get; set; }
        public DateTime? AcceptedDate { get; set; }
        public DateTime? DeclinedDate { get; set; }
        public bool SignedBySender { get; set; }
        public bool SignedByReceiver { get; set; }
        public DateTime? SenderAcceptanceDate { get; set; }
        public DateTime? ReceiverAcceptanceDate { get; set; }
        public string FileName { get; set; } = "";
        public string DocumentPath { get; set; } = "";
    }

    public class TokenTransactionDto
    {
        public int TransactionId { get; set; }
        public string Type { get; set; } = "";   // e.g. "Hold","Release","Payment"
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MeetingDto
    {
        public int MeetingId { get; set; }
        public string Status { get; set; } = "";
        public DateTime MeetingStartTime { get; set; }
        public DateTime? MeetingEndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string MeetingLink { get; set; } = "";
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string? MeetingNotes { get; set; }
        public int? MeetingRating { get; set; }

        public string? StartProofImageUrl { get; set; }
        public DateTime? StartProofDateTime { get; set; }
        public string? StartProofLocation { get; set; }

        public string? EndProofImageUrl { get; set; }
        public DateTime? EndProofDateTime { get; set; }
        public string? EndProofLocation { get; set; }

        public string? EndMeetingNotes { get; set; }
    }

    public class ResourceShareDto
    {
        public int ShareId { get; set; }
        public string ShareType { get; set; } = ""; // "Online","In-Person"
        public DateTime OccurredAt { get; set; }
        public string Details { get; set; } = "";
    }
}
