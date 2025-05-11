namespace SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel
{
    public class UserDetailDto
    {
        // 1) Basic profile
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsVerified { get; set; }
        public bool IsHeld { get; set; }
        public DateTime? HeldAt { get; set; }
        public int FailedOtpAttempts { get; set; }

        // 2) Flags summary
        public int TotalUserFlags { get; set; }
        public DateTime? LastUserFlagDate { get; set; }
        public int TotalReviewFlags { get; set; }
        public DateTime? LastReviewFlagDate { get; set; }
        public int TotalReplyFlags { get; set; }
        public DateTime? LastReplyFlagDate { get; set; }

        // 3) Registration/onboarding
        public DateTime RegistrationDate { get; set; }


        // 4) Recent exchanges
        public List<UserExchangeDto> Exchanges { get; set; } = new();

        // 5) Reviews and replies
        public List<ReviewDto> Reviews { get; set; } = new();

        // 6) Offers they’ve created
        public List<UserOfferDto> OffersCreated { get; set; } = new();

        // 7) Certificates pending/approved
        public List<CertificateDto> Certificates { get; set; } = new();

        // 8) Token transactions
        public List<TokenTransactionDto> TokenTransactions { get; set; } = new();

        // 9) Skills they possess
        public List<UserSkillDto> Skills { get; set; } = new();

        // ▼ NEW ACTIVITY OVERVIEW ▼
        public int TotalOffersCreated { get; set; }
        public int TotalExchangesInitiated { get; set; }
        public int TotalExchangesReceived { get; set; }

        public string? LastLoginIp { get; set; }
        public string? LastLoginGeo { get; set; }

        // ▼ NEW REPUTATION & RATINGS ▼
        public double AverageRatingReceived { get; set; }

        // ▼ NEW SECURITY & COMPLIANCE ▼
        public List<DateTime> OtpAttemptDates { get; set; } = new();
        public bool IsLockedOut { get; set; }

        // ▼ NEW FINANCIAL SUMMARY ▼
        public decimal TokensEarned { get; set; }
        public decimal TokensSpent { get; set; }
        public decimal CurrentTokenBalance { get; set; }

        // ▼ NEW PENDING ITEMS ▼
        public int PendingFlagReviewsCount { get; set; }
        public int PendingCertificatesCount { get; set; }

        public List<FlaggedMessageDto> FlaggedMessages { get; set; } = new();

        public List<UserOfferFlagDto> OfferFlags { get; set; } = new();
        public List<EducationDto> Education { get; set; }
        public List<UserExperienceDto> Experiences { get; set; } = new();
        public List<LanguageDto> Languages { get; set; }
        public List<KycDto> KycDetails { get; set; }
    }

    // define each sub-DTO similarly:
    public class UserExchangeDto { public int ExchangeId; public string OfferTitle; public string Mode; public DateTime? RequestDate; public DateTime? CompletionDate; public string Status; public decimal TokensPaid; public DateTime? LastStatusChangeDate; public string LastStatusChangeBy; public string SkillRequester; public string SkillOwner; public bool TokensSettled; public DateTime? TokenHoldDate; public DateTime? TokenReleaseDate; }
    public class ReviewDto { public int ReviewId; public string ReviewerName; public double Rating; public string Comments; public DateTime CreatedDate; public bool IsFlagged; public DateTime? FlaggedDate; }
    public class CertificateDto { public int CertificateId; public DateTime RequestedDate; public DateTime? ApprovedDate; public string SkillName; }
    public class TokenTransactionsDto { public int TransactionId; public string Type; public decimal Amount; public DateTime CreatedAt; }
    public class UserOfferDto
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public decimal TokenCost { get; set; }
    }

    public class UserSkillDto
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; } = "";
        public string SkillCategory { get; set; }
        public int? Proficiency { get; set; }
    }

    public class FlaggedMessageDto
    {
        public int MessageId { get; set; }
        public string SenderUserName { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime SentDate { get; set; }

        // ▼ NEW ▼
        public bool IsApproved { get; set; }
        public string? ApprovedByAdminName { get; set; }
        public DateTime? ApprovedDate { get; set; }
    }
    public class UserOfferFlagDto
    {
        public int FlagId { get; set; }
        public int OfferId { get; set; }
        public string OfferTitle { get; set; } = "";
        public DateTime FlaggedDate { get; set; }
        public string? AdminAction { get; set; }
        public DateTime? AdminActionDate { get; set; }
    }

    public class EducationDto
    {
        public string InstitutionName { get; set; }
        public string Degree { get; set; }
        public string Description { get; set; }
        public string University { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class UserExperienceDto
    {
        public int ExperienceId { get; set; }
        public string Company { get; set; }
        public string Position { get; set; }
        public string Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class LanguageDto
    {
        public string Language { get; set; }
        public string Proficiency { get; set; }
    }

    public class KycDto
    {
        public string DocumentName { get; set; }
        public string DocumentNumber { get; set; }
        public string ImageUrl { get; set; }
        public DateTime UploadedDate { get; set; }
        public bool IsVerified { get; set; }
    }
}