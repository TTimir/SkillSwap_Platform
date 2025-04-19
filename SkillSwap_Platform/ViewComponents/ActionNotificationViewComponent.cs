using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.NotificationVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Humanizer;

namespace SkillSwap_Platform.ViewComponents
{
    public class ActionNotificationViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<ActionNotificationViewComponent> _logger;

        public ActionNotificationViewComponent(
            SkillSwapDbContext db,
            ILogger<ActionNotificationViewComponent> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var user = ViewContext.HttpContext.User;
                if (user?.Identity?.IsAuthenticated != true)
                    return View(Enumerable.Empty<NotificationItemVm>());

                var idValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idValue, out var userId))
                    return View(Enumerable.Empty<NotificationItemVm>());

                // fetch latest 5 action notifications
                var list = await _db.TblNotifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // … your existing fetch & projection …
                var vm = list.Select(n => {
                    var x = new NotificationTrackVm
                    {
                        NotificationId = n.NotificationId,
                        UserId = n.UserId,
                        Title = n.Title,
                        Message = !string.IsNullOrEmpty(n.Message) && n.Message.Length > 80
                            ? n.Message.Substring(0, 80).TrimEnd() + "…"
                            : n.Message,
                        Url = n.Url,
                        CreatedAt = n.CreatedAt,
                        IsRead = n.IsRead,
                    };
                    // set icon based on title
                    x.IconUrl = PickIconFor(n.Title);
                    return x;
                }).ToList();

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading action notifications");
                return View(Enumerable.Empty<NotificationItemVm>());
            }
        }

        private string PickIconFor(string title)
        {
            // simple keyword → icon mapping; adjust paths & keywords as needed
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Offer"] = "/template_assets/images/resource/notif-5.png",
                ["Profile"] = "/template_assets/images/resource/notif-1.png",
                ["Contract"] = "/template_assets/images/resource/notif-4.png",
                ["Resource"] = "/template_assets/images/resource/notif-3.png",
                ["Password"] = "/template_assets/images/resource/notif-2.png"
            };

            foreach (var kv in map)
                if (title.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;

            return "/template_assets/images/resource/notif-1.png";
        }
    }
}