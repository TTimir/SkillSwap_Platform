using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review
{
    public class OfferReviewService : IOfferReviewService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _emailService;

        public OfferReviewService(SkillSwapDbContext db, IEmailService email)
        {
            _db = db;
            _emailService = email;
        }

        public async Task<PagedResult<OfferFlagVm>> GetFlaggedOffersAsync(int page, int pageSize)
        {
            // 1) grouped summary
            var q = _db.TblOfferFlags
                .Include(f => f.Offer)
                .ThenInclude(o => o.User)
                .GroupBy(f => new { f.OfferId, f.Offer.Title, Seller = f.Offer.User.UserName })
                .Select(g => new OfferFlagVm
                {
                    OfferId = g.Key.OfferId,
                    Title = g.Key.Title,
                    SellerUserName = g.Key.Seller,
                    FlagCount = g.Count(),
                    LastFlaggedAt = g.Max(f => f.FlaggedDate)
                });

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(x => x.LastFlaggedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<OfferFlagVm>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<FlaggedOfferDetailsVm> GetOfferDetailsAsync(int offerId)
        {
            var offer = await _db.TblOffers
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OfferId == offerId)
                ?? throw new KeyNotFoundException("Offer not found");

            var logs = await _db.TblOfferFlags
                .Where(f => f.OfferId == offerId)
                .Include(f => f.FlaggedByUser)
                .OrderByDescending(f => f.FlaggedDate)
                .Select(f => new OfferFlagLog
                {
                    FlagId = f.OfferFlagId,
                    ReporterUserId = f.FlaggedByUserId,
                    ReporterUserName = f.FlaggedByUser.UserName,
                    FlaggedAt = f.FlaggedDate,
                    Reason = f.Reason
                })
                .ToListAsync();

            return new FlaggedOfferDetailsVm
            {
                OfferId = offer.OfferId,
                Title = offer.Title,
                Description = offer.Description,
                SellerUserName = offer.User.UserName,
                Flags = logs
            };
        }

        public async Task<PagedResult<ActiveFlagVm>> GetActiveFlagsAsync(int page, int pageSize)
        {
            // 1) Current review‐flags
            var reviewFlags = _db.TblReviews
                .Where(r => r.IsFlagged && !r.IsDeleted)
                .Select(r => new ActiveFlagVm
                {
                    EntityType = "Review",
                    EntityId = r.ReviewId,
                    OfferId = r.OfferId,
                    OfferTitle = r.Offer.Title,
                    AuthorUserName = r.ReviewerName,
                    ReporterUserId = r.FlaggedByUserId ?? 0,
                    ReporterUserName = _db.TblUsers
                              .Where(u => u.UserId == r.FlaggedByUserId)
                              .Select(u => u.UserName)
                              .FirstOrDefault() ?? "Unknown",
                    FlaggedAt = r.FlaggedDate ?? r.CreatedDate,
                    Reason = r.Comments ?? ""
                });

            // 2) Current reply‐flags
            var replyFlags = _db.TblReviewReplies
                .Where(rr => rr.IsFlagged && !rr.IsDeleted)
                .Select(rr => new ActiveFlagVm
                {
                    EntityType = "Reply",
                    EntityId = rr.ReplyId,
                    OfferId = rr.Review.OfferId,
                    OfferTitle = rr.Review.Offer.Title,
                    AuthorUserName = rr.ReplierUser.UserName,
                    ReporterUserId = rr.FlaggedByUserId ?? 0,
                    ReporterUserName = _db.TblUsers
                              .Where(u => u.UserId == rr.FlaggedByUserId)
                              .Select(u => u.UserName)
                              .FirstOrDefault() ?? "Unknown",
                    FlaggedAt = rr.FlaggedDate ?? rr.CreatedDate,
                    Reason = rr.Comments ?? ""
                });

            var allFlags = reviewFlags
                .Union(replyFlags)
                .OrderByDescending(f => f.FlaggedAt);

            var total = await allFlags.CountAsync();
            var items = await allFlags
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ActiveFlagVm>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<ReviewFlagVm>> GetFlaggedReviewsAsync(int page, int pageSize)
        {
            // 1) pull all flagged reviews into memory
            var flaggedReviews = await _db.TblReviews
                .Where(r => r.IsFlagged && !r.IsDeleted)
                .Include(r => r.Offer)
                .ToListAsync();

            // 2) group to compute dynamic flag counts & last‐flagged
            var flagStats = flaggedReviews
                .GroupBy(r => r.ReviewId)
                .Select(g => new
                {
                    ReviewId = g.Key,
                    FlagCount = g.Count(),
                    LastFlaggedAt = g.Max(r => r.FlaggedDate ?? r.CreatedDate)
                })
                .ToDictionary(x => x.ReviewId);

            // 3) project into view‐models
            var vms = flaggedReviews
                .Select(r =>
                {
                    flagStats.TryGetValue(r.ReviewId, out var stats);
                    return new ReviewFlagVm
                    {
                        ReviewId = r.ReviewId,
                        OfferId = r.OfferId,
                        OfferTitle = r.Offer.Title,
                        ReviewerUserName = r.ReviewerName,
                        Rating = (int)r.Rating,
                        FlagCount = stats?.FlagCount ?? 0,
                        LastFlaggedAt = stats?.LastFlaggedAt ?? r.CreatedDate
                    };
                })
                .OrderByDescending(x => x.LastFlaggedAt)
                .ToList();

            // 4) apply paging
            var pagedItems = vms
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<ReviewFlagVm>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = vms.Count(),
                Items = pagedItems
            };
        }

        public async Task<FlaggedReviewDetailsVm> GetFlaggedReviewDetailsAsync(int reviewId)
        {
            var r = await _db.TblReviews
                .Include(r => r.Offer)
                .FirstOrDefaultAsync(x => x.ReviewId == reviewId && x.IsFlagged)
                ?? throw new KeyNotFoundException("Review not found or not flagged");

            // we only have one “flag” inline, so wrap it in a single-item list
            var log = new ReviewFlagLog
            {
                FlagId = 0,                     // no separate flag id
                ReporterUserId = r.FlaggedByUserId ?? 0,
                ReporterUserName = await _db.TblUsers
                                             .Where(u => u.UserId == r.FlaggedByUserId)
                                             .Select(u => u.UserName)
                                             .FirstOrDefaultAsync() ?? "Unknown",
                FlaggedAt = r.FlaggedDate ?? r.CreatedDate,
                Reason = $"👍 {r.HelpfulCount}  👎 {r.NotHelpfulCount}"
            };

            return new FlaggedReviewDetailsVm
            {
                ReviewId = r.ReviewId,
                OfferId = r.OfferId,
                OfferTitle = r.Offer.Title,
                ReviewerUserName = r.ReviewerName,
                Rating = (int)r.Rating,
                Comment = r.Comments ?? "",
                Flags = new List<ReviewFlagLog> { log }
            };
        }

        // ---- Flagged Review Replies ----
        public async Task<PagedResult<ReviewFlagVm>> GetFlaggedReplyFlagsAsync(int page, int pageSize)
        {
            // 1) Pull all flagged replies (including the parent review → offer, and the replier user)
            var flaggedReplies = await _db.TblReviewReplies
                .Where(rr => rr.IsFlagged && !rr.IsDeleted)
                .Include(rr => rr.Review)
                    .ThenInclude(r => r.Offer)
                .Include(rr => rr.ReplierUser)
                .ToListAsync();

            // 2) Compute per‐reply stats in memory
            var stats = flaggedReplies
                .GroupBy(rr => rr.ReplyId)
                .Select(g => new
                {
                    ReplyId = g.Key,
                    FlagCount = g.Count(),
                    LastFlagged = g.Max(rr => rr.FlaggedDate ?? rr.CreatedDate)
                })
                .ToDictionary(x => x.ReplyId);

            // 3) Project each reply into your VM, pulling in offer info & replier name
            var vms = flaggedReplies
                .Select(rr =>
                {
                    stats.TryGetValue(rr.ReplyId, out var s);
                    return new ReviewFlagVm
                    {
                        ReviewId = rr.ReplyId,                        // reply-ID
                        OfferId = rr.Review.OfferId,                // parent offer
                        OfferTitle = rr.Review.Offer.Title,
                        ReviewerUserName = rr.ReplierUser.UserName,
                        Rating = 0,                                // replies have no rating
                        FlagCount = s?.FlagCount ?? 1,             // fallback to 1
                        LastFlaggedAt = s?.LastFlagged ?? rr.CreatedDate // fallback
                    };
                })
                .OrderByDescending(vm => vm.LastFlaggedAt)
                .ToList();

            // 4) page it
            var paged = vms
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<ReviewFlagVm>
            {
                Items = paged,
                TotalCount = vms.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<FlaggedReviewDetailsVm> GetReplyDetailsAsync(int replyId)
        {
            var rr = await _db.TblReviewReplies
                .FirstOrDefaultAsync(x => x.ReplyId == replyId && x.IsFlagged)
                ?? throw new KeyNotFoundException("Reply not found or not flagged");

            // load its parent review (so we can show its comment)
            var parent = await _db.TblReviews
                           .FirstOrDefaultAsync(r => r.ReviewId == rr.ReviewId)
                         ?? throw new KeyNotFoundException("Parent review not found.");

            var review = await _db.TblReviews
                .Include(r => r.Offer)
                .FirstOrDefaultAsync(r => r.ReviewId == rr.ReviewId)
                ?? throw new KeyNotFoundException("Parent review not found");

            var replierName = await _db.TblUsers
                .Where(u => u.UserId == rr.ReplierUserId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync()
                ?? "Unknown";

            var log = new ReviewFlagLog
            {
                FlagId = rr.ReplyId,
                ReporterUserId = rr.FlaggedByUserId ?? 0,
                ReporterUserName = await _db.TblUsers
                                             .Where(u => u.UserId == rr.FlaggedByUserId)
                                             .Select(u => u.UserName)
                                             .FirstOrDefaultAsync() ?? "Unknown",
                FlaggedAt = rr.FlaggedDate ?? rr.CreatedDate,
                Reason = string.IsNullOrWhiteSpace(rr.Comments)
                    ? "—"
                    : rr.Comments
            };

            return new FlaggedReviewDetailsVm
            {
                ReviewId = rr.ReplyId,
                ReplyId = rr.ReplyId,
                OfferId = review.OfferId,
                OfferTitle = review.Offer.Title,
                ParentReviewUserName = parent.ReviewerName,
                ReviewerUserName = replierName,
                Rating = 0,
                Comment = rr.Comments ?? "",
                ParentReviewId = parent.ReviewId,
                ParentReviewComment = parent.Comments,
                Flags = new List<ReviewFlagLog> { log }
            };
        }

        public async Task DismissOfferFlagsAsync(int offerId, int adminId, string notes)
        {
            // 1) remove all flags for that offer
            var flags = _db.TblOfferFlags.Where(f => f.OfferId == offerId);
            _db.TblOfferFlags.RemoveRange(flags);

            // 2) log that you dismissed them
            _db.TblExchangeHistories.Add(new TblExchangeHistory
            {
                OfferId = offerId,
                ChangedStatus = "FlagsCleared",
                ChangedBy = adminId,
                ChangeDate = DateTime.UtcNow,
                Reason = notes
            });

            await _db.SaveChangesAsync();
        }

        public async Task DismissReviewFlagsAsync(int reviewId, int flagId, int adminId, string notes)
        {
            var review = await _db.TblReviews
                                  .FirstOrDefaultAsync(r => r.ReviewId == reviewId)
                       ?? throw new KeyNotFoundException("Review not found");

            if (!review.IsFlagged)
                throw new InvalidOperationException("Review is not flagged.");

            // 1) Clear the flag on the review
            review.IsFlagged = false;
            review.FlaggedDate = null;
            review.FlaggedByUserId = null;
            _db.TblReviews.Update(review);

            // 2) Record the moderation action
            _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
            {
                FlagId = flagId,
                ReviewId = reviewId,
                AdminId = adminId,
                Action = "DismissedFlag",
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task DismissReplyFlagsAsync(int replyId, int adminId, string notes)
        {
            var reply = await _db.TblReviewReplies
                                 .FirstOrDefaultAsync(r => r.ReplyId == replyId && r.IsFlagged)
                        ?? throw new KeyNotFoundException("Reply not found or not flagged.");

            // clear the flag
            reply.IsFlagged = false;
            reply.FlaggedDate = null;
            reply.FlaggedByUserId = null;
            _db.TblReviewReplies.Update(reply);

            var parent = await _db.TblReviews
                          .FirstOrDefaultAsync(r => r.ReviewId == reply.ReviewId)
               ?? throw new KeyNotFoundException("Parent review not found.");

            // log the moderation action
            _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
            {
                ReviewId = parent.ReviewId,
                ReplyId = replyId,
                AdminId = adminId,
                Action = "DismissedReplyFlag",
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task ModerateAndWarnAsync(
            int contentId,
            bool isReply,
            int adminId,
            string moderationNote,
            string warningMessage)
        {
            // 1) Locate the entity
            if (!isReply)
            {
                // Soft‐delete the review
                var review = await _db.TblReviews
                                      .FirstOrDefaultAsync(r => r.ReviewId == contentId)
                             ?? throw new KeyNotFoundException("Review not found");

                review.IsDeleted = true;
                review.DeletedAt = DateTime.UtcNow;
                review.DeletedByAdminId = adminId;
                review.DeletionReason = moderationNote;
                _db.TblReviews.Update(review);

                // 2) Log it
                _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
                {
                    ReviewId = contentId,
                    AdminId = adminId,
                    Action = "DeletedReview",
                    Notes = moderationNote,
                    CreatedAt = DateTime.UtcNow
                });

                // 3) Warn the user
                await WarnUserInternalAsync(
                    review.ReviewerId,
                    warningMessage,
                    entityType: "Review",
                    entityId: contentId
                );
            }
            else
            {
                var reply = await _db.TblReviewReplies
                                     .FirstOrDefaultAsync(r => r.ReplyId == contentId)
                            ?? throw new KeyNotFoundException("Reply not found");

                reply.IsDeleted = true;
                reply.DeletedAt = DateTime.UtcNow;
                reply.DeletedByAdminId = adminId;
                reply.DeletionReason = moderationNote;
                _db.TblReviewReplies.Update(reply);

                // 2) Log it (reuse your same history table, or create TblReplyModerationHistory)
                _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
                {
                    ReviewId = reply.ReviewId,
                    ReplyId = contentId,
                    AdminId = adminId,
                    Action = "DeletedReply",
                    Notes = moderationNote,
                    CreatedAt = DateTime.UtcNow
                });
                
                _db.TblReviewReplies.Remove(reply);

                // 3) Warn the user
                await WarnUserInternalAsync(
                    reply.ReplierUserId,
                    warningMessage,
                    entityType: "Reply",
                    entityId: contentId
                );
            }

            await _db.SaveChangesAsync();

        }

        private async Task WarnUserInternalAsync(
            int userId,
            string message,
            string entityType,
            int entityId)
        {
            var user = await _db.TblUsers.FindAsync(userId);
            if (user != null)
            {
                var sentAt = DateTime.UtcNow.ToLocalTime()
                        .ToString("dd MMMM, yyyy hh:mm tt");

                var body = $@"
                  <p>Hi {user.UserName},</p>
                
                  <p>{message}</p>
                
                  <p style=""font-size:.9em;color:#666;"">
                    <em>Sent on {sentAt}</em>
                  </p>
                
                  <p>
                    If you have any questions or need help, just reply to this email and we’ll get right back to you.
                  </p>
                
                  <p>
                    Thanks,<br/>
                    The SkillSwap Team
                  </p>
                ";

                // send email
                await _emailService.SendEmailAsync(
                    to: user.Email,
                    subject: "⚠️ Content removed – please read",
                    body: body,
                    isBodyHtml: true
                );

                // record a warning
                _db.TblUserWarnings.Add(new TblUserWarning
                {
                    UserId = userId,
                    EntityType = entityType,     // e.g. "Review" or "Reply"
                    EntityId = entityId,       // the PK of the flagged entity
                    Message = message,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        public async Task<PagedResult<FlagHistoryVm>> GetFlagHistoryAsync(int page, int pageSize)
        {
            // 1) Query your moderation history
            var q = _db.TblReviewModerationHistories
                       .Include(h => h.Admin)
                       .Include(h => h.Review)          // for parent review → offer
                         .ThenInclude(r => r.Offer)
                       .Where(h => h.Action.StartsWith("Deleted")
                                || h.Action.StartsWith("Dismissed"));

            // 2) Count & page
            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(h => h.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new FlagHistoryVm
                {
                    EntityType = h.ReplyId != null ? "Reply" : "Review",
                    EntityId = h.ReplyId > 0
                                    ? h.ReplyId
                                    : h.ReviewId,
                    OfferId = h.Review.OfferId,
                    OfferTitle = h.Review.Offer.Title,
                    AuthorUserName = h.ReplyId > 0
                                        ? h.Reply.ReplierUser.UserName
                                        : h.Review.ReviewerName,
                    AdminUserName = h.Admin.UserName,
                    Action = h.Action,    // e.g. "DeletedReview", "DismissedFlag"
                    Notes = h.Notes,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<FlagHistoryVm>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}