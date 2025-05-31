using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;
using System.Linq;
using System.Security.Policy;

namespace SkillSwap_Platform.Services.AdminControls.AdminSearch
{
    public class AdminSearchService : IAdminSearchService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<AdminSearchService> _logger;

        public AdminSearchService(SkillSwapDbContext db, ILogger<AdminSearchService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PagedResult<OfferSearchResultDto>> SearchOffersAsync(SearchCriteria criteria)
        {
            try
            {
                // base query: join Offers → Users, filter by title/category
                var baseQuery = from o in _db.TblOffers.AsNoTracking()
                                join u in _db.TblUsers.AsNoTracking()
                                  on o.UserId equals u.UserId
                                where string.IsNullOrWhiteSpace(criteria.Term)
                                   || EF.Functions.Like(o.Title, $"%{criteria.Term}%")
                                   || EF.Functions.Like(o.Category, $"%{criteria.Term}%")
                                select new
                                {
                                    o.OfferId,
                                    o.Title,
                                    o.Category,
                                    o.CreatedDate,
                                    OwnerUserName = u.UserName
                                };

                var total = await baseQuery.CountAsync();

                var pageData = await baseQuery
                    .OrderByDescending(x => x.CreatedDate)
                    .Skip((criteria.Page - 1) * criteria.PageSize)
                    .Take(criteria.PageSize)
                    .ToListAsync();

                // now shape into DTOs, counting flags per offer
                var items = pageData
                    .Select(x => new OfferSearchResultDto
                    {
                        OfferId = x.OfferId,
                        Title = x.Title,
                        Category = x.Category,
                        CreatedDate = x.CreatedDate,
                        OwnerUserName = x.OwnerUserName,
                        TotalFlags = _db.TblOfferFlags
                                           .Count(f => f.OfferId == x.OfferId)
                    })
                    .ToList();

                return new PagedResult<OfferSearchResultDto>
                {
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                    TotalCount = total,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching offers with term '{Term}'", criteria.Term);
                throw;
            }
        }

        public async Task<OfferDetailDto> GetOfferDetailAsync(int offerId, int currentUserId)
        {
            try
            {
                // 1) Basic offer + owner
                var dto = await _db.TblOffers.AsNoTracking()
                    .Where(o => o.OfferId == offerId)
                    .Select(o => new OfferDetailDto
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        Category = o.Category,
                        TokenCost = o.TokenCost,
                        CreatedDate = o.CreatedDate,
                        IsDeleted = o.IsDeleted,
                        OwnerUserName = o.User.UserName,
                        OwnerEmail = o.User.Email
                    })
                    .FirstOrDefaultAsync();

                if (dto == null)
                    throw new InvalidOperationException($"Offer {offerId} not found.");

                // 2) Flags
                dto.Flags = await _db.TblOfferFlags.AsNoTracking()
                    .Where(f => f.OfferId == offerId)
                    .Select(f => new OfferFlagDetailDto
                    {
                        FlagId = f.OfferFlagId,
                        FlaggedByUserName = f.FlaggedByUser.UserName,
                        FlaggedDate = f.FlaggedDate,
                        AdminAction = f.AdminAction,
                        AdminActionDate = f.AdminActionDate
                    })
                    .ToListAsync();

                // 3) Exchanges
                dto.Exchanges = await _db.TblExchanges.AsNoTracking()
                    .Where(x => x.OfferId == offerId)
                    .Select(x => new ExchangeDetailDto
                    {
                        ExchangeId = x.ExchangeId,
                        Mode = x.ExchangeMode,
                        Status = x.Status,
                        ExchangeDate = x.ExchangeDate,
                        LastStatusChangeDate = x.LastStatusChangeDate,
                        SkillIdRequester = x.SkillIdRequester,
                        SkillIdOfferOwner = x.SkillIdOfferOwner,
                        StatusChangedBy = x.LastStatusChangedByNavigation.UserName,
                        StatusChangeReason = x.StatusChangeReason,
                        Description = x.Description,
                        TokensPaid = x.TokensPaid,
                        TokensSettled = x.TokensSettled,
                        TokenHoldDate = x.TokenHoldDate,
                        TokenReleaseDate = x.TokenReleaseDate
                    })
                    .ToListAsync();

                // 4) Exchange history
                dto.ExchangeHistory = await _db.TblExchangeHistories.AsNoTracking()
                    .Where(h => h.OfferId == offerId)
                    .Select(h => new ExchangeHistoryDto
                    {
                        HistoryId = h.HistoryId,
                        ExchangeId = h.ExchangeId,
                        ChangedStatus = h.ChangedStatus,
                        ChangedBy = h.ChangedByNavigation.UserName,
                        ChangeDate = h.ChangeDate,
                        Reason = h.Reason,
                        ActionType = h.ActionType,
                        MeetingVerifiedDate = h.MeetingVerifiedDate,
                        VerificationNote = h.VerificationNote
                    })
                    .ToListAsync();

                // 5) Chat messages
                dto.ChatHistory = await _db.TblMessages.AsNoTracking()
                    .Where(m => m.OfferId == offerId)
                    .OrderBy(m => m.SentDate)
                    .Select(m => new ChatMessageDto
                    {
                        SenderUserName = m.SenderUser.UserName,
                        MessageType = m.MessageType,
                        Content = m.Content,
                        SentDate = m.SentDate
                    })
                    .ToListAsync();

                // 6) Contracts
                dto.Contracts = await _db.TblContracts.AsNoTracking()
                    .Where(c => c.OfferId == offerId)
                    .Select(c => new ContractDto
                    {
                        ContractId = c.ContractId,
                        ContractUniqueId = c.ContractUniqueId,
                        SenderUserId = c.SenderUserId,
                        ReceiverUserId = c.ReceiverUserId,
                        RequestDate = c.RequestDate,
                        AcceptedDate = c.AcceptedDate,
                        DeclinedDate = c.DeclinedDate,
                        SignedBySender = c.SignedBySender,
                        SignedByReceiver = c.SignedByReceiver,
                        SenderAcceptanceDate = c.SenderAcceptanceDate,
                        ReceiverAcceptanceDate = c.ReceiverAcceptanceDate,
                        FileName = Path.GetFileName(c.ContractDocument),
                        DocumentPath = c.ContractDocument
                    })
                    .ToListAsync();

                // 7) Meetings (online + in-person), but only those by the current user
                var onlineMeetings = await _db.TblMeetings.AsNoTracking()
                    .Where(m => m.OfferId == offerId)
                    .Select(m => new MeetingDto
                    {
                        MeetingId = m.MeetingId,
                        Status = m.Status,
                        MeetingStartTime = m.MeetingStartTime,
                        MeetingEndTime = m.MeetingEndTime,
                        DurationMinutes = m.DurationMinutes,
                        MeetingLink = m.MeetingLink,
                        ActualStartTime = m.ActualStartTime,
                        ActualEndTime = m.ActualEndTime,
                        MeetingNotes = m.MeetingNotes,
                        MeetingRating = m.MeetingRating,

                        StartProofImageUrl = null,
                        StartProofDateTime = null,
                        StartProofLocation = null,

                        EndProofImageUrl = null,
                        EndProofDateTime = null,
                        EndProofLocation = null,

                        EndMeetingNotes = null,
                    })
                    .ToListAsync();

                var inPersonMeetings = await (
            from ip in _db.TblInPersonMeetings.AsNoTracking()
            join proof in _db.TblInpersonMeetingProofs.AsNoTracking()
                on ip.ExchangeId equals proof.ExchangeId into proofGroup
            from pr in proofGroup.DefaultIfEmpty()    // LEFT JOIN

            where ip.Exchange.OfferId == offerId

            select new MeetingDto
            {
                MeetingId = ip.InPersonMeetingId,
                MeetingStartTime = ip.MeetingScheduledDateTime ?? default(DateTime),
                MeetingEndTime = (ip.MeetingScheduledDateTime ?? DateTime.MinValue)
                                            .AddMinutes((double)ip.InpersonMeetingDurationMinutes),
                DurationMinutes = ip.InpersonMeetingDurationMinutes.Value,
                MeetingLink = "—",  // not applicable
                MeetingNotes = ip.MeetingNotes,

                StartProofImageUrl = pr != null ? pr.StartProofImageUrl : null,
                StartProofDateTime = pr != null ? pr.StartProofDateTime : null,
                StartProofLocation = pr != null ? pr.StartProofLocation : null,

                EndProofImageUrl = pr != null ? pr.EndProofImageUrl : null,
                EndProofDateTime = pr != null ? pr.EndProofDateTime : null,
                EndProofLocation = pr != null ? pr.EndProofLocation : null,

                EndMeetingNotes = pr != null ? pr.EndMeetingNotes : null,
            })
                    .ToListAsync();

                dto.Meetings = onlineMeetings
                    .Concat(inPersonMeetings)
                    .OrderBy(m => m.MeetingStartTime)
                    .ToList();

                // 8) Resource shares (other types, if applicable)
                dto.ResourceShares = await _db.TblResources.AsNoTracking()
                    .Where(r => r.OfferId == offerId && r.OwnerUserId == currentUserId)
                    .Select(r => new ResourceShareDto
                    {
                        ShareId = r.ResourceId,
                        ShareType = r.ResourceType,
                        OccurredAt = r.CreatedDate,
                        Details = r.Description
                    })
                    .ToListAsync();

                // 8) Token transactions for this offer’s exchanges
                dto.TokenTransactions = await (
                    from tx in _db.TblTokenTransactions.AsNoTracking()
                    join ex in _db.TblExchanges.AsNoTracking()
                      on tx.ExchangeId equals ex.ExchangeId
                    where ex.OfferId == offerId
                    orderby tx.CreatedAt
                    select new TokenTransactionDto
                    {
                        TransactionId = tx.TransactionId,
                        Type = tx.TxType,
                        Amount = tx.Amount,
                        CreatedAt = tx.CreatedAt
                    }
                ).ToListAsync();



                // — ENGAGEMENT METRICS —
                dto.ViewCount = await _db.TblOffers
                    .Where(o => o.OfferId == offerId)
                    .Select(o => o.Views)            // assumes you have a ViewCount column on TblOffers
                    .FirstOrDefaultAsync();

                dto.BookmarkCount = await _db.TblUserWishlists
                                             .CountAsync(b => b.OfferId == offerId);

                // both pending exchange *and* contract requests
                var pendingExch = await _db.TblExchanges
                                           .CountAsync(x => x.OfferId == offerId && x.Status == "Pending");
                var pendingContract = await _db.TblContracts
                                               .CountAsync(c => c.OfferId == offerId
                                                              && c.AcceptedDate == null
                                                              && c.DeclinedDate == null);
                dto.PendingRequestsCount = pendingExch + pendingContract;

                // — CONVERSION & PERFORMANCE —
                var completedExch = await _db.TblExchanges
                                             .CountAsync(x => x.OfferId == offerId && x.Status == "Completed");

                var denominator = Math.Max(dto.ViewCount, dto.PendingRequestsCount);
                dto.ConversionRate = denominator > 0
                    ? (double)completedExch / denominator * 100
                    : 0;

                dto.AverageRating = await _db.TblReviews
                                            .Where(r => r.OfferId == offerId)       // just filter by offer
                                            .Select(r => (double?)r.Rating)         // cast to nullable
                                            .AverageAsync()                         // yields double? or null
                                            ?? 0.0;

                // — DISPUTES & REFUNDS —
                dto.OpenDisputes = await _db.TblExchanges
                                            .CountAsync(x => x.OfferId == offerId
                                                          && (x.Status == "Dispute" || x.Status == "RefundRequested"));

                dto.RefundedAmount = await _db.TblTokenTransactions
                                              .Where(tx => tx.TxType == "Refund"
                                                        && tx.Exchange.OfferId == offerId)
                                              .SumAsync(tx => tx.Amount);

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading details for offer {OfferId}", offerId);
                throw;
            }
        }

        public async Task<PagedResult<UserSearchResultDto>> SearchUsersAsync(SearchCriteria criteria)
        {
            try
            {
                // base query: filter users by name/email
                var baseQuery = from u in _db.TblUsers.AsNoTracking()
                                where string.IsNullOrWhiteSpace(criteria.Term)
                                   || EF.Functions.Like(u.UserName, $"%{criteria.Term}%")
                                   || EF.Functions.Like(u.Email, $"%{criteria.Term}%")
                                select new
                                {
                                    u.UserId,
                                    u.UserName,
                                    u.Email,
                                    u.CreatedDate,
                                    u.IsHeld,
                                    u.FailedOtpAttempts
                                };

                var total = await baseQuery.CountAsync();

                var pageData = await baseQuery
                    .OrderByDescending(x => x.CreatedDate)
                    .Skip((criteria.Page - 1) * criteria.PageSize)
                    .Take(criteria.PageSize)
                    .ToListAsync();

                // shape into DTOs, counting flags per user
                var items = pageData
                    .Select(x => new UserSearchResultDto
                    {
                        UserId = x.UserId,
                        UserName = x.UserName,
                        Email = x.Email,
                        CreatedDate = x.CreatedDate,
                        IsHeld = x.IsHeld,
                        FailedOtpAttempts = x.FailedOtpAttempts ?? 0,
                        TotalFlags = _db.TblUserFlags
                                                 .Count(f => f.FlaggedUserId == x.UserId)
                    })
                    .ToList();

                return new PagedResult<UserSearchResultDto>
                {
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                    TotalCount = total,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term '{Term}'", criteria.Term);
                throw;
            }
        }

        public async Task<UserDetailDto> GetUserDetailAsync(int userId)
        {
            // 1) Basic profile + flags summary
            var u = await _db.TblUsers.AsNoTracking()
                 .Where(x => x.UserId == userId)
                 .Select(x => new
                 {
                     x.UserId,
                     x.UserName,
                     x.Designation,
                     x.Email,
                     x.CreatedDate,
                     x.LastActive,
                     x.IsActive,
                     x.IsVerified,
                     x.IsHeld,
                     x.HeldAt,
                     x.HeldReason,
                     x.ReleasedAt,
                     x.ReleaseReason,
                     x.FailedOtpAttempts,
                     x.DigitalTokenBalance
                 }).FirstOrDefaultAsync();

            if (u == null) throw new InvalidOperationException($"User {userId} not found.");

            var dto = new UserDetailDto
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                Designation = u.Designation,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastActive,
                IsActive = u.IsActive,
                IsVerified = u.IsVerified,
                IsHeld = u.IsHeld,
                HeldAt = u.HeldAt,
                HeldReason = u.HeldReason,
                ReleaseAt = u.ReleasedAt,
                ReleaseReason = u.ReleaseReason,
                FailedOtpAttempts = u.FailedOtpAttempts ?? 0,
                IsLockedOut = u.IsHeld,
                RegistrationDate = u.CreatedDate
            };

            // 2) Flags summary
            dto.TotalUserFlags = await _db.TblUserFlags.CountAsync(f => f.FlaggedUserId == userId);
            dto.LastUserFlagDate = await _db.TblUserFlags
                .Where(f => f.FlaggedUserId == userId).MaxAsync(f => (DateTime?)f.FlaggedDate);
            dto.TotalReviewFlags = await _db.TblReviews.CountAsync(r => r.RevieweeId == userId && r.IsFlagged);
            dto.LastReviewFlagDate = await _db.TblReviews
                .Where(r => r.RevieweeId == userId && r.IsFlagged).MaxAsync(r => (DateTime?)r.FlaggedDate);
            dto.TotalReplyFlags = await _db.TblReviewReplies.CountAsync(rr => rr.ReplierUserId == userId && rr.IsFlagged);
            dto.LastReplyFlagDate = await _db.TblReviewReplies
                .Where(rr => rr.ReplierUserId == userId && rr.IsFlagged).MaxAsync(rr => (DateTime?)rr.FlaggedDate);

            // 3b) Registration date
            dto.RegistrationDate = u.CreatedDate;

            var skillMap = await _db.TblSkills.ToDictionaryAsync(s => s.SkillId, s => s.SkillName);

            // Step 1: Get exchange data into memory
            var rawExchanges = await _db.TblExchanges.AsNoTracking()
                .Where(x => x.OfferOwnerId == userId || x.OtherUserId == userId)
                .OrderByDescending(x => x.ExchangeDate)
                .Take(20)
                .Select(x => new
                {
                    x.ExchangeId,
                    x.Offer.Title,
                    x.ExchangeMode,
                    x.Description,
                    x.Status,
                    x.LastStatusChangeDate,
                    x.LastStatusChangedBy,
                    x.RequestDate,
                    x.CompletionDate,
                    x.SkillIdRequester,
                    x.SkillIdOfferOwner,
                    x.TokensPaid,
                    x.TokensSettled,
                    x.TokenHoldDate,
                    x.TokenReleaseDate
                })
                .ToListAsync(); // materialize into memory

            var userMap = await _db.TblUsers
                .Select(u => new { u.UserId, u.UserName })
                .ToDictionaryAsync(u => u.UserId, u => u.UserName);

            // Step 2: Now project with C# + dictionary logic
            dto.Exchanges = rawExchanges.Select(x => new UserExchangeDto
            {
                ExchangeId = x.ExchangeId,
                OfferTitle = x.Title,
                Mode = x.ExchangeMode,
                Status = x.Status,
                LastStatusChangeDate = x.LastStatusChangeDate,
                LastStatusChangeBy = userMap.TryGetValue(x.LastStatusChangedBy ?? 0, out var changer) ? changer : "—",
                RequestDate = x.RequestDate,
                CompletionDate = x.CompletionDate,
                SkillRequester = skillMap.TryGetValue(x.SkillIdRequester ?? 0, out var skillReq) ? skillReq : "—",
                SkillOwner = skillMap.TryGetValue(x.SkillIdOfferOwner ?? 0, out var skillOwn) ? skillOwn : "—",
                TokensPaid = x.TokensPaid,
                TokensSettled = x.TokensSettled,
                TokenHoldDate = x.TokenHoldDate,
                TokenReleaseDate = x.TokenReleaseDate
            }).ToList();

            dto.Education = await _db.TblEducations.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => new EducationDto
                {
                    InstitutionName = e.InstitutionName,
                    Degree = e.DegreeName,
                    Description = e.Description,
                    University = e.UniversityName,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate
                }).ToListAsync();

            dto.Experiences = await _db.TblExperiences.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => new UserExperienceDto
                {
                    ExperienceId = e.ExperienceId,
                    Company = e.CompanyName,
                    Position = e.Position,
                    Description = e.Description,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate
                }).ToListAsync();

