using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.NotificationVM;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class TrackNotificationsController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<TrackNotificationsController> _logger;

        public TrackNotificationsController(
            SkillSwapDbContext db,
            ILogger<TrackNotificationsController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            try
            {
                // 1) Determine current user
                var idValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idValue, out var userId))
                    return Challenge();

                IQueryable<TblNotification> query = _db.TblNotifications
                    .Where(n => n.UserId == userId);    

                // apply search if provided
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(n =>
                        n.Title.Contains(search) ||
                        n.Message.Contains(search));
                }

                var total = await query.CountAsync();

                // 2) Fetch all notifications
                var list = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 3) Project into your VM
                var items = list.Select(n =>
                {
                    return new NotificationTrackVm
                    {
                        NotificationId = n.NotificationId,
                        UserId = n.UserId,
                        Title = n.Title,
                        Message = n.Message,
                        Url = n.Url ?? "#",
                        CreatedAt = n.CreatedAt,
                        IsRead = n.IsRead,
                        IconUrl = PickIconFor(n.Title),
                    };
                }).ToList();

                var vm = new NotificationPagedVm
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    SearchTerm = search
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all notifications");
                TempData["ErrorMessage"] = "Unable to load notifications.";
                return View(new List<NotificationTrackVm>());
            }
        }

        private static string PickIconFor(string title)
        {
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