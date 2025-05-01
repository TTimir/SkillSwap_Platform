using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.OfferFlag;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class OfferFlagController : Controller
    {
        private readonly IOfferFlagService _svc;
        public OfferFlagController(IOfferFlagService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var flags = await _svc.GetPendingFlagsAsync();
            return View(flags);
        }

        [HttpPost]
        public async Task<IActionResult> Dismiss(int id)
        {
            await _svc.DismissFlagAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveOffer(int id)
        {
            await _svc.RemoveOfferAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}

