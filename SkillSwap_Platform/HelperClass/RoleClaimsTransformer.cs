using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.HelperClass
{
    public class RoleClaimsTransformer : IClaimsTransformation
    {
        private readonly SkillSwapDbContext _db;
        public RoleClaimsTransformer(SkillSwapDbContext db) => _db = db;

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Only add once
            if (principal.Identity is ClaimsIdentity id
             && id.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier)
             && !principal.IsInRole("Admin")    // you can adjust these guards
             && !principal.IsInRole("Moderator")
             && !principal.IsInRole("Support Agent"))
            {
                var idClaim = id.FindFirst(ClaimTypes.NameIdentifier).Value;
                if (int.TryParse(idClaim, out var userId))
                {
                    var role = await _db.TblUsers
                                        .Where(u => u.UserId == userId)
                                        .Select(u => u.Role)
                                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(role))
                    {
                        id.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                }
            }
            return principal;
        }
    }
}