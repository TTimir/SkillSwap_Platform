using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class MinimumTierHandler : AuthorizationHandler<MinimumTierRequirement>
    {
        private readonly ISubscriptionService _subs;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MinimumTierHandler(
            ISubscriptionService subs,
            IHttpContextAccessor httpContextAccessor)
        {
            _subs = subs;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext ctx,
            MinimumTierRequirement req)
        {
            var userId = GetUserId(ctx);
            if (userId == null)
                return;

            var tier = await _subs.GetTierAsync(userId ?? 0);
            if (tier >= req.RequiredTier)
                ctx.Succeed(req);
        }


        private int? GetUserId(AuthorizationHandlerContext ctx)
        {
            // 1) Try session:
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session != null)
            {
                var temp = httpContext.Session.GetInt32("TempUserId");
                if (temp.HasValue)
                    return temp.Value;
            }

            // 2) Fallback to claims:
            var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out var id))
                return id;

            return null;
        }
    }
}