            dto.Languages = await _db.TblLanguages.AsNoTracking()
                .Where(l => l.UserId == userId)
                .Select(l => new LanguageDto
                {
                    Language = l.Language,
                    Proficiency = l.Proficiency
                }).ToListAsync();

            dto.KycDetails = await _db.TblKycUploads.AsNoTracking()
                .Where(k => k.UserId == userId)
                .Select(k => new KycDto
                {
                    DocumentName = k.DocumentName,
                    DocumentNumber = k.DocumentNumber,
                    ImageUrl = k.DocumentImageUrl,
                    UploadedDate = k.UploadedDate,
                    IsVerified = k.IsVerified
                }).ToListAsync();

            // 5) Reviews & replies
            dto.Reviews = await _db.TblReviews.AsNoTracking()
                .Where(r => r.RevieweeId == userId)
                .OrderByDescending(r => r.CreatedDate)
                .Take(20)
                .Select(r => new ReviewDto
                {
                    ReviewId = r.ReviewId,
                    ReviewerName = r.ReviewerName,
                    Rating = r.Rating,
                    Comments = r.Comments,
                    CreatedDate = r.CreatedDate,
                    IsFlagged = r.IsFlagged,
                    FlaggedDate = r.FlaggedDate
                })
                .ToListAsync();

