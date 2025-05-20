using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;

namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review
{
    public class OfferReviewService : IOfferReviewService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subs;
        public OfferReviewService(SkillSwapDbContext db, IEmailService email, ISubscriptionService subscription)
        {
            _db = db;
            _emailService = email;
            _subs = subscription;
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

        //public async Task DismissOfferFlagsAsync(int offerId, int adminId, string notes)
        //{
        //    // 1) remove all flags for that offer
        //    var flags = _db.TblOfferFlags.Where(f => f.OfferId == offerId);
        //    _db.TblOfferFlags.RemoveRange(flags);

        //    // 2) log that you dismissed them
        //    _db.TblExchangeHistories.Add(new TblExchangeHistory
        //    {
        //        OfferId = offerId,
        //        ChangedStatus = "FlagsCleared",
        //        ChangedBy = adminId,
        //        ChangeDate = DateTime.UtcNow,
        //        Reason = notes
        //    });

        //    await _db.SaveChangesAsync();
        //}

        public async Task DismissReviewFlagsAsync(int reviewId, int flagId, int adminId, string notes)
        {
            // 1) Load review & reporter info
            var review = await _db.TblReviews
                                  .FirstOrDefaultAsync(r => r.ReviewId == reviewId)
                         ?? throw new KeyNotFoundException("Review not found");

            var flag = await _db.TblReviewModerationHistories
                                .FirstOrDefaultAsync(h => h.FlagId == flagId)
                       ?? throw new KeyNotFoundException("Flag record not found");

            var author = await _db.TblUsers.FindAsync(review.ReviewerId);
            var reporter = await _db.TblUsers.FindAsync(review.FlaggedByUserId);

            // 2) Clear the flag
            review.IsFlagged = false;
            review.FlaggedDate = null;
            review.FlaggedByUserId = null;
            _db.TblReviews.Update(review);

            // 3) Record mod action
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

            // common helper to build prefix
            (string label, string sla) GetPrefix(int userId)
            {
                var plan = _subs.GetActiveAsync(userId).Result?.PlanName ?? "Free";
                return plan switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };
            }
            ;

            // 4) Notify the **author** that their review is cleared
            if (author != null)
            {
                var (label, sla) = GetPrefix(author.UserId);
                var subject = $"[{label} · {sla}] Update on your review for “{review.Offer.Title}”";
                var body = $@"
                Hello {author.FirstName},<br/><br/>

                We’ve completed our review of the report on your review for<br/>
                <strong>“{review.Offer.Title}”</strong>.<br/><br/>

                <strong>Moderator’s note:</strong><br/>
                <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">
                  {notes}
                </blockquote><br/>

                No further action is required. Thanks for being a valued member of our community!<br/><br/>

                Warm regards,<br/>
                <em>The SkillSwap Support Team</em>
            ";
                await _emailService.SendEmailAsync(author.Email, subject, body, isBodyHtml: true);
            }

            // 5) Acknowledge the **reporter** that the flag was dismissed
            if (reporter != null)
            {
                var (label, sla) = GetPrefix(reporter.UserId);
                var subject = $"[{label} · {sla}] Result of your report on “{review.Offer.Title}”";
                var body = $@"
                Hi {reporter.FirstName},<br/><br/>

                Thank you for bringing this to our attention. We’ve reviewed the report on the review for<br/>
                <strong>“{review.Offer.Title}”</strong> and found no violation.<br/><br/>

                <strong>Moderator’s note:</strong><br/>
                <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">
                  {notes}
                </blockquote><br/>

                We appreciate your help maintaining a respectful marketplace!<br/><br/>

                Sincerely,<br/>
                <em>The SkillSwap Support Team</em>
            ";
                await _emailService.SendEmailAsync(reporter.Email, subject, body, isBodyHtml: true);
            }
        }

        public async Task DismissReplyFlagsAsync(int replyId, int adminId, string notes)
        {
            // 1) Load flagged reply & its reporter
            var reply = await _db.TblReviewReplies
                                 .FirstOrDefaultAsync(r => r.ReplyId == replyId && r.IsFlagged)
                        ?? throw new KeyNotFoundException("Reply not found or not flagged.");

            var reporter = await _db.TblUsers.FindAsync(reply.FlaggedByUserId);
            var author = await _db.TblUsers.FindAsync(reply.ReplierUserId);

            // 2) Clear the flag
            reply.IsFlagged = false;
            reply.FlaggedDate = null;
            reply.FlaggedByUserId = null;
            _db.TblReviewReplies.Update(reply);

            // 3) Log moderation action
            _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
            {
                ReviewId = reply.ReviewId,
                ReplyId = replyId,
                AdminId = adminId,
                Action = "DismissedReplyFlag",
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // same prefix helper
            (string label, string sla) GetPrefix(int userId)
            {
                var plan = _subs.GetActiveAsync(userId).Result?.PlanName ?? "Free";
                return plan switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };
            }
            ;
            // 4) Notify the author that the flag was dismissed
            if (author != null)
            {
                var (label, sla) = GetPrefix(author.UserId);
                var subject = $"[{label} · {sla}] Update on your reply (ID: {replyId})";
                var body = $@"
                Hello {author.FirstName},<br/><br/>

                We’ve reviewed the report on your reply under review “{reply.Review.Comments}” and found no violation.<br/><br/>

                <strong>Moderator’s note:</strong><br/>
                <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">
                  {notes}
                </blockquote><br/>

                Thank you for contributing positively to our community!<br/><br/>

                Best regards,<br/>
                <em>The SkillSwap Support Team</em>
            ";
                await _emailService.SendEmailAsync(author.Email, subject, body, isBodyHtml: true);
            }

            // 5) Acknowledge the reporter that the flag was dismissed
            if (reporter != null)
            {
                var (label, sla) = GetPrefix(author.UserId);
                var subject = $"[{label} · {sla}] Result of your report on a reply";
                var body = $@"
                Hi {reporter.FirstName},<br/><br/>

                Thank you for reporting a reply that you felt violated our guidelines.  
                We’ve carefully reviewed it and found that no action was necessary.<br/><br/>

                <strong>Moderator’s note:</strong><br/>
                <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">
                  {notes}
                </blockquote><br/>

                We appreciate your vigilance in keeping SkillSwap respectful!<br/><br/>

                Sincerely,<br/>
                <em>The SkillSwap Support Team</em>
            ";
                await _emailService.SendEmailAsync(reporter.Email, subject, body, isBodyHtml: true);
            }
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
                // 0) Load the flagged review (so we know who reported it)
                var review = await _db.TblReviews
                    .FirstOrDefaultAsync(r => r.ReviewId == contentId && r.IsFlagged)
                    ?? throw new KeyNotFoundException("Review not found or not flagged.");
                var reporterId = review.FlaggedByUserId;

                // 1) Soft-delete the review
                review.IsDeleted = true;
                review.DeletedAt = DateTime.UtcNow;
                review.DeletedByAdminId = adminId;
                review.DeletionReason = moderationNote;
                _db.TblReviews.Update(review);
                await _db.SaveChangesAsync();

                // 2) Log the action
                _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
                {
                    ReviewId = contentId,
                    AdminId = adminId,
                    Action = "DeletedReview",
                    Notes = moderationNote,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                var author = await _db.TblUsers.FindAsync(review.ReviewerId);
                // prefix again
                (string label, string sla) = _subs.GetActiveAsync(author.UserId).Result.PlanName switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };

                // 3a) Notify the review’s author
                if (author != null)
                {
                    var subject = $"[{label} · {sla}] Notice: Your review has been removed";
                    var body = $@"
                        <p>Hi {author.FirstName},</p>
                        <p>We’ve removed your review because it didn’t meet our Community Guidelines.</p>
                        <p><strong>Moderator’s note:</strong></p>
                        <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                          {moderationNote}
                        </blockquote>
                        <p>If you’d like to appeal or learn more, just reply to this email.</p>
                        <p>— The SkillSwap Support Team</p>
                    ";
                    await _emailService.SendEmailAsync(author.Email, subject, body, isBodyHtml: true);
                }

                // 3b) Thank & inform the reporter
                if (reporterId.HasValue)
                {
                    var reporter = await _db.TblUsers.FindAsync(reporterId.Value);
                    if (reporter != null)
                    {
                        // 1) get their current plan
                        var reporterSub = await _subs.GetActiveAsync(reporter.UserId);
                        var plan = reporterSub?.PlanName ?? "Free";

                        // 2) map to support label + SLA
                        var (reportLabel, reportSla) = plan switch
                        {
                            "Plus" => ("Plus Support", "72h SLA"),
                            "Pro" => ("Pro Support", "48h SLA"),
                            "Growth" => ("Growth Support", "24h SLA"),
                            _ => ("Free Support", "120h SLA")
                        };

                        // 3) prefix your subject
                        var subject = $"[{reportLabel} · {reportSla}] Thank you: your report helped us take action";
                        var body = $@"
                            <p>Hi {reporter.FirstName},</p>
                            <p>Thanks for reporting that review. We’ve investigated and removed it.</p>
                            <p><strong>Removed review:</strong></p>
                            <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                              {review.Comments}
                            </blockquote>
                            <p><strong>Moderator’s note:</strong></p>
                            <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                              {moderationNote}
                            </blockquote>
                            <p>Your help makes SkillSwap safer for everyone—thank you!</p>
                            <p>— The SkillSwap Support Team</p>
                        ";
                        await _emailService.SendEmailAsync(reporter.Email, subject, body, isBodyHtml: true);
                    }
                }
            }
            else
            {
                // 0) Load the flagged reply so we know who reported it
                var reply = await _db.TblReviewReplies
                    .FirstOrDefaultAsync(r => r.ReplyId == contentId && r.IsFlagged)
                    ?? throw new KeyNotFoundException("Reply not found or not flagged.");
                var reporterId = reply.FlaggedByUserId;

                // 1) Soft‐delete the reply
                reply.IsDeleted = true;
                reply.DeletedAt = DateTime.UtcNow;
                reply.DeletedByAdminId = adminId;
                reply.DeletionReason = moderationNote;
                _db.TblReviewReplies.Update(reply);
                await _db.SaveChangesAsync();

                // 2) Log the moderation action
                _db.TblReviewModerationHistories.Add(new TblReviewModerationHistory
                {
                    ReviewId = reply.ReviewId,
                    ReplyId = contentId,
                    AdminId = adminId,
                    Action = "DeletedReply",
                    Notes = moderationNote,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                // 3a) Notify the reply’s author
                var author = await _db.TblUsers.FindAsync(reply.ReplierUserId);
                // prefix again
                (string label, string sla) = _subs.GetActiveAsync(author.UserId).Result.PlanName switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "48h SLA"),
                    "Growth" => ("Growth Support", "24h SLA"),
                    _ => ("Free Support", "120h SLA")
                };
                if (author != null)
                {
                    var subject = $"[{label} · {sla}] Notice: Your reply has been removed";
                    var body = $@"
                        <p>Hi {author.FirstName},</p>
                        <p>We’ve removed your reply because it didn’t follow our Community Guidelines.</p>
                        <p><strong>Moderator’s note:</strong></p>
                        <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                          {moderationNote}
                        </blockquote>
                        <p>If you believe this was a mistake or want to learn more, just reply to this email.</p>
                        <p>— The SkillSwap Support Team</p>
                    ";
                    await _emailService.SendEmailAsync(author.Email, subject, body, isBodyHtml: true);
                }

                // 3b) Thank & inform the reporter
                if (reporterId.HasValue)
                {
                    var reporter = await _db.TblUsers.FindAsync(reporterId.Value);
                    if (reporter != null)
                    {
                        // 1) get their current plan
                        var reporterSub = await _subs.GetActiveAsync(reporter.UserId);
                        var plan = reporterSub?.PlanName ?? "Free";

                        // 2) map to support label + SLA
                        var (reportLabel, reportSla) = plan switch
                        {
                            "Plus" => ("Plus Support", "72h SLA"),
                            "Pro" => ("Pro Support", "48h SLA"),
                            "Growth" => ("Growth Support", "24h SLA"),
                            _ => ("Free Support", "120h SLA")
                        };

                        // 3) prefix your subject
                        var subject = $"[{reportLabel} · {reportSla}] Thank you: your report helped us take action";
                        var body = $@"
                            <p>Hi {reporter.FirstName},</p>
                            <p>Thanks for reporting that reply. We’ve reviewed and removed it.</p>
                            <p><strong>Removed reply content:</strong></p>
                            <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                              {reply.Comments}
                            </blockquote>
                            <p><strong>Moderator’s note:</strong></p>
                            <blockquote style=""border-left:4px solid #ccc; padding-left:1em;"">
                              {moderationNote}
                            </blockquote>
                            <p>Your help keeps SkillSwap safe—thank you!</p>
                            <p>— The SkillSwap Support Team</p>
                        ";
                        await _emailService.SendEmailAsync(reporter.Email, subject, body, isBodyHtml: true);
                    }
                }
            }

            await _db.SaveChangesAsync();

        }


        public async Task<PagedResult<FlagHistoryVm>> GetFlagHistoryAsync(int page, int pageSize)
        {
            // 1) Initial flag events on reviews
            var reviewFlags = await _db.TblReviews
                .Where(r => r.FlaggedDate != null)
                .Include(r => r.Offer)
                .Select(r => new FlagHistoryVm
                {
                    EntityType = "Review",
                    EntityId = r.ReviewId,
                    OfferId = r.OfferId,
                    OfferTitle = r.Offer.Title,

                    // the person who wrote it
                    ReviewAuthorUserName = r.ReviewerName,
                    // who reported it
                    FlaggedByUserName = _db.TblUsers
                                           .Where(u => u.UserId == r.FlaggedByUserId)
                                           .Select(u => u.UserName)
                                           .FirstOrDefault() ?? "Unknown",

                    AdminAction = "Flagged",
                    FlaggedDate = r.FlaggedDate!.Value,

                    // no admin yet
                    AdminUserName = null,
                    AdminReason = null,
                    AdminActionDate = null
                })
                .ToListAsync();

            // 2) Initial flag events on replies
            var replyFlags = await _db.TblReviewReplies
                .Where(rr => rr.FlaggedDate != null)
                .Include(rr => rr.Review).ThenInclude(r => r.Offer)
                .Select(rr => new FlagHistoryVm
                {
                    EntityType = "Reply",
                    EntityId = rr.ReplyId,
                    OfferId = rr.Review.OfferId,
                    OfferTitle = rr.Review.Offer.Title,

                    ReviewAuthorUserName = rr.ReplierUser.UserName,
                    FlaggedByUserName = _db.TblUsers
                                           .Where(u => u.UserId == rr.FlaggedByUserId)
                                           .Select(u => u.UserName)
                                           .FirstOrDefault() ?? "Unknown",

                    AdminAction = "Flagged",
                    FlaggedDate = rr.FlaggedDate!.Value,

                    AdminUserName = null,
                    AdminReason = null,
                    AdminActionDate = null
                })
                .ToListAsync();

            // 3) Moderation history entries (dismissals/deletions)
            var modLogs = await _db.TblReviewModerationHistories
                .Include(h => h.Admin)
                .Include(h => h.Review).ThenInclude(r => r.Offer)
                .Include(h => h.Reply).ThenInclude(rr => rr.Review).ThenInclude(r => r.Offer)
                .Select(h => new FlagHistoryVm
                {
                    EntityType = h.ReplyId != null ? "Reply" : "Review",
                    EntityId = h.ReplyId ?? h.ReviewId,
                    OfferId = (h.ReplyId != null
                                          ? h.Reply.Review.OfferId
                                          : h.Review.OfferId),
                    OfferTitle = (h.ReplyId != null
                                          ? h.Reply.Review.Offer.Title
                                          : h.Review.Offer.Title),

                    // who originally wrote it
                    ReviewAuthorUserName = (h.ReplyId != null
                                          ? h.Reply.ReplierUser.UserName
                                          : h.Review.ReviewerName),

                    // admin who took this action
                    AdminUserName = h.Admin.UserName,
                    AdminAction = h.Action,           // e.g. "DismissedFlag", "DeletedReview"…
                    AdminReason = h.Notes,
                    AdminActionDate = h.CreatedAt,

                    // for a moderation log we leave Reporter empty
                    FlaggedByUserName = null,
                    FlaggedDate = h.CreatedAt
                })
                .ToListAsync();

            // 4) Merge, sort, and page
            var all = reviewFlags
                .Concat(replyFlags)
                .Concat(modLogs)
                .OrderByDescending(x => x.FlaggedDate)
                .ToList();

            var pagedItems = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<FlagHistoryVm>
            {
                Items = pagedItems,
                TotalCount = all.Count(),
                Page = page,
                PageSize = pageSize
            };
        }
    }
}