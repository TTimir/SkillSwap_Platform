using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Meeting
{
    public class MeetingTimeoutHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MeetingTimeoutHostedService> _logger;

        // Define a constant to indicate a system action (e.g., for audit purposes).
        private const int SYSTEM_USER_ID = -1;

        // Define the grace period and check interval.
        private readonly TimeSpan _gracePeriod = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public MeetingTimeoutHostedService(IServiceScopeFactory scopeFactory, ILogger<MeetingTimeoutHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Repeat until cancellation is requested.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Create a new scope to obtain scoped services.
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        // Resolve the DbContext from the scope.
                        var dbContext = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();

                        // Load all active in-person meeting records from the dedicated table that have a scheduled meeting time and have not ended.
                        var meetingsToExpire = await dbContext.TblInPersonMeetings
                            .Where(m => m.MeetingScheduledDateTime.HasValue && !m.IsMeetingEnded)
                            .ToListAsync(stoppingToken);

                        foreach (var meeting in meetingsToExpire)
                        {
                            // Calculate the meeting duration: use InpersonMeetingDurationMinutes if provided, otherwise default to 60 minutes.
                            var duration = meeting.InpersonMeetingDurationMinutes.HasValue
                                ? TimeSpan.FromMinutes(meeting.InpersonMeetingDurationMinutes.Value)
                                : TimeSpan.FromMinutes(60);

                            // Calculate the meeting end time based on the scheduled time and duration.
                            DateTime meetingEndTime = meeting.MeetingScheduledDateTime.Value.Add(duration);

                            // Add the grace period to the meeting end time.
                            DateTime timeoutTime = meetingEndTime.Add(_gracePeriod);

                            // If the current UTC time is past the timeout time, mark the meeting as ended/no-show.
                            if (DateTime.UtcNow >= timeoutTime)
                            {
                                meeting.IsMeetingEnded = true;

                                // Retrieve the associated exchange using the meeting's ExchangeId.
                                var exchange = await dbContext.TblExchanges.FindAsync(new object[] { meeting.ExchangeId }, stoppingToken);

                                // Log this action in the exchange history table.
                                dbContext.TblExchangeHistories.Add(new TblExchangeHistory
                                {
                                    ExchangeId = meeting.ExchangeId,
                                    OfferId = exchange != null ? exchange.OfferId : 0,
                                    ActionType = "In-Person Meeting Timeout",
                                    ChangedStatus = "No-show",
                                    ChangedBy = SYSTEM_USER_ID,  // Indicates system-initiated action.
                                    ChangeDate = DateTime.UtcNow,
                                    Reason = "The meeting did not complete end proof verification within the allowed timeframe."
                                });
                            }
                        }

                        // Save the changes made for these meetings.
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while checking meeting timeouts.");
                }

                // Wait for the check interval before the next iteration.
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckForMissingStartProofAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                // Get your application's DbContext.
                var context = scope.ServiceProvider.GetRequiredService<SkillSwapDbContext>();

                // Query meetings with a scheduled time and duration,
                // that have already ended and where StartProofImageUrl is null or empty.
                var meetingsAndProofs = await (
                    from meeting in context.TblInPersonMeetings
                    join proof in context.TblInpersonMeetingProofs
                        on meeting.ExchangeId equals proof.ExchangeId into proofGroup
                    from pr in proofGroup.DefaultIfEmpty()
                    where meeting.MeetingScheduledDateTime.HasValue &&
                          meeting.InpersonMeetingDurationMinutes.HasValue &&
                          // Calculate expected meeting end time:
                          meeting.MeetingScheduledDateTime.Value
                            .AddMinutes(meeting.InpersonMeetingDurationMinutes.Value) < DateTime.UtcNow &&
                          (pr == null || string.IsNullOrWhiteSpace(pr.StartProofImageUrl))
                    select new { Meeting = meeting, Proof = pr }
                ).ToListAsync(stoppingToken);

                // Iterate through the results.
                foreach (var item in meetingsAndProofs)
                {
                    // If no proof record exists, create one.
                    if (item.Proof == null)
                    {
                        var newProof = new TblInpersonMeetingProof
                        {
                            ExchangeId = item.Meeting.ExchangeId,
                            CreatedDate = DateTime.UtcNow,
                            StartProofImageUrl = "/template_assets/images/No_image.png"
                        };
                        context.TblInpersonMeetingProofs.Add(newProof);
                    }
                    else
                    {
                        // Otherwise, update the existing proof record.
                        item.Proof.StartProofImageUrl = "/template_assets/images/No_image.png";
                    }

                    // Optionally, update meeting notes to reflect the auto-mark.
                    item.Meeting.MeetingNotes = (item.Meeting.MeetingNotes ?? "")
                        + " [Auto-Update: No start proof was provided; meeting auto-marked as missing proof by system.]";

                    // Retrieve the associated exchange for logging.
                    var exchange = await context.TblExchanges.FindAsync(new object[] { item.Meeting.ExchangeId }, stoppingToken);
                    if (exchange == null)
                    {
                        continue;
                    }

                    // Create a history record to log this automatic action.
                    var historyRecord = new TblExchangeHistory
                    {
                        ExchangeId = exchange.ExchangeId,
                        OfferId = exchange.OfferId,
                        ActionType = "Auto-Check: Missing Start Proof",
                        ChangedStatus = "Missing Proof",
                        // You can use a designated system user ID here; we use 0.
                        ChangedBy = 0,
                        ChangeDate = DateTime.UtcNow,
                        Reason = "Meeting ended without a start proof. System auto-marked the meeting as missing proof."
                    };
                    context.TblExchangeHistories.Add(historyRecord);
                }

                // Save all changes once for efficiency.
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}