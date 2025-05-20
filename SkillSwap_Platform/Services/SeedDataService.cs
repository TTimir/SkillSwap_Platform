using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services
{
    public class SeedDataService : IHostedService
    {
        private readonly IServiceProvider _services;

        public SeedDataService(IServiceProvider services) => _services = services;

        public async Task StartAsync(CancellationToken _)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
            await db.EnsureAdminUserAsync();
            await db.EnsureEscrowUserAsync();
            await db.EnsureSystemReserveUserAsync();
        }

        public Task StopAsync(CancellationToken _) => Task.CompletedTask;
    }
}
