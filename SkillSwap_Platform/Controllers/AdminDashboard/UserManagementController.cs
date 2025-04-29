using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.UserManagement;
using System.Drawing;
using System.Drawing.Printing;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class UserManagementController : Controller
    {
        private readonly IUserManagmentService _usermanageService;
        private readonly ILogger<UserManagementController> _logger;
        private readonly SkillSwapDbContext _db;
        private const int PageSize = 10;

        public UserManagementController(
            IUserManagmentService usermanage,
            ILogger<UserManagementController> logger,
            SkillSwapDbContext db)
        {
            _usermanageService = usermanage;
            _logger = logger;
            _db = db;
        }

        // GET: /Admin/UserManagement/Manage
        public async Task<IActionResult> Manage(int page = 1)
        {
            var activeUsers = await _usermanageService.GetActiveUsersAsync(page, PageSize);
            return View(activeUsers);
        }

        // GET: /Admin/UserManagement/Index
        public async Task<IActionResult> Index(int page = 1)
        {
            var heldUsers = await _usermanageService.GetHeldUsersAsync(page, PageSize);
            return View(heldUsers);
        }

        // POST: /Admin/UserManagement/Hold/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold(int id, string category, string reason, DateTime? heldUntil)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A reason is required.";
                return RedirectToAction(nameof(Manage));
            }
            if (string.IsNullOrWhiteSpace(category))
            {
                TempData["Error"] = "Please select a category.";
                return RedirectToAction(nameof(Manage));
            }

            if (!heldUntil.HasValue)
                heldUntil = DateTime.UtcNow.AddMonths(3);

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (await _usermanageService.HoldUserAsync(id, category, reason, heldUntil, adminId))
                TempData["Success"] = "User held until " + heldUntil.Value.ToString("dd MMMM, yyyy hh:mm tt");
            else
                TempData["Error"] = "Failed to hold user.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/UserManagement/Release/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please provide a reason for release.";
                return RedirectToAction(nameof(Index));
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            bool ok = await _usermanageService.ReleaseUserAsync(id, reason, adminId);
            TempData[ok ? "Success" : "Error"] = ok
                ? "User has been released."
                : "Failed to release user.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> HoldHistory(int page = 1, int? userId = null)
        {
            // 1) fetch a PagedResult<HoldHistoryEntryDto>
            var paged = await _usermanageService
                .GetHoldHistoryAsync(page, PageSize, userId);

            // 2) optional: still fetch user name for the header
            if (userId.HasValue)
            {
                var user = await _db.TblUsers.FindAsync(userId.Value);
                if (user == null) return NotFound();
                ViewBag.UserName = $"{user.FirstName} {user.LastName}";
            }

            // 3) pass the PagedResult straight into the view
            return View(paged);
        }

    }
}
