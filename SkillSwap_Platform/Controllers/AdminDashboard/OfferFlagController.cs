using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.OfferFlag;
using SkillSwap_Platform.Services.AdminControls.UserFlag;
using System.Drawing;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class OfferFlagController : Controller
    {
        private readonly IOfferFlagService _svc;
        private readonly SkillSwapDbContext _ctx;
        public OfferFlagController(IOfferFlagService svc, SkillSwapDbContext ctx)
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
                TempData["Error"] = "Unable to load flagged offers.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetProcessedFlagsAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load flag history.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Dismiss(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException("Reason is required");

                var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var adminUserId = int.TryParse(adminIdStr, out var a) ? a
                                : throw new Exception("Cannot identify admin user.");

                await _svc.DismissFlagAsync(id, adminUserId, reason);
                TempData["Success"] = "Flag dismissed.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to dismiss flag.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveOffer(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException("Reason is required");

                var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var adminUserId = int.TryParse(adminIdStr, out var a) ? a
                                : throw new Exception("Cannot identify admin user.");

                await _svc.RemoveOfferAsync(id, adminUserId, reason);
                TempData["Success"] = "Offer removed.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to remove offer.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET /Admin/OfferFlag/Summary
        public async Task<IActionResult> FlaggedOfferSummary(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetFlaggedOfferSummariesAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load summary.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET /Admin/OfferFlag/Details/123
        public async Task<IActionResult> FlaggedOfferDetails(int offerId, int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _svc.GetFlagsForOfferAsync(offerId, page, pageSize);

                var title = await _ctx.TblOffers
                                     .Where(o => o.OfferId == offerId)
                                     .Select(o => o.Title)
                                     .FirstOrDefaultAsync();
                ViewData["Title"] = title != null
                    ? $"Flagged Offer Details — “{title}”"
                    : "Flagged Offer Details";

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load details.";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to)
        {
            try
            {
                var start = from ?? DateTime.UtcNow.AddMonths(-1);
                var end = to ?? DateTime.UtcNow;
                var model = await _svc.GetDashboardMetricsAsync(start, end);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load dashboard.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFlagsByDay([FromQuery] int days = 30)
        {
            try
            {
                var end = DateTime.UtcNow;
                var start = end.AddDays(-days);
                // We only need the trends, so call the service for that window:
                var metrics = await _svc.GetDashboardMetricsAsync(start, end);
                // Map to simple DTO
                var result = metrics.FlagTrends
                    .Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count });
                return Ok(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading data";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFlagResolutionBreakdown()
        {
            try
            {
                var oneYearAgo = DateTime.UtcNow.AddYears(-1);
                // Use a broad window so you get all actions
                var metrics = await _svc.GetDashboardMetricsAsync(oneYearAgo, DateTime.UtcNow);
                var result = metrics.ResolutionBreakdown
                    .Select(r => new { action = r.Action, count = r.Count });
                return Ok(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading data";
                return RedirectToAction("EP500", "EP");
            }
        }
    }
}

