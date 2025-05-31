using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        //[ProducesResponseType(StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status401Unauthorized)]
        //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MiningStatusDto>> Status(CancellationToken ct)
        {
            try
            {
                int userId = GetCurrentUserId();

                // Single round-trip via projection
                var data = await _db.TblUsers
                                .Where(u => u.UserId == userId)
                                .Select(u => new
                                {
                                    u.DigitalTokenBalance,
                                    prog = _db.UserMiningProgresses
                            .Where(p => p.UserId == userId)
                            .Select(p => new
                            {
                                LastEmittedUtc = p.LastEmittedUtc,
                                EmittedToday = p.EmittedToday
                            })
                            .FirstOrDefault()
                                })
                    .FirstOrDefaultAsync(ct);

                if (data == null)
                    return Unauthorized();


                return Ok(new MiningStatusDto
                {
                    Balance = data.DigitalTokenBalance,
                    LastEmittedUtc = data.prog?.LastEmittedUtc,
                    LastEmittedLocal = data.prog?.LastEmittedUtc.ToLocalTime(),
                    EmittedToday = data.prog != null
                                        ? Convert.ToInt32(data.prog.EmittedToday)
                                        : 0
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return RedirectToAction("EP500", "EP");
            }
        }

        #region Helpers
        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
            return id;
        }
        #endregion
    }

    /// <summary>
    /// DTO returned by MiningApiController.Status
    /// </summary>
    public class MiningStatusDto
    {
        public decimal Balance { get; set; }
        public DateTime? LastEmittedUtc { get; set; }
        public DateTime? LastEmittedLocal { get; set; }
        public int EmittedToday { get; set; }
    }
}