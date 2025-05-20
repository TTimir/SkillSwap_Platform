using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.UserFlag;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator, Support Agent")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class UserFlagController : Controller
    {
        private readonly IUserFlagService _svc;
        private readonly SkillSwapDbContext _ctx;
        public UserFlagController(IUserFlagService svc, SkillSwapDbContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetPendingFlagsAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load flag user details";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id, string adminReason)
        {
            try
            {
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                await _svc.DismissFlagAsync(id, adminId, adminReason);
                TempData["Success"] = "The report has been dismissed";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to done the dismiss process";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(int id, string adminReason)
        {
            try
            {
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                await _svc.RemoveUserAsync(id, adminId, adminReason);
                TempData["Success"] = "Great! You’ve successfully handled the user’s report.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to done the remove process";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> FlagUserSummary(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetFlaggedUserSummariesAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load summary";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetAllFlagHistoriesAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load history";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Admin/UserFlag/FlaggedUsers
        public async Task<IActionResult> FlaggedUsers(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetFlaggedUserSummariesAsync(page, pageSize);
                return View("FlaggedUsers", vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load flagged users";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Admin/UserFlag/FlaggedUserDetails/5
        [HttpGet]
        public async Task<IActionResult> FlaggedUserDetails(int userId, int page = 1, int pageSize = 20)
        {
            try
            {
                // 1) load the paged flags
                var vm = await _svc.GetFlagsForUserAsync(userId, page, pageSize);

                // 2) grab the username
                var user = await _ctx.TblUsers
                                     .AsNoTracking()
                                     .Where(u => u.UserId == userId)
                                     .Select(u => new { u.UserName })
                                     .FirstOrDefaultAsync();

                // 3) set the title
                ViewData["Title"] = user != null
                    ? $"Flagged User Details — {user.UserName}"
                    : "Flagged User Details";
                return View("FlaggedUserDetails", vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to flagged user details";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET /Admin/UserFlag/Dashboard
        public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.UtcNow.AddMonths(-1);
            var end = to ?? DateTime.UtcNow;
            var model = await _svc.GetUserDashboardMetricsAsync(start, end);
            return View(model);
        }

        // AJAX: /Admin/UserFlag/GetFlagsByDay?days=30
        [HttpGet]
        public async Task<IActionResult> GetFlagsByDay(int days = 30)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-days);
            var m = await _svc.GetUserDashboardMetricsAsync(start, end);
            return Ok(m.FlagTrends.Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count }));
        }

        // AJAX: /Admin/UserFlag/GetFlagResolutionBreakdown
        [HttpGet]
        public async Task<IActionResult> GetFlagResolutionBreakdown()
        {
            var m = await _svc.GetUserDashboardMetricsAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
            return Ok(m.ResolutionBreakdown.Select(r => new { action = r.Action, count = r.Count }));
        }
    }
}