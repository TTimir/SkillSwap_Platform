using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Contracts
{
    public class ContractExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractExpirationService> _logger;

        public ContractExpirationService(IServiceScopeFactory scopeFactory, ILogger<ContractExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Contract Expiration Service starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
                        var now = DateTime.UtcNow;
                        // Find contracts that are still pending, under review, or modified and whose completion date has passed.
                        var contractsToExpire = await context.TblContracts
                            .Where(c => (c.Status == "Pending" ||
                                         c.Status == "ModifiedByReceiver" ||
                                         c.Status == "ModifiedBySender" ||
                                         c.Status == "Review")
                                        && c.CompletionDate.HasValue
                                        && c.CompletionDate.Value < now)
                            .ToListAsync(stoppingToken);

                        foreach (var contract in contractsToExpire)
                        {
                            contract.Status = "Expired";
                            _logger.LogInformation($"Contract {contract.ContractUniqueId} marked as expired.");
                        }

                        if (contractsToExpire.Any())
                        {
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating contract expirations.");
                }

                // Delay next check (for example, every 5 minutes)
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

            _logger.LogInformation("Contract Expiration Service stopping.");
        }
    }
}