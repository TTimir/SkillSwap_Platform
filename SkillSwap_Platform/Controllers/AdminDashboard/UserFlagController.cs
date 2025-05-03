using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.UserFlag;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
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
            var vm = await _svc.GetPendingFlagsAsync(page, pageSize);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id, string adminReason)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            await _svc.DismissFlagAsync(id, adminId, adminReason);
            TempData["Success"] = "The report has been dismissed";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(int id, string adminReason)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            await _svc.RemoveUserAsync(id, adminId, adminReason);
            TempData["Success"] = "Great! You’ve successfully handled the user’s report.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> FlagUserSummary(int page = 1, int pageSize = 20)
        {
            var vm = await _svc.GetFlaggedUserSummariesAsync(page, pageSize);
            return View(vm);
        }

        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            var vm = await _svc.GetAllFlagHistoriesAsync(page, pageSize);
            return View(vm);
        }

        // GET: /Admin/UserFlag/FlaggedUsers
        public async Task<IActionResult> FlaggedUsers(int page = 1, int pageSize = 20)
        {
            var vm = await _svc.GetFlaggedUserSummariesAsync(page, pageSize);
            return View("FlaggedUsers", vm);
        }

        // GET: /Admin/UserFlag/FlaggedUserDetails/5
        [HttpGet]
        public async Task<IActionResult> FlaggedUserDetails(int userId, int page = 1, int pageSize = 20)
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
    }
}