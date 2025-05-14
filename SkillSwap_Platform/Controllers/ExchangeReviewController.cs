using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services.BadgeTire;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ExchangeReviewController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ExchangeReviewController> _logger;
        private readonly BadgeService _badgeService;

        public ExchangeReviewController(SkillSwapDbContext context, ILogger<ExchangeReviewController> logger, BadgeService badgeService)
        {
            _context = context;
            _logger = logger;
            _badgeService = badgeService;
        }

        #region Helper Method to Update User Aggregates

        // This helper method recalculates and updates a user’s aggregate metrics
        // (review count, average rating, recommended percentage, and job success rate)
        // and saves them into the user record.
        private async Task UpdateUserAggregatesAsync(int userId)
        {
            // Retrieve all reviews written for this user.
            var userReviews = await _context.TblReviews
                                    .Where(r => r.UserId == userId)
                                    .ToListAsync();

            int count = userReviews.Count;
            // Calculate the average rating.
            decimal avgRating = count > 0 ? (decimal)userReviews.Average(r => r.Rating) : 0;
            // Count how many reviews are positive (for example, rating >= 4).
            int positiveCount = count > 0 ? userReviews.Count(r => r.Rating >= 4) : 0;
            // Calculate the recommended percentage.
            decimal recommendedPercentage = count > 0 ? (positiveCount / (decimal)count) * 100 : 0;

            // Retrieve all exchanges where the user participated either as the offer owner or as the other party.
            var userExchanges = await _context.TblExchanges
                                    .Where(e => e.OfferOwnerId == userId || e.OtherUserId == userId)
                                    .ToListAsync();
            int totalExchanges = userExchanges.Count;
            int completedExchanges = totalExchanges > 0
                                     ? userExchanges.Count(e => e.Status != null && e.Status.Trim().ToLower() == "completed")
                                     : 0;
            // Calculate the job success rate based on the percentage of completed exchanges.
            decimal jobSuccessRate = totalExchanges > 0 ? (completedExchanges / (decimal)totalExchanges) * 100 : 0;

            // Retrieve the user entity and update the properties.
            var user = await _context.TblUsers.FindAsync(userId);
            if (user != null)
            {
                user.ReviewCount = count;
                user.AverageRating = avgRating;
                user.RecommendedPercentage = (double?)recommendedPercentage;
                user.JobSuccessRate = (double?)jobSuccessRate;

                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region Review GET and POST Actions

        // GET: /ExchangeReview/Review?exchangeId=...
        [HttpGet]
        public async Task<IActionResult> Review(int exchangeId)
        {
            // Retrieve the exchange record
            var exchange = await _context.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("EP404", "EP");
            }

            // Get the current user ID from claims
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int currentUserId))
            {
                return Forbid();
            }

            // Check if the current user participated in the exchange and if the exchange is completed.
            // Use ToLower() for translation compatibility.
            var completedExchange = await _context.TblExchanges.FirstOrDefaultAsync(e =>
                e.OfferId == exchange.OfferId &&
                (e.OfferOwnerId == currentUserId || e.OtherUserId == currentUserId) &&
                e.Status != null && e.Status.ToLower() == "completed");

            if (completedExchange == null)
            {
                TempData["ErrorMessage"] = "Reviews can only be submitted after the exchange is completed.";
                // Redirect back to a relevant page (e.g. the exchange details or dashboard)
                return RedirectToAction("Details", "ExchangeDashboard", new { exchangeId = exchangeId });
            }

            // Optionally, retrieve stored reviewer details from cookies.
            string reviewerName = Request.Cookies["ReviewerName"] ?? string.Empty;
            string reviewerEmail = Request.Cookies["ReviewerEmail"] ?? string.Empty;

            var model = new OfferExchangeReviewVM
            {
                ExchangeId = exchange.ExchangeId,
                OfferId = exchange.OfferId,
                ReviewerName = reviewerName,
                ReviewerEmail = reviewerEmail
            };

            return View(model);
        }

        // POST: /ExchangeReview/Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(OfferExchangeReviewVM model)
        {
            ValidateReviewModel(model);

            if (!ModelState.IsValid)
            {
                // Collect the validation error messages
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                TempData["ErrorMessage"] = "Please correct the following errors: " + string.Join(" | ", validationErrors);
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId = model.OfferId });
            }

            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentUserId))
                return Forbid();

            // Retrieve the exchange record to verify existence.
            var exchange = await _context.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == model.ExchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("EP404", "EP");
            }

            // *** NEW: Prevent the same user from submitting more than two reviews for this exchange ***
            int existingReviewCount = await _context.TblReviews
                .Where(r => r.ExchangeId == model.ExchangeId && r.ReviewerId == currentUserId)
                .CountAsync();
            if (existingReviewCount >= 2)
            {
                TempData["ErrorMessage"] = "You have already submitted two reviews for this exchange.";
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId = model.OfferId });
            }
            // *** End of new check ***

            // Determine the reviewee's user id based on the current user's participation.
            int revieweeId = DetermineReviewee(exchange, currentUserId);
            if (revieweeId == 0)
            {
                ModelState.AddModelError("", "Could not determine the reviewee for this exchange.");
                TempData["ErrorMessage"] = "Could not determine the reviewee for this exchange.";
                return RedirectToAction("EP404", "EP");
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {

                // Create a new review record. (Ensure TblReview exists and its properties match.)
                var newReview = new TblReview
                {
                    ExchangeId = model.ExchangeId,
                    OfferId = exchange.OfferId,
                    ReviewerId = currentUserId,
                    RevieweeId = revieweeId,
                    Rating = model.Rating,
                    Comments = model.Comments,
                    CreatedDate = DateTime.UtcNow,
                    ReviewerName = model.ReviewerName,
                    ReviewerEmail = model.ReviewerEmail,
                    // If your TblReview uses a property like UserId for the reviewee, set it here:
                    UserId = revieweeId
                };

                _context.TblReviews.Add(newReview);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Update aggregates for the reviewed user.
                await UpdateUserAggregatesAsync(newReview.UserId);

                // Award 5-Star Mentor for  participants:
                _badgeService.EvaluateAndAward(newReview.UserId);

                SetRememberMeCookies(model);

                TempData["SuccessMessage"] = "Thank you for your review!";
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId = model.OfferId });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // Log the error
                _logger.LogError(ex, "Error while submitting review for exchange ID {ExchangeId}", model.ExchangeId);
                TempData["ErrorMessage"] = "An error occurred while submitting your review. Please try again.";
                return RedirectToAction("EP500", "EP");
            }
        }

        private void ValidateReviewModel(OfferExchangeReviewVM m)
        {
            if (m.Rating < 1 || m.Rating > 5)
                ModelState.AddModelError(nameof(m.Rating), "Rating must be between 1 and 5.");
            if (string.IsNullOrWhiteSpace(m.Comments))
                ModelState.AddModelError(nameof(m.Comments), "Comments are required.");
            if (string.IsNullOrWhiteSpace(m.ReviewerName))
                ModelState.AddModelError(nameof(m.ReviewerName), "Name is required.");
            if (string.IsNullOrWhiteSpace(m.ReviewerEmail))
                ModelState.AddModelError(nameof(m.ReviewerEmail), "Email is required.");
        }

        private int DetermineReviewee(TblExchange ex, int currentUserId)
            => ex.OfferOwnerId == currentUserId
               ? ex.OtherUserId ?? 0
               : ex.OtherUserId == currentUserId
                   ? ex.OfferOwnerId ?? 0
                   : 0;

        private void SetRememberMeCookies(OfferExchangeReviewVM model)
        {
            if (model.RememberMe)
            {
                var opts = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = false,
                    Secure = true
                };
                Response.Cookies.Append("ReviewerName", model.ReviewerName!, opts);
                Response.Cookies.Append("ReviewerEmail", model.ReviewerEmail!, opts);
            }
            else
            {
                Response.Cookies.Delete("ReviewerName");
                Response.Cookies.Delete("ReviewerEmail");
            }
        }
        #endregion

        #region Vote Action(Helpful or not) for Review Voting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(int reviewId, string vote)
        {
            // Create a unique key for this review vote in the session.
            string voteSessionKey = $"ReviewVote_{reviewId}";
            if (HttpContext.Session.GetString(voteSessionKey) != null)
            {
                // If this key exists, the user has already voted on this review.
                return Json(new { success = false, message = "You have already voted on this review." });
            }

            var review = await _context.TblReviews.FindAsync(reviewId);
            if (review == null)
            {
                return RedirectToAction("EP404", "EP");
            }

            // Update the vote count according to the vote type.
            if (vote == "helpful")
            {
                review.HelpfulCount++;
            }
            else if (vote == "not_helpful")
            {
                review.NotHelpfulCount++;
            }
            else
            {
                return BadRequest("Invalid vote type.");
            }

            await _context.SaveChangesAsync();

            // Helpful Reviewer badge evaluation
            _badgeService.EvaluateAndAward(review.ReviewerId);

            // Mark this review as voted in the session.
            HttpContext.Session.SetString(voteSessionKey, "voted");

            // Return both the updated helpful and not helpful counts.
            return Json(new
            {
                success = true,
                message = "Your vote has been recorded.",
                helpfulCount = review.HelpfulCount,
                notHelpfulCount = review.NotHelpfulCount
            });
        }
        #endregion
    }
}