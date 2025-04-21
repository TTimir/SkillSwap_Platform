using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        public CategoryMenuViewComponent(SkillSwapDbContext db)
            => _db = db;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // read current filter from querystring
            var current = HttpContext.Request.Query["category"].ToString();

            // fetch distinct categories
            var cats = await _db.TblOffers
                .Where(o => o.IsActive && !o.IsDeleted)
                .Select(o => o.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var vm = cats
                .Select(c => new CategoryItemVm
                {
                    Value = c,
                    Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(c),
                    Active = string.Equals(c, current, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            return View("~/Views/Shared/Components/CategoryMenu/Default.cshtml", vm);
        }
    }
}

