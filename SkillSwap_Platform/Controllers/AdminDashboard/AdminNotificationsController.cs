using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminNotificationsController : Controller
    {
        private readonly SkillSwapDbContext _db;

        public AdminNotificationsController(SkillSwapDbContext db)
        {
            _db = db;
        }

        // GET: Admin/AdminNotifications/Index
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.AdminNotifications
                .OrderBy(n => n.SentAtUtc.HasValue)   // show failures first
                .ThenBy(n => n.CreatedAtUtc);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            return View(items);
        }

        // POST: Admin/AdminNotifications/Resend/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Resend(int id)
        {
            var note = await _db.AdminNotifications.FindAsync(id);
            if (note != null)
            {
                note.SentAtUtc = null;
                note.AttemptCount = 0;
                note.LastError = null;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Notification #{id} queued for resend.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
