using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.ViewComponents
{
    public class TopTierBadgeViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        public TopTierBadgeViewComponent(SkillSwapDbContext db) => _db = db;

        public async Task<IViewComponentResult> InvokeAsync(int userId)
        {
            // Fetch the highest-ID badge (9–11) for this user
            var award = await _db.TblBadgeAwards
                .Where(a => a.UserId == userId && (a.BadgeId >= 9 && a.BadgeId <= 11))
                .Include(a => a.Badge)
                .OrderByDescending(a => a.BadgeId)
                .FirstOrDefaultAsync();

            if (award is null)
                return Content(""); // nothing to render

            var vm = new BadgeAwardVM
            {
                BadgeId = award.BadgeId,
                Name = award.Badge.Name,
                Description = award.Badge.Description,
                IconUrl = award.Badge.IconUrl,
                Level = int.Parse(award.Badge.Tier),
                AwardedAt = award.AwardedAt
            };

            return View(vm); // will look for Views/Shared/Components/TopTierBadge/Default.cshtml
        }
    }
}