            // 6b) Offers they’ve created
            dto.OffersCreated = await _db.TblOffers.AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedDate)
                .Select(o => new UserOfferDto
                {
                    OfferId = o.OfferId,
                    Title = o.Title,
                    Category = o.Category,
                    CreatedDate = o.CreatedDate,
                    TokenCost = o.TokenCost
                })
                .ToListAsync();

            // 7) Certificates
            dto.Certificates = await _db.TblUserCertificates.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.SubmittedDate).Take(20)
                .Select(c => new CertificateDto
                {
                    CertificateId = c.CertificateId,
                    RequestedDate = c.SubmittedDate,
                    ApprovedDate = c.ApprovedDate,
                    SkillName = c.Skill.SkillName
                }).ToListAsync();

            // 8) Token transactions
            dto.TokenTransactions = await _db.TblTokenTransactions
                .AsNoTracking()
                .Where(t =>
                    // always include anything you sent or received:
                    t.FromUserId == userId
                 || t.ToUserId == userId

                 // plus: include *all* Release txns for exchanges you own or requested
                 || (t.TxType == "Release"
                     && t.ExchangeId != null
                     && _db.TblExchanges.Any(ex =>
                           ex.ExchangeId == t.ExchangeId
                        && (ex.OfferOwnerId == userId
                         || ex.OtherUserId == userId)
                     ))
                )
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new TokenTransactionDto
                {
                    TransactionId = t.TransactionId,
                    Type = t.TxType,
                    Amount = t.Amount,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            // 9b) Skills
            dto.Skills = await (
                from us in _db.TblUserSkills.AsNoTracking()
                join s in _db.TblSkills.AsNoTracking()
                  on us.SkillId equals s.SkillId
                where us.UserId == userId
                select new UserSkillDto
                {
                    SkillId = s.SkillId,
                    SkillName = s.SkillName,
                    SkillCategory = us.Skill.SkillCategory,
                    Proficiency = us.ProficiencyLevel
                })
                .ToListAsync();

            dto.Badges = await _db.TblBadgeAwards
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Include(a => a.Badge)
                .Select(a => new BadgeDto
                {
                    IconUrl = a.Badge.IconUrl,
                    Name = a.Badge.Name,
                    Description = a.Badge.Description,
                    LevelName =
                        a.Badge.Tier == "1" ? "Common" :
                        a.Badge.Tier == "2" ? "Uncommon" :
                        a.Badge.Tier == "3" ? "Rare" :
                        a.Badge.Tier == "4" ? "Epic" :
                        a.Badge.Tier == "5" ? "Legendary" :
                        a.Badge.Tier == "6" ? "Mythic" :
                        "Level n/a"
                })
                .ToListAsync();

            // — FLAGGED MESSAGES — 
            dto.FlaggedMessages = await _db.TblMessages.AsNoTracking()
                .Where(m =>
                    (m.SenderUserId == userId || m.ReceiverUserId == userId)
                    && m.IsFlagged
                )
                .OrderByDescending(m => m.SentDate)
                .Select(m => new FlaggedMessageDto
                {
                    MessageId = m.MessageId,
                    SenderUserName = m.SenderUser.UserName,
                    Content = m.Content,
                    SentDate = m.SentDate,
                    IsApproved = m.IsApproved,                 // assumes you have this
                    ApprovedByAdminName = m.ApprovedByAdmin.UserName,    // navigation property
                    ApprovedDate = m.ApprovedDate
                })
                .ToListAsync();

            // — OFFER FLAGS HISTORY —
            dto.OfferFlags = await _db.TblOfferFlags.AsNoTracking()
                .Where(f => f.FlaggedByUserId == userId)
                .OrderByDescending(f => f.FlaggedDate)
                .Select(f => new UserOfferFlagDto
                {
                    FlagId = f.OfferFlagId,
                    OfferId = f.OfferId,
                    OfferTitle = f.Offer.Title,
                    FlaggedDate = f.FlaggedDate,
                    AdminAction = f.AdminAction,
                    AdminActionDate = f.AdminActionDate
                })
                .ToListAsync();

            // — ACTIVITY OVERVIEW —
            dto.TotalOffersCreated = await _db.TblOffers.CountAsync(o => o.UserId == userId);
            dto.TotalExchangesInitiated = await _db.TblExchanges.CountAsync(x => x.OtherUserId == userId);
            dto.TotalExchangesReceived = await _db.TblExchanges.CountAsync(x => x.OfferOwnerId == userId);

            // — REPUTATION & RATINGS —
            dto.AverageRatingReceived = await _db.TblReviews
                                            .Where(r => r.RevieweeId == userId)
                                            .Select(r => (double?)r.Rating)
                                            .AverageAsync() ?? 0.0;

            // — SECURITY & COMPLIANCE —
            dto.OtpAttemptDates = await _db.OtpAttempts
                                            .Where(a => a.UserId == userId)
                                            .Select(a => a.AttemptedAt)
                                            .ToListAsync();

            // — FINANCIAL SUMMARY —
            dto.TokensEarned = await _db.TblTokenTransactions
                                                  .Where(t => t.ToUserId == userId)
                                                  .SumAsync(t => t.Amount);
            dto.TokensSpent = await _db.TblTokenTransactions
                                                  .Where(t => t.FromUserId == userId)
                                                  .SumAsync(t => t.Amount);
            dto.CurrentTokenBalance = u.DigitalTokenBalance;

            // — PENDING ITEMS —
            dto.PendingFlagReviewsCount = await _db.TblUserFlags
                                                  .CountAsync(f => f.FlaggedUserId == userId
                                                                 && f.AdminAction == null);
            dto.PendingCertificatesCount = await _db.TblUserCertificates
                                                    .CountAsync(c => c.UserId == userId
                                                                  && c.ApprovedDate == null);

            return dto;
        }

    }
}