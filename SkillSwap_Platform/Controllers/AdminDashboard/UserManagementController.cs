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
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
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
        public async Task<IActionResult> Manage(int page = 1, string term = "")
        {
            try
            {
                ViewBag.Term = term;
                var activeUsers = await _usermanageService.GetActiveUsersAsync(page, PageSize, term);
                return View(activeUsers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load user details";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Admin/UserManagement/Index
        public async Task<IActionResult> Index(int page = 1, string term = "")
        {
            try
            {
                ViewBag.Term = term;
                var heldUsers = await _usermanageService.GetHeldUsersAsync(page, PageSize, term);
                return View(heldUsers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load user details";
                return RedirectToAction("EP500", "EP");
            }
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

            var untilUtc = (heldUntil ?? DateTime.UtcNow.AddMonths(1))
                  .ToUniversalTime();

            try
            {
                var utcHeld = DateTime.SpecifyKind(untilUtc, DateTimeKind.Utc);
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                var localHeld = TimeZoneInfo.ConvertTimeFromUtc(utcHeld, istZone);

                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (await _usermanageService.HoldUserAsync(id, category, reason, heldUntil, adminId))
                    TempData["Success"] = $"User held until {localHeld.ToLocalTime().ToString("dd MMMM, yyyy hh:mm tt")} IST";
                else
                    TempData["Error"] = "Failed to hold user.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to hold user.";
                _logger.LogError(ex, "Error holding user {UserId}", id);
                return RedirectToAction("EP500", "EP");
            }
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

            try
            {
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                bool ok = await _usermanageService.ReleaseUserAsync(id, reason, adminId);
                TempData[ok ? "Success" : "Error"] = ok
                    ? "User has been released."
                    : "Failed to release user.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to hold user.";
                _logger.LogError(ex, "Error holding user {UserId}", id);
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> HoldHistory(int page = 1, int? userId = null)
        {
            try
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
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to hold user.";
                _logger.LogError(ex, "Error holding user");
                return RedirectToAction("EP500", "EP");
            }
        }

    }
}
