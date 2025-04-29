using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.ViewComponents
{
    public class NotificationBadgeViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        public NotificationBadgeViewComponent(SkillSwapDbContext db)
            => _db = db;

        /// <summary>
        /// type = "action" or "message"
        /// </summary>
        public async Task<IViewComponentResult> InvokeAsync(string type)
        {
            var user = HttpContext.User;
            if (!user.Identity.IsAuthenticated)
                return Content(string.Empty);

            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(id, out var userId))
                return Content(string.Empty);

            int unread = type switch
            {
                "message" => await _db.TblMessages.CountAsync(m => m.ReceiverUserId == userId && !m.IsRead),
                _ => 0
            };

            if (unread == 0)
                return Content(string.Empty);

            return View(unread);
        }
    }
}