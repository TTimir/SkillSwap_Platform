using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Middlewares
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class UpdateLastActiveMiddleware
    {
        private readonly RequestDelegate _next;

        public UpdateLastActiveMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, SkillSwapDbContext dbContext)
        {
            if (context.User?.Identity != null && context.User.Identity.IsAuthenticated)
            {
                int userId;
                if (int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId))
                {
                    var user = await dbContext.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.LastActive = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                    }
                }
             }

            await _next(context);
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class UpdateLastActiveMiddlewareExtensions
    {
        public static IApplicationBuilder UseUpdateLastActiveMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UpdateLastActiveMiddleware>();
        }
    }
}
