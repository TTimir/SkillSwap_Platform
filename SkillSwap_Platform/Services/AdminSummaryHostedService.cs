using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services
{
    public class AdminSummaryHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AdminSummaryHostedService> _logger;
        private DateTime _lastRun = DateTime.UtcNow;

        public AdminSummaryHostedService(
            IServiceScopeFactory scopes,
            ILogger<AdminSummaryHostedService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var dash = scope.ServiceProvider.GetRequiredService<IAdminDashboardService>();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    // get counts *since* our last run
                    var newFlags = await dash.GetFlaggedOffersCountAsync();       // tweak these if you want "since"
                    var newReviews = await dash.GetFlaggedReviewsCountAsync();
                    var newMessages = await dash.GetFlaggedMessagesCountAsync();
                    var pendingCerts = await dash.GetPendingCertificatesCountAsync();
                    var heldUsers = await dash.GetHeldUsersCountAsync();

                    if (newFlags + newReviews + newMessages + pendingCerts + heldUsers > 0)
                    {
                        var body = $@"
                        <p>Hello Admin,</p>
                        <p>Since {_lastRun:u}:</p>
                        <ul>
                          <li>{newFlags} new flagged offer(s)</li>
                          <li>{newReviews} new flagged review(s)</li>
                          <li>{newMessages} new flagged message(s)</li>
                          <li>{pendingCerts} pending verification certificate(s)</li>
                          <li>{heldUsers} newly held user(s)</li>
                        </ul>
                        <p><em>— Your SkillSwap System</em></p>";

                        await email.SendEmailAsync("admin@yourdomain.com",
                                                  "SkillSwap Admin Summary",
                                                  body,
                                                  isBodyHtml: true);
                    }

                    _lastRun = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AdminSummaryHostedService");
                }

                // wait 5 minutes (you can also use a cron‐library for more precise schedules)
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}