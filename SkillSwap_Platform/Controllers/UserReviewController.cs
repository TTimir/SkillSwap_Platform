using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Controllers
{
    public class UserReviewController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserOfferManageController> _logger;
        public IActionResult Index()
        {
            return View();
        }

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
                    if (offer == null) return NotFound("Offer not found");

                    var user = offer.User;
                    if (user == null) return NotFound("User not found");

                    // ✅ Save the new review
                    var review = new TblReview
                    {
                        OfferId = offerId,
                        UserId = user.UserId,
                        Rating = rating
                    };
                    _context.TblReviews.Add(review);
                    await _context.SaveChangesAsync();

                    // Update the user's statistics (job success and recommendation percentages).
                    await UpdateUserStats(user.UserId);

                    // Commit transaction if all steps succeed.
                    await transaction.CommitAsync();
                    return Ok("Review submitted successfully");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error submitting review for offer {OfferId}", offerId);
                    return StatusCode(500, "An error occurred while submitting your review.");
                }
            }
        }

        #region Helper Methods
        private async Task UpdateUserStats(int userId)
        {
            try
            {
                var user = await _context.TblUsers
                .Include(u => u.TblReviewReviewees) // ✅ Includes reviews
                .Include(u => u.TblExchangeLastStatusChangedByNavigations) // ✅ First set of exchanges
                .Include(u => u.TblExchangeRequesters) // ✅ Second set of exchanges
                .Include(u => u.TblOffers) // ✅ User's offers
                .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null) return;

                // Calculate Recommended Percentage (Total Positive Reviews / Total Reviews)
                int totalReviews = user.TblReviewReviewees.Count();
                int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);
                user.RecommendedPercentage = totalReviews > 0 ? (positiveReviews * 100.0 / totalReviews) : 0;

                // Combine both exchange sets for success rate calculation
                var allExchanges = user.TblExchangeLastStatusChangedByNavigations
                    .Concat(user.TblExchangeRequesters)
                    .ToList();

                int totalExchanges = allExchanges.Count;
                int successfulExchanges = allExchanges.Count(e => e.IsSuccessful); // ✅ Count successful ones

                user.JobSuccessRate = totalExchanges > 0 ? (successfulExchanges * 100.0 / totalExchanges) : 0;

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
