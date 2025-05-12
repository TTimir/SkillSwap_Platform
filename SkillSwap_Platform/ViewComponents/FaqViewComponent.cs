using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.Faqs;

namespace SkillSwap_Platform.ViewComponents
{
    public class FaqViewComponent : ViewComponent
    {
        private readonly IFaqService _faqs;
        public FaqViewComponent(IFaqService faqs) => _faqs = faqs;

        public async Task<IViewComponentResult> InvokeAsync(string section)
        {
            var items = await _faqs.GetBySectionAsync(section);
            return View(items);
        }
    }
}