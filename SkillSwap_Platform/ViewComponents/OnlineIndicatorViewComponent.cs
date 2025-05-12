using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.ViewComponents
{
    public class OnlineIndicatorViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        private static readonly TimeSpan _threshold = TimeSpan.FromMinutes(1);

        public OnlineIndicatorViewComponent(SkillSwapDbContext db)
            => _db = db;

        /// <summary>
        /// Renders a green “online” dot if that user’s LastActive is within the threshold.
        /// If userId is null, uses the current logged-in user.
        /// </summary>
        public async Task<IViewComponentResult> InvokeAsync(int? userId = null)
        {
            // 1) if no userId passed, try the current logged-in user
            if (!userId.HasValue && HttpContext.User.Identity?.IsAuthenticated == true)
            {
                // cast to ClaimsPrincipal before calling FindFirst
                var cp = HttpContext.User as ClaimsPrincipal;
                var idClaim = cp?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var me)) userId = me;
            }

            bool isOnline = false;
            if (userId.HasValue)
            {
                var last = await _db.TblUsers
                                    .AsNoTracking()
                                    .Where(u => u.UserId == userId.Value)
                                    .Select(u => u.LastActive)
                                    .FirstOrDefaultAsync();

                isOnline = last.HasValue
                           && DateTime.UtcNow - last.Value < _threshold;
            }

            return View(isOnline);
        }
    }
}