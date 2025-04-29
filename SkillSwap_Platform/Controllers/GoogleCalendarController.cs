using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Google.Apis.Auth.OAuth2.Responses;
using SkillSwap_Platform.Models.ViewModels.MeetingVM;
using Newtonsoft.Json.Linq;
using SkillSwap_Platform.Services.NotificationTrack;

namespace SkillSwap_Platform.Controllers
{
    public class GoogleCalendarController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleCalendarController> _logger;
        private readonly SkillSwapDbContext _dbContext;
        private readonly TimeSpan _reuseThreshold = TimeSpan.FromHours(1);
        private readonly INotificationService _notif;

        public GoogleCalendarController(IConfiguration configuration, ILogger<GoogleCalendarController> logger, SkillSwapDbContext dbContext, INotificationService notif)
        {
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
            _notif = notif;
        }

        // Creates a calendar event with a Google Meet conference link.
        [Obsolete]
        public async Task<IActionResult> CreateEvent(int? exchangeId, int otherUserId)
        {
            try
            {
                // Get current user id
                int userId = 0;
                var userClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userClaim != null && int.TryParse(userClaim.Value, out int parsedUserId))
                {
                    userId = parsedUserId;
                }
                else
                {
                    _logger.LogError("Unable to retrieve user id from claims.");
                    return Unauthorized();
                }


                var exchange = await _dbContext.TblExchanges
                    .Include(e => e.Offer)
                    .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                if (exchange == null)
                {
                    _logger.LogError("Exchange with id {ExchangeId} not found.", exchangeId);
                    return NotFound("Exchange not found.");
                }

                var offer = exchange.Offer;
                if (offer == null)
                {
                    _logger.LogError("Associated offer for exchange id {ExchangeId} is null.", exchangeId);
                    return NotFound("Offer not found.");
                }

                if (!string.IsNullOrEmpty(exchange.ExchangeMode) &&
            exchange.ExchangeMode.Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    exchange.IsInOnlineExchange = true;
                }
                else
                {
                    exchange.IsInOnlineExchange = false;
                }

                // Build a unique session key for this meeting record.
                var sessionKey = $"MeetingRecord_{exchangeId}_{otherUserId}_{userId}";

                // Check if a meeting record id is stored in session.
                int? storedMeetingId = HttpContext.Session.GetInt32(sessionKey);
                if (storedMeetingId.HasValue)
                {
                    var storedMeeting = await _dbContext.TblMeetings.FirstOrDefaultAsync(m => m.MeetingId == storedMeetingId.Value);
                    // Only reuse if the meeting still has status "Scheduled" and it was created within the _reuseThreshold.
                    if (storedMeeting != null && storedMeeting.Status == "Scheduled" &&
                        (DateTime.UtcNow - storedMeeting.CreatedDate) < _reuseThreshold)
                    {
                        _logger.LogInformation("Reusing meeting record from session (ID: {MeetingId})", storedMeeting.MeetingId);
                        return View("CreateEvent", storedMeeting.MeetingLink);
                    }
                    else
                    {
                        // Remove the session key if the record is stale or its status is not "Scheduled".
                        HttpContext.Session.Remove(sessionKey);
                    }
                }

                // Retrieve stored token from DB.
                var userToken = await _dbContext.UserGoogleTokens.FirstOrDefaultAsync(t => t.UserId == userId);
                if (userToken == null)
                {
                    // No token stored – must re-authorize.
                    return RedirectToAction("Authorize", "GoogleAuth");
                }

                // If token is expired, refresh it.
                if (userToken.ExpiresAt < DateTime.UtcNow)
                {
                    userToken = await RefreshAccessTokenAsync(userToken);
                    if (userToken == null)
                    {
                        // Refresh failed; redirect to reauthorize.
                        return RedirectToAction("Authorize", "GoogleAuth");
                    }
                }

                // Use the valid access token to initialize the CalendarService.
                var calendarService = new CalendarService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = GoogleCredential.FromAccessToken(userToken.AccessToken),
                    ApplicationName = "SkillSwapMeet"
                });

                // Build the event details.
                var newEvent = new Event
                {
                    Summary = offer.Title,
                    Location = "Online",
                    Description = $"Skill Swap session for the offer \"{offer.Title}\". Please join at the scheduled time.",
                    Start = new EventDateTime
                    {
                        DateTime = DateTime.Now,
                        TimeZone = "UTC"
                    },
                    End = new EventDateTime
                    {
                        DateTime = DateTime.Now.AddMinutes(60),
                        TimeZone = "UTC"
                    },
                    // Configure Google Meet conference data.
                    ConferenceData = new ConferenceData
                    {
                        CreateRequest = new CreateConferenceRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            ConferenceSolutionKey = new ConferenceSolutionKey
                            {
                                Type = "hangoutsMeet"
                            }
                        }
                    }
                };

                // Insert the event into the primary calendar.
                var request = calendarService.Events.Insert(newEvent, "primary");
                request.ConferenceDataVersion = 1;
                var createdEvent = await request.ExecuteAsync();

                // Return the Google Meet join URL to the user.
                var joinUrl = createdEvent.ConferenceData?.EntryPoints?[0]?.Uri;

                var scheduledDateTime = newEvent.Start.DateTime ?? DateTime.Now;
                var duration = (newEvent.End.DateTime - newEvent.Start.DateTime)?.TotalMinutes ?? 60;

                var previousSessionsCount = await _dbContext.TblMeetings.CountAsync(m => m.ExchangeId == exchange.ExchangeId);
                int nextSessionNumber = previousSessionsCount + 1;

                // Create a new meeting record to save in the database.
                var meetingRecord = new TblMeeting
                {
                    ExchangeId = exchange.ExchangeId,
                    CreatorUserId = userId,
                    OtherUserId = otherUserId,
                    OfferId = offer.OfferId,
                    DurationMinutes = (int)duration,
                    MeetingLink = joinUrl,
                    Status = "Scheduled",
                    ActualStartTime = scheduledDateTime,
                    ActualEndTime = DateTime.UtcNow.AddMinutes(duration),
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    MeetingStartTime = scheduledDateTime,
                    MeetingNotes = string.Empty,
                    MeetingType = "Google Meet",
                    Location = newEvent.Location,
                    MeetingSessionNumber = nextSessionNumber
                };


                // Save the meeting record to the database.
                _dbContext.TblMeetings.Add(meetingRecord);
                await _dbContext.SaveChangesAsync();

                // Check if the exchange mode is online using the ExchangeMode column.
                // Ensure that the value comparison is case-insensitive.
                if (!string.IsNullOrEmpty(exchange.ExchangeMode) &&
                    exchange.ExchangeMode.Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    // Initialize a JSON array from the existing MeetingsJson property,
                    // or start a new array if it's null or empty.
                    JArray meetingsArray = string.IsNullOrEmpty(exchange.ThisMeetingLink)
                        ? new JArray()
                        : JArray.Parse(exchange.ThisMeetingLink);

                    // Create a JSON object representing this meeting session.
                    JObject meetingSession = new JObject
                    {
                        { "SessionNumber", nextSessionNumber },
                        { "MeetingId", meetingRecord.MeetingId },
                        { "MeetingLink", joinUrl },
                        { "CreatedDate", DateTime.UtcNow.ToString("o") } // ISO 8601 format
                    };

                    // Add the meeting session object to the JSON array.
                    meetingsArray.Add(meetingSession);

                    // Serialize the updated JSON array back to a string and store it in the exchange record.
                    exchange.ThisMeetingLink = meetingsArray.ToString();

                    // Update the exchange record in the database.
                    _dbContext.TblExchanges.Update(exchange);
                    await _dbContext.SaveChangesAsync();
                }

                // Store the newly created meeting record id in session.
                HttpContext.Session.SetInt32(sessionKey, meetingRecord.MeetingId);

                // Insert the meeting invitation as a system message.
                await InsertMeetingCardMessageAsync(meetingRecord, offer);

                if (exchange == null)
                {
                    return NotFound("Exchange record not found.");
                }

                // Store Exchange history record.
                var historyRecord = new TblExchangeHistory
                {
                    ExchangeId = exchange.ExchangeId,
                    OfferId = offer.OfferId,
                    ActionType = "Online Meeting",
                    ChangeDate = DateTime.UtcNow,
                    ChangedStatus = "Meeting Launched",
                    Reason = $"Online meeting launched. Session #{meetingRecord.MeetingSessionNumber} (MeetingID: {meetingRecord.MeetingId})",
                    ChangedBy = userId
                };
                _dbContext.TblExchangeHistories.Add(historyRecord);
                await _dbContext.SaveChangesAsync();

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = userId,
                    Title = "Online Meeting Scheduled",
                    Message = "Online meeting scheduled successfully.",
                    Url = Url.Action("Conversation", "Messaging"),
                });

                var vm = new MeetingLaunchVM
                {
                    JoinUrl = joinUrl,
                    MeetingRecordId = meetingRecord.MeetingId,
                    SessionNumber = meetingRecord.MeetingSessionNumber
                };

                return View("CreateEvent", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Google Calendar event.");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMeetingNotes(int meetingId, string meetingNotes, int Rating)
        {
            try
            {
                // Server-side validation: ensure notes have been provided.
                if (string.IsNullOrWhiteSpace(meetingNotes))
                {
                    ModelState.AddModelError("MeetingNotes", "Meeting notes are required.");
                    // Optionally, you can re-render the CreateEvent view with the error.
                    // Here we simply return a BadRequest.
                    return BadRequest("Meeting notes are required.");
                }

                // Validate the rating is within an acceptable range.
                if (Rating < 1 || Rating > 5)
                {
                    ModelState.AddModelError("Rating", "A valid rating between 1 and 5 is required.");
                    return BadRequest("A valid rating between 1 and 5 is required.");
                }

                if (meetingId == 0)
                {
                    _logger.LogError("Invalid meeting id provided: {MeetingId}", meetingId);
                    return BadRequest("A valid meeting id must be provided.");
                }

                // Retrieve the meeting record based on the meetingId.
                var meeting = await _dbContext.TblMeetings.FirstOrDefaultAsync(m => m.MeetingId == meetingId);
                if (meeting == null)
                {
                    _logger.LogError("Meeting with id {MeetingId} not found.", meetingId);
                    return NotFound("Meeting not found.");
                }

                // Update the meeting record with the provided notes, and mark the meeting as 'Completed'.
                meeting.MeetingNotes = meetingNotes;
                meeting.MeetingRating = Rating;
                meeting.Status = "Completed";  // or whatever status you need to capture
                meeting.UpdatedDate = DateTime.UtcNow;

                _dbContext.TblMeetings.Update(meeting);
                await _dbContext.SaveChangesAsync();

                // Log an exchange history record for tracking (adjust the mapping as per your domain model).
                var historyRecord = new TblExchangeHistory
                {
                    ExchangeId = meeting.ExchangeId,
                    OfferId = meeting.OfferId ?? 0,
                    ChangeDate = DateTime.UtcNow,
                    ChangedStatus = "Meeting Completed",
                    Reason = $"Meeting feedback submitted with rating {Rating} and notes: {meetingNotes}",
                    // Retrieve the current user's id.
                    ChangedBy = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0
                };
                _dbContext.TblExchangeHistories.Add(historyRecord);
                await _dbContext.SaveChangesAsync();

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = userId,
                    Title = "Online Meeting Notes Saved",
                    Message = "You successfully saved your notes of online meeting.",
                    Url = Url.Action("Conversation", "Messaging"),
                });

                _logger.LogInformation("Meeting notes saved successfully for meeting id {MeetingId}.", meetingId);
                return RedirectToAction("Index", "ExchangeDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving meeting notes for meeting id {MeetingId}.", meetingId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Inserts a system message (meeting card) into the messaging table so that both users see the meeting invitation.
        /// Assumes that TblMessage has additional properties such as MessageType and MeetingId.
        /// </summary>
        private async Task InsertMeetingCardMessageAsync(TblMeeting meetingRecord, TblOffer offer)
        {
            try
            {
                // Build a unique prefix that helps to identify this meeting message later.
                string meetingPrefix = $"[MEETING_ID:{meetingRecord.MeetingId}]";
                string meetingMessageContent = $@"
                    {meetingPrefix}
                    <div style='border: 1px solid #ccc; padding: 15px; border-radius: 5px; background-color: rgba(91, 187, 123, 0.05);'>
                        <h4 style='margin-top: 0; font-family:Arial, sans-serif;'>Meeting Scheduled</h4>
                        <p style='font-family:Arial, sans-serif;'>
                            Meeting for <strong>{offer.Title}</strong> has been scheduled.
                        </p>
                        <div style='margin-top: 10px;'>
                            <a href='{meetingRecord.MeetingLink}' target='_blank' class='ud-btn btn-light-thm me-4'>
                                Join Meeting <i class=""fal fa-arrow-right-long""></i>
                            </a>
                        </div>
                        <div style='margin-top:5px; font-size:0.9em; color:#555;'>
                            <p>Session Number: {meetingRecord.MeetingSessionNumber}</p>
                            <p>Meeting ID: {meetingRecord.MeetingId}</p>
                        </div>
                    </div>";

                var meetingMessage = new TblMessage
                {
                    SenderUserId = meetingRecord.CreatorUserId,
                    ReceiverUserId = meetingRecord.OtherUserId,
                    Content = meetingMessageContent,
                    MessageType = "MeetingCard",
                    SentDate = DateTime.UtcNow,
                    MeetingLink = meetingRecord.MeetingLink,
                    OfferId = meetingRecord.OfferId,
                    ExchangeId = meetingRecord.ExchangeId
                };

                _dbContext.TblMessages.Add(meetingMessage);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting meeting card message for MeetingId: {MeetingId}", meetingRecord.MeetingId);
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMeetingSession(int meetingId, DateTime meetingStartTime, DateTime actualEndTime)
        {
            var meeting = await _dbContext.TblMeetings.FirstOrDefaultAsync(m => m.MeetingId == meetingId);
            if (meeting == null)
            {
                return NotFound("Meeting record not found.");
            }

            // Use the meetingStartTime passed from the client instead of the one stored in the DB.
            int duration = (int)(actualEndTime - meetingStartTime).TotalMinutes;

            if (duration < 1) duration = 1;

            // Calculate duration in minutes based on the meeting start time.
            meeting.DurationMinutes = duration;
            meeting.MeetingEndTime = actualEndTime;
            meeting.ActualEndTime = actualEndTime;
            meeting.Status = "Completed";
            meeting.UpdatedDate = DateTime.UtcNow;

            _dbContext.TblMeetings.Update(meeting);
            await _dbContext.SaveChangesAsync();

            // Build the session key used previously.
            //var sessionKey = $"MeetingRecord_{meeting.OfferId}_{meeting.OtherUserId}_{meeting.CreatorUserId}";
            //HttpContext.Session.Remove(sessionKey);

            await UpdateMeetingCardMessageAsync(meetingId, actualEndTime);

            return Ok();
        }

        /// <summary>
        /// Updates the meeting card message in the messaging table to mark that the meeting has ended.
        /// Identifies the message by using the reserved prefix in the content.
        /// </summary>
        private async Task UpdateMeetingCardMessageAsync(int meetingId, DateTime actualEndTime)
        {
            try
            {
                // Search for the meeting card message using the reserved prefix.
                string meetingPrefix = $"[MEETING_ID:{meetingId}]";
                var meetingMessage = await _dbContext.TblMessages
                    .FirstOrDefaultAsync(m => m.Content.StartsWith(meetingPrefix));

                if (meetingMessage != null)
                {
                    meetingMessage.Content = $"{meetingPrefix} Meeting ended at {actualEndTime.ToLocalTime():g}";

                    _dbContext.TblMessages.Update(meetingMessage);
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating meeting card message for MeetingId: {MeetingId}", meetingId);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh token.
        /// </summary>
        private async Task<UserGoogleToken> RefreshAccessTokenAsync(UserGoogleToken token)
        {
            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                var clientSecret = _configuration["Authentication:Google:ClientSecret"];
                var tokenRequestUrl = "https://oauth2.googleapis.com/token";
                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("refresh_token", token.RefreshToken),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                });

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.PostAsync(tokenRequestUrl, requestBody);
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                    token.AccessToken = tokenResponse.AccessToken;
                    token.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds.HasValue ? tokenResponse.ExpiresInSeconds.Value : 3600);
                    _dbContext.UserGoogleTokens.Update(token);
                    await _dbContext.SaveChangesAsync();
                }
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing Google access token.");
                return null;
            }
        }
    }
}