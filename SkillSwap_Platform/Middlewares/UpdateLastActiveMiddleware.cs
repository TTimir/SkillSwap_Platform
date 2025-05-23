﻿using System.Security.Claims;
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
                var uidClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(uidClaim, out var uid))
                {
                    var lastActive = await db.TblUsers
                                             .Where(u => u.UserId == uid)
                                             .Select(u => u.LastActive)
                                             .FirstOrDefaultAsync();

                    if (lastActive == null
                        || DateTime.UtcNow - lastActive > _minUpdateInterval)
                    {
                        // attach only LastActive
                        var stub = new TblUser { UserId = uid, LastActive = DateTime.UtcNow };
                        db.TblUsers.Attach(stub);
                        db.Entry(stub).Property(u => u.LastActive).IsModified = true;
                        await db.SaveChangesAsync();
                    }
                }
            }

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