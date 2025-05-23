﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
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

        #region Async Action Execution
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // If the endpoint has [AllowAnonymous], skip the filter logic.
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await next();
                return;
            }

            // Check if the controller is a Controller (for ViewData access).
            if (context.Controller is Controller controller)
            {
                // Only perform updates if the user is authenticated.
                if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int userId))
                    {
                        try
                        {
                            // Asynchronously fetch the user.
                            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                            if (user != null)
                            {
                                // Update ViewData with Profile Image.
                                controller.ViewData["UserProfileImage"] =
                                string.IsNullOrEmpty(user.ProfileImageUrl)
                                    ? null
                                    : $"/uploads/profile/{Path.GetFileName(user.ProfileImageUrl)}";

                                // Update last active time.
                                user.LastActive = DateTime.UtcNow;
                                await _context.SaveChangesAsync();

                                // Calculate online status (online if updated within 10 minutes).
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
                // If the user is not authenticated, simply continue without redirecting.
            }
            // Continue to the action.
            await next();
        }
        #endregion
    }
}
