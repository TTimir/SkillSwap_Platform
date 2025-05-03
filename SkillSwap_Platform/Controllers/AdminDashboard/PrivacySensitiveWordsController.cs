using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.PrivacyWord;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class PrivacySensitiveWordsController : Controller
    {
        private readonly IPrivacySensitiveWordService _svc;
        private readonly ILogger<PrivacySensitiveWordsController> _log;

        public PrivacySensitiveWordsController(
            IPrivacySensitiveWordService svc,
            ILogger<PrivacySensitiveWordsController> log)
        {
            _svc = svc;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var model = await _svc.GetPagedAsync(page, pageSize);
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new PrivacySensitiveWordVm());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PrivacySensitiveWordVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                await _svc.CreateAsync(vm);
                TempData["Success"] = "Privacy Sensitive Word added.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Create failed {@Vm}", vm);
                ModelState.AddModelError("", "Unable to add word.");
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _svc.GetByIdAsync(id);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PrivacySensitiveWordVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                await _svc.UpdateAsync(vm);
                TempData["Success"] = "Privacy Sensitive Word updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Edit failed {@Vm}", vm);
                ModelState.AddModelError("", "Unable to update word.");
                return View(vm);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _svc.DeleteAsync(id);
                TempData["Success"] = "Privacy Sensitive Word deleted.";
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Delete failed {Id}", id);
                TempData["Error"] = "Unable to delete word.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}