using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.Message.SensitiveWord;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class SensitiveWordsController : Controller
    {
        private readonly ISensitiveWordService _svc;
        private readonly ILogger<SensitiveWordsController> _log;

        public SensitiveWordsController(
            ISensitiveWordService svc,
            ILogger<SensitiveWordsController> log)
        {
            _svc = svc;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            try
            {
                var model = await _svc.GetPagedAsync(page, pageSize);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load words";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public IActionResult Create()
            => View(new SensitiveWordVm());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SensitiveWordVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                await _svc.CreateAsync(vm);
                TempData["Success"] = "Sensitive word added.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Create failed {@Vm}", vm);
                ModelState.AddModelError("", "Failed to add entry.");
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _svc.GetByIdAsync(id);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SensitiveWordVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                await _svc.UpdateAsync(vm);
                TempData["Success"] = "Sensitive word updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Edit failed {@Vm}", vm);
                ModelState.AddModelError("", "Failed to save changes.");
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _svc.DeleteAsync(id);
                TempData["Success"] = "Sensitive word deleted.";
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Delete failed {Id}", id);
                TempData["Error"] = "Failed to delete entry.";
                return RedirectToAction("EP500", "EP");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}