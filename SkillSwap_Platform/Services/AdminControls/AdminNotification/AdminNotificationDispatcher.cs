using Humanizer;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services.AdminControls.AdminNotification
{
    public class AdminNotificationDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmailService _email;
        private readonly ILogger<AdminNotificationDispatcher> _log;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15);

        public AdminNotificationDispatcher(
            IServiceScopeFactory scopeFactory,
            IEmailService email,
            ILogger<AdminNotificationDispatcher> log)
        {
            _scopeFactory = scopeFactory;
            _email = email;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();

                    var batch = await db.AdminNotifications
                        .Where(n => n.SentAtUtc == null && n.AttemptCount < 5)
                        .OrderBy(n => n.CreatedAtUtc)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    foreach (var note in batch)
                    {
                        try
                        {
                            await _email.SendEmailAsync(
                                to: note.ToEmail,
                                subject: note.Subject,
                                body: note.Body,
                                isBodyHtml: true);

                            note.SentAtUtc = DateTime.UtcNow;
                            note.AttemptCount++;
                            note.LastError = null;
                        }
                        catch (Exception ex)
                        {
                            note.AttemptCount++;
                            note.LastError = ex.Message.Truncate(1000);
                            _log.LogWarning(ex, "Failed to send admin notification #{Id}", note.Id);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Notification dispatcher loop failed");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }
}
