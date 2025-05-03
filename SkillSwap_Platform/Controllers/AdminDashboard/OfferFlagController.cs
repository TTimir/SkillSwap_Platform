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
            var vm = await _svc.GetPendingFlagsAsync(page, pageSize);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            var vm = await _svc.GetProcessedFlagsAsync(page, pageSize);
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Dismiss(int id, string reason)
        {
            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminUserId = int.TryParse(adminIdStr, out var a) ? a
                            : throw new Exception("Cannot identify admin user.");

            await _svc.DismissFlagAsync(id, adminUserId, reason);
            TempData["Success"] = "Flag dismissed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveOffer(int id, string reason)
        {
            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminUserId = int.TryParse(adminIdStr, out var a) ? a
                            : throw new Exception("Cannot identify admin user.");

            await _svc.RemoveOfferAsync(id, adminUserId, reason);
            TempData["Success"] = "Offer removed.";
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/OfferFlag/Summary
        public async Task<IActionResult> FlaggedOfferSummary(int page = 1, int pageSize = 20)
        {
            var vm = await _svc.GetFlaggedOfferSummariesAsync(page, pageSize);
            return View(vm);
        }

        // GET /Admin/OfferFlag/Details/123
        public async Task<IActionResult> FlaggedOfferDetails(int offerId, int page = 1, int pageSize = 20)
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
    }
}

