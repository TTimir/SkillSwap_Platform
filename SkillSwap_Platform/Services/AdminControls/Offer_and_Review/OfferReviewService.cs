using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels;

namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review
{
    public class OfferReviewService : IOfferReviewService
    {
        private readonly SkillSwapDbContext _db;

        public OfferReviewService(SkillSwapDbContext db)
            => _db = db;

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

        public async Task<PagedResult<ReviewFlagVm>> GetFlaggedReviewsAsync(int page, int pageSize)
        {
            // 1) pull all flagged reviews into memory
            var flaggedReviews = await _db.TblReviews
                .Where(r => r.IsFlagged)
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
                .Where(rr => rr.IsFlagged)
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
    }
}