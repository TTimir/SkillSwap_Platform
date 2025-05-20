using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Faqs;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class FaqAdminController : Controller
    {
        private readonly IFaqService _faqs;
        public FaqAdminController(IFaqService faqs) => _faqs = faqs;

        // list only active, not deleted, in the selected section
        public async Task<IActionResult> Index(string section = "HowItWorks")
        {
            ViewBag.Section = section;
            var items = await _faqs.GetBySectionAsync(section);
            return View(items);
        }

        // seed Create with Section + next SortOrder
        public async Task<IActionResult> Create(string section)
        {
            if (string.IsNullOrEmpty(section))
                return RedirectToAction(nameof(Index));

            // pull all existing FAQs in this section
            var existing = await _faqs.GetBySectionAsync(section);

            // safely get highest SortOrder (0 when empty) and bump it
            var next = existing
                .Select(f => f.SortOrder)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var vm = new TblSkillSwapFaq
            {
                Section = section,
                SortOrder = next,
                IsActive = true,
                CreatedDate = DateTime.UtcNow    // if your model has CreatedDate
            };

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TblSkillSwapFaq model)
        {
            if (!ModelState.IsValid)
                return View(model);
            try
            {
                await _faqs.AddAsync(model);
                TempData["Success"] = "FAQ added.";
                return RedirectToAction(nameof(Index), new { section = model.Section });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not add FAQ.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // load for editing (ignores deleted)
        public async Task<IActionResult> Edit(int id)
        {
            var faq = await _faqs.GetByIdAsync(id);
            if (faq == null)
                return NotFound();

            return View(faq);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TblSkillSwapFaq model)
        {
            if (!ModelState.IsValid)
                return View(model);
            try
            {
                await _faqs.UpdateAsync(model);
                return RedirectToAction(nameof(Index), new { section = model.Section });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not update FAQ.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // soft‐delete
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var faq = await _faqs.GetByIdAsync(id);
            if (faq == null)
                return RedirectToAction("EP404", "EP");

            try
            {
                await _faqs.DeleteAsync(id);
                return RedirectToAction(nameof(Index), new { section = faq.Section });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not delete FAQ.";
                return RedirectToAction("EP500", "EP");
            }
        }
    }
}
