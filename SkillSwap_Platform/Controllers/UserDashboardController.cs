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

            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                try
                {
                    // Set profile image for layout (read-only)
                    var user = _context.TblUsers.AsNoTracking().FirstOrDefault(u => u.UserId == userId);
                    ViewData["UserProfileImage"] = user?.ProfileImageUrl;

                    // Update LastActive timestamp
                    var userToUpdate = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                    if (userToUpdate != null)
                    {
                        userToUpdate.LastActive = DateTime.UtcNow;
                        _context.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnActionExecuting for user {UserId}", userId);
                }
            }
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
