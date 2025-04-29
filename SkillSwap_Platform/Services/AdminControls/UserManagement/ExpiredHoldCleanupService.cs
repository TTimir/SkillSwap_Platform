namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public class ExpiredHoldCleanupService : BackgroundService
    {
        private readonly ILogger<ExpiredHoldCleanupService> _logger;
        private readonly IServiceProvider _services;
        private readonly TimeSpan _interval;

        public ExpiredHoldCleanupService(
            IServiceProvider services,
            ILogger<ExpiredHoldCleanupService> logger)
        {
            _services = services;
            _logger = logger;
            // you can read this from IConfiguration if you like
            _interval = TimeSpan.FromHours(1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExpiredHoldCleanupService is starting up.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var svc = scope.ServiceProvider
                                   .GetRequiredService<IUserManagmentService>();

                    var releasedCount = await svc.ReleaseExpiredHoldsAsync();
                    if (releasedCount > 0)
                        _logger.LogInformation("Released {Count} expired holds.", releasedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ExpiredHoldCleanupService");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("ExpiredHoldCleanupService is shutting down.");
        }
    }
}