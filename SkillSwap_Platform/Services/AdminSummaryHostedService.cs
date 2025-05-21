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
                        var subject = "SkillSwap Admin Summary";
                        var body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
    <tr>
      <td align=""center"" style=""padding:20px;"">
        <table width=""600"" style=""background:#fff;border-collapse:collapse;"">
          
          <!-- Header -->
          <tr>
            <td style=""background:#00A88F;padding:20px;"">
              <h1 style=""margin:0;color:#fff;font-size:24px;"">SkillSwap Admin</h1>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""padding:20px;color:#333;line-height:1.5;"">
              <p>Hello Admin,</p>
              <p>Since <strong>{_lastRun.ToLocalTime().ToString("u")}</strong>:</p>
              <ul>
                <li>{newFlags} new flagged offer(s)</li>
                <li>{newReviews} new flagged review(s)</li>
                <li>{newMessages} new flagged message(s)</li>
                <li>{pendingCerts} pending verification certificate(s)</li>
                <li>{heldUsers} newly held user(s)</li>
              </ul>
              <p><em>— Your SkillSwap System</em></p>
            </td>
          </tr>

          <!-- Divider -->
          <tr>
            <td style=""padding:0 20px;"">
              <hr style=""border:none;border-top:1px solid #e0e0e0;margin:0;"">
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background:#00A88F;padding:10px;text-align:center;color:#e0f7f1;font-size:11px;"">
              © {DateTime.UtcNow.ToLocalTime().ToString("yyyy")} SkillSwap Inc. | 
              <a href=""mailto:skillswap360@gmail.com"" style=""color:#fff;text-decoration:underline;"">Support</a>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>
";

                        await email.SendEmailAsync(
                            to: "admin@yourdomain.com",
                            subject: subject,
                            body: body,
                            isBodyHtml: true
                        );
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