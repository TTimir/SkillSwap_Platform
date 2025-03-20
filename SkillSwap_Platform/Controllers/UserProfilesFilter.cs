using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    public class UserProfilesFilter : ActionFilterAttribute
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfilesFilter> _logger;

        // Inject the ApplicationDbContext and ILogger via DI
        public UserProfilesFilter(SkillSwapDbContext context, ILogger<UserProfilesFilter> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // If the action (or controller) allows anonymous access, skip this filter.
            if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
            {
                base.OnActionExecuting(context);
                return;
            }

            if (context.Controller is Controller controller)
            {
                if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int userId))
                    {
                        try
                        {
                            var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                            if (user != null)
                            {
                                // Update Profile Image in ViewData
                                controller.ViewData["UserProfileImage"] = user.ProfileImageUrl;

                                // Update last active time
                                user.LastActive = DateTime.UtcNow;
                                _context.SaveChanges();

                                // Calculate online status (e.g., within the last 10 minutes)
                                bool isOnline = (DateTime.UtcNow - user.LastActive.Value).TotalMinutes < 10;
                                controller.ViewData["IsOnline"] = isOnline;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating LastActive for user {UserId}", userId);
                        }
                    }
                }
            }
            else
            {
                // If not authenticated, redirect to Login.
                context.Result = new RedirectToActionResult("Login", "Home", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
