using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MiningApiController : ControllerBase
    {
        private readonly SkillSwapDbContext _db;
        public MiningApiController(SkillSwapDbContext db) => _db = db;

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _db.TblUsers.FindAsync(userId);
            var prog = await _db.UserMiningProgresses.FindAsync(userId);

            return Ok(new
            {
                balance = user.DigitalTokenBalance,
                lastEmitted = prog?.LastEmittedUtc,
                emittedToday = prog?.EmittedToday
            });
        }
    }
}