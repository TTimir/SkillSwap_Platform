using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private readonly SkillSwapDbContext _context;

        public UserProfileController(SkillSwapDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            if (userId != null)
            {
                var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                ViewData["UserProfileImage"] = user?.ProfileImageUrl;
            }
            if (userId > 0)
            {
                var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                if (user != null)
                {
                    user.LastActive = DateTime.UtcNow; // ✅ Update LastActive
                    _context.SaveChanges();
                }
            }
        }
        public async Task<IActionResult> Index()
        {
            // Retrieve current user's ID from claims.
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Load the user and related data.
            var user = await _context.TblUsers
                .Include(u => u.TblEducations)
                .Include(u => u.TblExperiences)
                .Include(u => u.TblLanguages)
                .Include(u => u.TblUserCertificateUsers)
                .Include(u => u.TblReviewReviewees) // ✅ Fetch reviews received by the user
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            // Ensure there's at least one review
            double recommendedPercentage = 0;
            if (user.TblReviewReviewees.Any())
            {
                int totalReviews = user.TblReviewReviewees.Count();
                int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);

                decimal? percentage = (decimal?)positiveReviews / totalReviews * 100;
                recommendedPercentage = percentage.HasValue ? (double)percentage.Value : 0;
            }

            // 🔥 Fetch the most recent completed exchange (job)
            var lastCompletedExchange = await _context.TblExchanges
                .Where(e => e.RequesterId == userId || e.LastStatusChangedBy == userId) // Job done by the user
                .Where(e => e.Status == "Completed") // Only completed transactions
                .OrderByDescending(e => e.LastStatusChangeDate) // Get the most recent one
                .FirstOrDefaultAsync();

            // 🕒 Calculate days since last completed job
            int lastDeliveryDays = lastCompletedExchange != null
                ? (DateTime.UtcNow - lastCompletedExchange.LastStatusChangeDate).Days 
                : -1; // Default if no completed jobs

            // Prepare the view model.
            var model = new UserProfileVM
            {
                User = user,
                Educations = user.TblEducations.OrderByDescending(e => e.StartDate).ToList(),
                Experiences = user.TblExperiences.OrderByDescending(e => e.StartDate).ToList(),
                Languages = user.TblLanguages.ToList(),
                Certificates = user.TblUserCertificateUsers.ToList(),
                LastExchangeDays = lastDeliveryDays,
                RecommendedPercentage = recommendedPercentage
            };

            return View(model);
        }
    }
}