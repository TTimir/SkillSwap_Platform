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

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // run once a day
                    await Task.Delay(TimeSpan.FromHours(24), token).ConfigureAwait(false);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
                    var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                    var now = DateTime.UtcNow;
                    // process subscriptions ended within the last 24 hours
                    var windowStart = now.AddDays(-1);

                    var due = await db.Subscriptions
                         .Where(s => s.IsAutoRenew
                                     && s.EndDate <= now
                                     && s.EndDate > windowStart
                                     && (s.LastAutoRenewedAt == null || s.LastAutoRenewedAt < s.EndDate))
                         .ToListAsync(token)
                         .ConfigureAwait(false);

                    foreach (var sub in due)
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
                                                      start, end,
                                                      gatewayOrderId: string.Empty,
                                                      gatewayPaymentId: string.Empty,
                                                      paidAmount: 0m,
                                                      sendEmail: false)
                                    .ConfigureAwait(false);

                            _logger.LogInformation("Auto-renewed {Plan} for user {UserId}",
                                sub.PlanName, sub.UserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Auto-renew failed for user {User}", sub.UserId);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SubscriptionRenewalService encountered an error");
                }
            }
        }
    }
}
