using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    public class UserDashboardController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfileController> _logger;

        public UserDashboardController(SkillSwapDbContext context, ILogger<UserProfileController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (User.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
                {
                    try
                    {
                        // Set the user profile image for the layout.
                        var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                        if (user != null)
                        {
                            ViewData["UserProfileImage"] = user.ProfileImageUrl;
                            user.LastActive = DateTime.UtcNow;
                            _context.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating LastActive for user {UserId}", userId);
                    }
                }
            }

            //int userId = GetUserId();
            //if (userId != null)
            //{
            //    var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            //    ViewData["UserProfileImage"] = user?.ProfileImageUrl;
            //}
            //if (userId > 0)
            //{
            //    var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            //    if (user != null)
            //    {
            //        user.LastActive = DateTime.UtcNow; // ✅ Update LastActive
            //        _context.SaveChanges();
            //    }
            //}
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
