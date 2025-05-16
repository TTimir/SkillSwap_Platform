using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class SubscriptionRenewalService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionRenewalService> _logger;

        public SubscriptionRenewalService(IServiceScopeFactory scopeFactory,
                                          ILogger<SubscriptionRenewalService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // run once a day
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                // find ended subscriptions that still wanted auto-renew
                var toRenew = await db.Subscriptions
                    //.Where(s => s.EndDate <= DateTime.UtcNow && s.IsAutoRenew)
                    .ToListAsync(stoppingToken);

                foreach (var sub in toRenew)
                {
                    try
                    {
                        var start = sub.EndDate;
                        var end = sub.BillingCycle == "yearly"
                                      ? start.AddYears(1)
                                      : start.AddMonths(1);

                        await service.UpsertAsync(sub.UserId,
                                                  sub.PlanName,
                                                  sub.BillingCycle,
                                                  start, end);

                        _logger.LogInformation("Auto-renewed {Plan} for user {UserId}",
                            sub.PlanName, sub.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-renew for user {UserId}", sub.UserId);
                    }
                }
            }
        }
    }
}
