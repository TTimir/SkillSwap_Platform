using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.NotificationVM;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.ViewComponents
{
    public class MessageNotificationViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<MessageNotificationViewComponent> _logger;

        public MessageNotificationViewComponent(SkillSwapDbContext db, ILogger<MessageNotificationViewComponent> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                // 1) grab the ClaimsPrincipal
                var user = ViewContext.HttpContext.User as ClaimsPrincipal;
                if (user == null || !user.Identity?.IsAuthenticated == true)
                    return View(Enumerable.Empty<NotificationItemVm>());

                // 2) pull the user id
                var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idClaim, out var userId))
                    return View(Enumerable.Empty<NotificationItemVm>());

                // 3) find each sender and their most recent send time
                var partners = await _db.TblMessages
                    .Where(m => m.ReceiverUserId == userId)
                    .GroupBy(m => m.SenderUserId)
                    .Select(g => new { SenderId = g.Key, LastDate = g.Max(m => m.SentDate) })
                    .OrderByDescending(x => x.LastDate)
                    .Take(5)
                    .ToListAsync();

                var notifications = new List<NotificationItemVm>(partners.Count);

                // 4) for each partner, load their single most recent message
                foreach (var p in partners)
                {
                    var msg = await _db.TblMessages
                        .Where(m => m.ReceiverUserId == userId && m.SenderUserId == p.SenderId)
                        .OrderByDescending(m => m.SentDate)
                        .Include(m => m.SenderUser)
                        .FirstOrDefaultAsync();

                    if (msg == null) continue;

                    var plain = Regex.Replace(msg.Content, "<.*?>", String.Empty);
                    var snippet = plain.Length > 80
                                  ? plain.Substring(0, 80).TrimEnd() + "…"
                                  : plain;

                    notifications.Add(new NotificationItemVm
                    {
                        SenderUserId = msg.SenderUserId,
                        SenderName = msg.SenderUser.UserName,
                        ProfileImageUrl = msg.SenderUser.ProfileImageUrl ?? "/template_assets/images/No_Profile_img.png",
                        HtmlContent = msg.Content,
                        PreviewText = snippet,
                        SentDate = msg.SentDate,
                        IsRead = msg.IsRead,
                        TimeAgo = msg.SentDate.Humanize(false, DateTime.UtcNow)
                    });
                }

                return View(notifications);
            }
            catch (Exception ex)
            {
                // log if you have ILogger injected
                return View(Enumerable.Empty<NotificationItemVm>());
            }
        }
    }
}