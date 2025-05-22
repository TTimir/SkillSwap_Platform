using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.ReviewReplyVm;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.NotificationTrack;
using SkillSwap_Platform.Services.ReviewReply;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    public class UserReviewController : Controller
    {
        private readonly IReviewService _reviews;
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserReviewController> _logger;
        private readonly INotificationService _notif;
        private readonly IEmailService _emailService;

        public UserReviewController(
        IReviewService reviews,
        SkillSwapDbContext context,
        ILogger<UserReviewController> logger, INotificationService notif, IEmailService emailService)
        {
            _reviews = reviews ?? throw new ArgumentNullException(nameof(reviews));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notif = notif ?? throw new ArgumentNullException(nameof(notif));
            _emailService = emailService;
        }

        public async Task<IActionResult> MyOffers()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                    return Forbid();

                var offers = await _context.TblOffers
                    .Where(o => o.UserId == userId)
                    .Where(o => o.IsActive)
                    .Where(o => _context.TblReviews.Any(r => r.OfferId == o.OfferId))
                    .OrderByDescending(o => o.CreatedDate)
                    .ToListAsync();

                return View(offers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading offers for user");
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index(int offerId)
        {
            try
            {
                var reviews = await _context.TblReviews
                    .Where(r => r.OfferId == offerId)
                    .Include(r => r.Reviewer)                           // Reviewer nav
                    .Include(r => r.TblReviewReplies)
                        .ThenInclude(rep => rep.ReplierUser)            // Who replied
                    .OrderByDescending(r => r.CreatedDate)
                    .ToListAsync();

                return View(reviews);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reviews for offer {OfferId}", offerId);
                return RedirectToAction("EP500", "EP");
            }
        }

        #region Submit Review
        // POST: /UserReview/SubmitReview
        [HttpPost]
        public async Task<IActionResult> SubmitReview(int offerId, int rating)
        {
            // Wrap the multi-step review submission process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var offer = await _context.TblOffers.Include(o => o.User).FirstOrDefaultAsync(o => o.OfferId == offerId);
                    if (offer == null) return RedirectToAction("EP404", "EP");

                    var user = offer.User;
                    if (user == null) return RedirectToAction("EP404", "EP");

                    // ✅ Save the new review
                    var review = new TblReview
                    {
                        OfferId = offerId,
                        UserId = user.UserId,
                        Rating = rating
                    };
                    _context.TblReviews.Add(review);
                    await _context.SaveChangesAsync();

                    var owner = offer.User;
                    var htmlBodyNewReview = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        
        <!-- Header -->
        <tr>
          <td style=""border-top:4px solid rgba(0,168,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#00A88F;"">Swapo</h1>
          </td>
        </tr>

        <!-- Main Heading -->
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">New Review Received</h2>
            <p style=""margin:0 0 15px;"">
              Hi <strong>{owner.UserName}</strong>,
            </p>
            <p style=""margin:0 0 15px;"">
              Your swap offer “<strong>{offer.Title}</strong>” just received a 
              <strong>{review.Rating}-star</strong> review from {review.ReviewerName}.
            </p>
          </td>
        </tr>

        <!-- Divider -->
        <tr>
          <td style=""padding:0 20px;"">
            <hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""background-color:#00A88F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e0f7f1;font-size:14px;"">
              Thank you for being a valued member of <strong>Swapo</strong>. Your creativity and passion make our community thrive!
            </p>
            <p style=""margin:5px 0;color:#e0f7f1;font-size:13px;"">
              We appreciate you—keep sharing your skills and inspiring others.
            </p>
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>
";
                    await _emailService.SendEmailAsync(
                      owner.Email,
                      "You’ve got a new review on your swap!",
                      htmlBodyNewReview,
                      isBodyHtml: true
                    );


                    // Update the user's statistics (job success and recommendation percentages).
                    await UpdateUserStats(user.UserId);

                    // Commit transaction if all steps succeed.
                    await transaction.CommitAsync();
                    return Ok("Your review has been posted!");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error submitting review for offer {OfferId}", offerId);
                    return RedirectToAction("EP500", "EP");
                }
            }
        }
        #endregion

        #region Submit Reply
        // POST: /UserReview/SubmitReply
        [HttpPost]
        public async Task<IActionResult> SubmitReply(int reviewId, string replyText)
        {
            using (var tx = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var review = await _context.TblReviews.FindAsync(reviewId);
                    if (review == null)
                        return RedirectToAction("EP404", "EP");

                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!int.TryParse(userIdClaim, out var userId))
                        return Unauthorized();

                    var reply = new TblReviewReply
                    {
                        ReviewId = reviewId,
                        ReplierUserId = userId,
                        Comments = replyText,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.TblReviewReplies.Add(reply);
                    await _context.SaveChangesAsync();

                    var replier = await _context.TblUsers.FindAsync(userId);
                    if (replier != null)
                    {
                        var htmlBody1 = $@"
                            <!DOCTYPE html>
                            <html lang=""en"">
                            <head>
                              <meta charset=""UTF-8"">
                              <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                            </head>
                            <body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
                              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr><td align=""center"" style=""padding:20px;"">
                                  <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
                                    <tr>
                                      <td style=""border-top:4px solid rgba(0,168,143,0.8);padding:20px;"">
                                        <h1 style=""margin:0;font-size:24px;color:#00A88F;"">Swapo.</h1>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style=""padding:20px;color:#333333;line-height:1.5;"">
                                        <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">Reply Posted!</h2>
                                        <p style=""margin:0 0 10px;"">Hi <strong>{replier.UserName}</strong>,</p>
                                        <p style=""margin:0 0 15px;"">
                                          Your reply to “<strong>{review.Offer.Title}</strong>” was posted:
                                        </p>
                                        <blockquote style=""padding:.5rem;border-left:3px solid #ccc;margin:0 0 15px;"">
                                          {reply.Comments}
                                        </blockquote>
                                        <p style=""margin:0;"">
                                          Thanks for keeping Swapo conversations going!
                                        </p>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
                                    </tr>
                                    <tr>
                                      <td style=""background-color:#00A88F;padding:20px;text-align:center;"">
                                        <p style=""margin:10px 0;color:#e0f7f1;font-size:14px;"">
                                          Thank you for being a valued member of <strong>Swapo</strong>. Your creativity and passion make our community thrive!
                                        </p>
                                        <p style=""margin:5px 0;color:#e0f7f1;font-size:13px;"">
                                          We appreciate you—keep sharing your skills and inspiring others.
                                        </p>
                                      </td>
                                    </tr>
                                  </table>
                                </td></tr>
                              </table>
                            </body>
                            </html>";
                        await _emailService.SendEmailAsync(replier.Email, "Your reply was posted", htmlBody1, isBodyHtml: true);
                    }

                    // Notify the original reviewer that you replied
                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = review.UserId, // the original reviewee
                        Title = "Reply Posted",
                        Message = $"Your reply to the review on “{review.Offer.Title}” has been submitted and is now pending moderation.",
                        Url = Url.Action(nameof(Index), "UserReview", new { offerId = review.OfferId })
                    });

                    await tx.CommitAsync();
                    TempData["SuccessMessage"] = "Your response has been posted!";
                    return View();
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "Error submitting reply to review {ReviewId}", reviewId);
                    return RedirectToAction("EP500", "EP");
                }
            }
        }
        #endregion

        #region Flag Review & Reply
        [HttpPost]
        public async Task<IActionResult> Flag(int reviewId)
        {
            try
            {
                var review = await _context.TblReviews
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(r => r.ReviewId == reviewId);
                if (review == null) return NotFound();

                int currentUserId = GetUserId();

                if (review.UserId == currentUserId)
                    return BadRequest("You cannot flag your own review.");

                review.IsFlagged = true;
                review.FlaggedDate = DateTime.UtcNow;
                review.FlaggedByUserId = currentUserId;

                _context.TblReviews.Update(review);
                await _context.SaveChangesAsync();

                var flaggerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(flaggerIdClaim, out var flaggerUserId))
                    return Unauthorized();

                var flagger = await _context.TblUsers.FindAsync(flaggerUserId);
                if (flagger != null)
                {
                    var reviewUrl = Url.Action(
                        nameof(Index),
                        "UserReview",
                        new { offerId = review.OfferId },
                        Request.Scheme);

                    var htmlBody2 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        <tr>
          <td style=""border-top:4px solid rgba(0,168,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#00A88F;"">Swapo. Moderation</h1>
          </td>
        </tr>
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">Flag Received</h2>
            <p style=""margin:0 0 15px;"">
              Hi <strong>{flagger.UserName}</strong>, thanks for helping keep Swapo friendly.<br/>
              You flagged review <strong>#{review.ReviewId}</strong> on “<em>{review.Offer.Title}</em>.”
            </p>
            <p style=""margin:0;"">
              Our team will review it and take action within the next 24 hours.
            </p>
          </td>
        </tr>
        <tr>
          <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
        </tr>
        <tr>
          <td style=""background-color:#00A88F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e0f7f1;font-size:14px;"">
              Thank you for being a valued member of <strong>Swapo</strong>. Your creativity and passion make our community thrive!
            </p>
            <p style=""margin:5px 0;color:#e0f7f1;font-size:13px;"">
              We appreciate you—keep sharing your skills and inspiring others.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
                    await _emailService.SendEmailAsync(flagger.Email, "Confirmation: You flagged a review", htmlBody2, isBodyHtml: true);
                }

                // Notify moderation or the offer owner
                await _notif.AddAsync(new TblNotification
                {
                    UserId = review.UserId,
                    Title = "You flagged a review",
                    Message = $"You flagged review #{review.ReviewId} for moderation.",
                    Url = Url.Action(nameof(Index), "UserReview", new { offerId = review.OfferId })
                });
                TempData["SuccessMessage"] = "Review has been flagged for moderation.";

                // now we know the true offerId
                return RedirectToAction(nameof(Index),
                                        new { offerId = review.OfferId });
            }
            catch
            {
                _logger.LogError("Error flagging review {ReviewId}", reviewId);
                return RedirectToAction("EP500", "EP");
            }
        }
       

        [HttpPost]
        public async Task<IActionResult> FlagReply(int replyId)
        {
            try
            {
                // Load the reply along with its parent review so we can get the OfferId
                var reply = await _context.TblReviewReplies
                    .Include(r => r.Review)
                        .ThenInclude(rev => rev.Offer)
                        // navigation from reply → review
                    .FirstOrDefaultAsync(r => r.ReplyId == replyId);

                if (reply == null)
                    return NotFound();

                int currentUserId = GetUserId();

                if (reply.ReplierUserId == currentUserId)
                    return BadRequest("You cannot flag your own review.");

                // Mark as flagged
                var flaggerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                reply.IsFlagged = true;
                reply.FlaggedDate = DateTime.UtcNow;
                reply.FlaggedByUserId = currentUserId;

                _context.TblReviewReplies.Update(reply);
                await _context.SaveChangesAsync();

                // Notify the reply author only if someone else flags it
                if (flaggerId != reply.ReplierUserId)
                {
                    var author = await _context.TblUsers.FindAsync(reply.ReplierUserId);
                    var htmlBody3 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;border-collapse:collapse;"">
        <tr>
          <td style=""border-top:4px solid rgba(0,168,143,0.8);padding:20px;"">
            <h1 style=""margin:0;font-size:24px;color:#00A88F;"">Swapo Moderation</h1>
          </td>
        </tr>
        <tr>
          <td style=""padding:20px;color:#333333;line-height:1.5;"">
            <h2 style=""margin:0 0 15px;font-size:22px;font-weight:normal;"">Reply Flagged</h2>
            <p style=""margin:0 0 15px;"">
              Hi <strong>{author.UserName}</strong>,<br/>
              Your reply (ID: {reply.ReplyId}) on “<strong>{reply.Review.Offer.Title}</strong>” has been flagged for moderation.
            </p>
          </td>
        </tr>
        <tr>
          <td style=""padding:0 20px;""><hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;""/></td>
        </tr>
        <tr>
          <td style=""background-color:#00A88F;padding:20px;text-align:center;"">
            <p style=""margin:10px 0;color:#e0f7f1;font-size:14px;"">
              Thank you for being a valued member of <strong>Swapo</strong>. Your creativity and passion make our community thrive!
            </p>
            <p style=""margin:5px 0;color:#e0f7f1;font-size:13px;"">
              We appreciate you—keep sharing your skills and inspiring others.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
                    await _emailService.SendEmailAsync(author.Email, "One of your replies was flagged", htmlBody3, isBodyHtml: true);

                }

                // Grab the offerId from the parent review
                var offerId = reply.Review?.OfferId ?? 0;

                // Notify moderation or the original replier
                await _notif.AddAsync(new TblNotification
                {
                    UserId = reply.ReplierUserId,
                    Title = "Reply Flagged",
                    Message = $"One of your replies (#{replyId}) was flagged for moderation.",
                    Url = Url.Action(nameof(Index), "UserReview", new { offerId })
                });

                TempData["SuccessMessage"] = "Reply has been flagged for moderation.";

                // If for some reason we don’t have an offerId, just go back to the root Index
                if (offerId == 0)
                    return RedirectToAction(nameof(Index));

                // Redirect back to your reviews page for that offer
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging reply {ReplyId}", replyId);
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Helper Class
        /// <summary>
        /// Helper to get the current logged in user's ID from claims.
        /// </summary>
        /// <returns></returns>
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }

        private async Task UpdateUserStats(int userId)
        {
            try
            {
                var user = await _context.TblUsers
                .Include(u => u.TblReviewReviewees) // ✅ Includes reviews
                //.Include(u => u.TblExchangeLastStatusChangedByNavigations) // ✅ First set of exchanges
                //.Include(u => u.TblExchangeRequesters) // ✅ Second set of exchanges
                .Include(u => u.TblOffers) // ✅ User's offers
                .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null) return;

                // Calculate Recommended Percentage (Total Positive Reviews / Total Reviews)
                int totalReviews = user.TblReviewReviewees.Count();
                int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);
                user.RecommendedPercentage = totalReviews > 0 ? (positiveReviews * 100.0 / totalReviews) : 0;

                // Retrieve all exchanges where the user is either the OfferOwner or the OtherUser.
                var allExchanges = await _context.TblExchanges
                    .Where(e => e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId)
                    .ToListAsync();

                int totalExchanges = allExchanges.Count;
                int successfulExchanges = allExchanges.Count(e => e.IsSuccessful);
                double jobSuccessRate = totalExchanges > 0 ? (successfulExchanges * 100.0 / totalExchanges) : 0;

                // Update the user's JobSuccessRate property.
                user.JobSuccessRate = jobSuccessRate;

                // Update all offers belonging to this user
                foreach (var offer in user.TblOffers)
                {
                    offer.JobSuccessRate = user.JobSuccessRate;
                    offer.RecommendedPercentage = user.RecommendedPercentage;
                }

                // Save to DB
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user stats for user {UserId}", userId);
                throw;
            }

        }
        #endregion
    }
}
