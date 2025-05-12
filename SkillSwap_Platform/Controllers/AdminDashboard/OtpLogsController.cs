using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class OtpLogsController : Controller
    {
        private readonly SkillSwapDbContext _db;
        public OtpLogsController(SkillSwapDbContext db) => _db = db;

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                // 1) Build the grouped‐summary query
                var summaryQuery = _db.OtpAttempts
                    .Where(a => !a.WasSuccessful)
                    .GroupBy(a => new { a.UserId, a.UserName })
                    .Select(g => new OtpUserSummaryVm
                    {
                        UserId = g.Key.UserId,
                        UserName = g.Key.UserName,
                        FailedCount = g.Count(),
                        LastAttemptAt = g.Max(a => a.AttemptedAt)
                    });

                // 2) Count total groups
                var total = await summaryQuery.CountAsync();

                // 3) Fetch just this page, ordering by last attempt desc
                var items = await summaryQuery
                    .OrderByDescending(x => x.LastAttemptAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // mark “new” if within last 15 minutes:
                var cutoff = DateTime.UtcNow.AddMinutes(-15);
                items.ForEach(u => u.HasRecentFailure = u.LastAttemptAt >= cutoff);

                ViewBag.OtpFailureUserCount = total;

                // 4) Wrap in PagedResult<T>
                var model = new PagedResult<OtpUserSummaryVm>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    Items = items
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load OTP logs right now.";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> Details(int userId, int page = 1, int pageSize = 10)
        {
            try
            {
                var query = _db.OtpAttempts
                               .Where(a => a.UserId == userId)
                               .OrderByDescending(a => a.AttemptedAt);

                var total = await query.CountAsync();
                var items = await query
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();

                var model = new PagedResult<OtpAttempt>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    Items = items
                };
                ViewBag.UserId = userId;
                ViewBag.UserName = (await _db.OtpAttempts
                                           .Where(a => a.UserId == userId)
                                           .Select(a => a.UserName)
                                           .FirstOrDefaultAsync()) ?? "Unknown";

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load user OTP details right now.";
                return RedirectToAction("EP500", "EP");
            }
        }
    }
}