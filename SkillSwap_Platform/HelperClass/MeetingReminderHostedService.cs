using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.InPersonMeetReminder;

namespace SkillSwap_Platform.HelperClass
{
    public class MeetingReminderHostedService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<MeetingReminderHostedService> _log;
        private readonly IMemoryCache _cache;

        // send 30m before start
        private readonly TimeSpan _leadTime = TimeSpan.FromMinutes(30);
        // missing-end-proof at 15, 45, 75 minutes after start
        private readonly TimeSpan[] _reminderDelays = new[]
        {
            TimeSpan.FromMinutes(15),     // first nudge
            TimeSpan.FromHours(24),       // 1 day later
            TimeSpan.FromDays(2)          // 2 days later (final)
        };
        // how wide a “window” around our target minute
        private readonly TimeSpan _tolerance = TimeSpan.FromMinutes(1);

        public MeetingReminderHostedService(
            IServiceProvider sp,
            ILogger<MeetingReminderHostedService> log,
            IMemoryCache cache)
        {
            _sp = sp;
            _log = log;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();
                    var remindSvc = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    var nowUtc = DateTime.UtcNow;

                    var meetings = await db.TblInPersonMeetings
                                           .Include(m => m.Exchange)
                                           .ToListAsync(ct);

                    foreach (var m in meetings)
                    {
                        if (m.IsMeetingEnded)
                            continue;

                        if (_cache.TryGetValue($"snooze-{m.ExchangeId}", out _))
                            continue;

                        if (!m.MeetingScheduledDateTime.HasValue)
                            continue;

                        var startUtc = m.MeetingScheduledDateTime.Value.ToUniversalTime();
                        var toStart = startUtc - nowUtc;
                        var sinceStart = nowUtc - startUtc;

                        // 30-min before
                        if (toStart <= _leadTime && toStart > _leadTime - _tolerance)
                        {
                            await remindSvc.SendEndMeetingReminder(m.ExchangeId);
                        }

                        // post-meeting nudges
                        for (int i = 0; i < _reminderDelays.Length; i++)
                        {
                            var delay = _reminderDelays[i];
                            if (sinceStart >= delay && sinceStart < delay + _tolerance)
                            {
                                bool isFinal = (i == _reminderDelays.Length - 1);
                                await remindSvc.SendMissingEndProofReminder(m.ExchangeId, isFinal);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "MeetingReminderHostedService error");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
        }
    }
}