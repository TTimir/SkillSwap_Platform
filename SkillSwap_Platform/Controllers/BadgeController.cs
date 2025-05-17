using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    public class BadgeController : Controller
    {
        private readonly SkillSwapDbContext _ctx;
        public BadgeController(SkillSwapDbContext ctx) => _ctx = ctx;

        [HttpGet]
        public async Task<IActionResult> GetMyBadges()
        {
            var userId = GetCurrentUserId();
            var awards = await _ctx.TblBadgeAwards
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.AwardedAt)
                .ToListAsync();
            return Ok(awards);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new Exception("User ID not found in claims.");
        }
    }
}
