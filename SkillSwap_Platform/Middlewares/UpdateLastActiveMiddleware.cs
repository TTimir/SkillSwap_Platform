using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Middlewares;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Middlewares
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class UpdateLastActiveMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly TimeSpan _minUpdateInterval = TimeSpan.FromMinutes(1);

        public UpdateLastActiveMiddleware(RequestDelegate next)
            => _next = next;

        public async Task Invoke(HttpContext context, SkillSwapDbContext db)
        {
            var user = context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                // 1) get userId from the claims
                var uidClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(uidClaim, out var uid))
                {
                    // 2) fetch the last active timestamp
                    var lastActive = await db.TblUsers
                                             .Where(u => u.UserId == uid)
                                             .Select(u => (DateTime?)u.LastActive)
                                             .FirstOrDefaultAsync();

                    var now = DateTime.UtcNow;
                    if (lastActive == null
                        || now - lastActive > _minUpdateInterval)
                    {
                        // 3) raw SQL update: no concurrency token, no exception
                        await db.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE dbo.tblUsers
                               SET LastActive = {now}
                             WHERE UserId     = {uid};
                        ");
                    }
                }
            }

            // 4) continue the pipeline
            await _next(context);
        }
    }

    public static class UpdateLastActiveMiddlewareExtensions
    {
        public static IApplicationBuilder UseUpdateLastActive(
            this IApplicationBuilder builder)
            => builder.UseMiddleware<UpdateLastActiveMiddleware>();
    }
}