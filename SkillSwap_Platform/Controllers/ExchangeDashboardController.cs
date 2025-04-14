using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ExchangeDashboardController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ExchangeDashboardController> _logger;

        public ExchangeDashboardController(SkillSwapDbContext context, ILogger<ExchangeDashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /ExchangeDashboard/
        public async Task<IActionResult> Index(int page = 1)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                int pageSize = 5;

                // Count total exchanges for current user
                var totalExchanges = await _context.TblExchanges
                    .Where(e => (e.OtherUserId ?? 0) == currentUserId
                        || (e.OfferOwnerId ?? 0) == currentUserId)
                    .CountAsync();

                int totalPages = (int)Math.Ceiling(totalExchanges / (double)pageSize);

                // Retrieve exchanges where the current user is either the requester or the offer owner
                var exchanges = await _context.TblExchanges
                    .Include(e => e.Offer)
                    .Where(e => (e.OtherUserId ?? 0) == currentUserId
                        || (e.OfferOwnerId ?? 0) == currentUserId)
                    .OrderByDescending(e => e.ExchangeDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dashboardItems = new List<ExchangeDashboardItemVM>();

                // Define a threshold and time window.
                int meetingLaunchThreshold = 3;
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);

                // For each exchange, retrieve the associated history records
                foreach (var exchange in exchanges)
                {
                    var meetingRecord = await _context.TblInPersonMeetings
                        .FirstOrDefaultAsync(m => m.ExchangeId == exchange.ExchangeId);

                    // In your controller loop:
                    var history = await _context.TblExchangeHistories
                        .Where(h => h.ExchangeId == exchange.ExchangeId)
                        .OrderByDescending(h => h.ChangeDate)
                        .ToListAsync();

                    int offerOwnerId = exchange.OfferOwnerId ?? 0;
                    int otherUserIdFromExchange = exchange.OtherUserId ?? 0;
                    int otherUserId;

                    // Determine the other user based on the current user's role
                    if (currentUserId == offerOwnerId)
                    {
                        otherUserId = otherUserIdFromExchange;
                    }
                    else if (currentUserId == otherUserIdFromExchange)
                    {
                        otherUserId = offerOwnerId;
                    }
                    else
                    {

                        _logger.LogWarning("Current user is not a participant in exchange {ExchangeId}.", exchange.ExchangeId);
                        otherUserId = 0;
                        throw new Exception("The current user is not a participant in the exchange.");
                    }

                    // Count recent meeting launches for this offer and current user.
                    int recentCount = await _context.TblMeetings
                        .Where(m => m.CreatorUserId == currentUserId &&
                                    m.OfferId == exchange.OfferId &&
                                    m.CreatedDate >= oneHourAgo)
                        .CountAsync();

                    int totalSessionsCount = await _context.TblMeetings
                        .Where(m => m.ExchangeId == exchange.ExchangeId)
                        .CountAsync();

                    // If count is below threshold, the user may launch a new meeting.
                    bool canLaunch = recentCount < meetingLaunchThreshold;

                    // Assuming your TblOffer has a property "PortfolioUrls" (a JSON or comma‐separated list)
                    string offerImageUrl = "/template_assets/images/default-offer.png"; // fallback
                    if (exchange.Offer != null && !string.IsNullOrEmpty(exchange.Offer.Portfolio))
                    {
                        // Deserialize and pick the first URL, or adapt based on your data format
                        var portfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(exchange.Offer.Portfolio);
                        if (portfolioUrls != null && portfolioUrls.Any())
                        {
                            offerImageUrl = portfolioUrls.First();
                        }
                    }

                    var contract = await _context.TblContracts
                        .Where(c => c.OfferId == exchange.OfferId)
                        .OrderByDescending(c => c.ContractId) // or order by created date if available
                        .FirstOrDefaultAsync();

                    string contractUniqueId = contract != null
                        ? contract.ContractUniqueId
                        : exchange.ExchangeId.ToString();

                    // Retrieve last status changed by name (if applicable)
                    string lastStatusChangedByName = "N/A";
                    if (exchange.LastStatusChangedBy.HasValue)
                    {
                        var user = await _context.TblUsers
                            .FirstOrDefaultAsync(u => u.UserId == exchange.LastStatusChangedBy.Value);
                        if (user != null)
                        {
                            lastStatusChangedByName = user.UserName;
                        }
                    }

                    dashboardItems.Add(new ExchangeDashboardItemVM
                    {
                        Exchange = exchange,
                        History = history,
                        OfferOwnerId = offerOwnerId,
                        OtherUserId = otherUserId,
                        ContractUniqueId = contractUniqueId,
                        OfferTitle = exchange.Offer?.Title ?? "N/A",
                        ExchangeStartDate = exchange.ExchangeDate,
                        Status = exchange.Status,
                        ExchangeMode = exchange.ExchangeMode,
                        LastStatusChangedBy = exchange.LastStatusChangedBy,
                        LastStatusChangedByName = lastStatusChangedByName,
                        Description = exchange.Description,
                        OfferImageUrl = offerImageUrl,
                        CanLaunchMeeting = canLaunch,
                        RecentMeetingLaunchCount = totalSessionsCount,
                        Category = exchange.Offer.Category,
                        Token = contract.TokenOffer,
                        IsMeetingEnded = exchange.IsMeetingEnded,
                        MeetingScheduledDateTime = meetingRecord?.MeetingScheduledDateTime,
                        InpersonMeetingDurationMinutes = meetingRecord?.InpersonMeetingDurationMinutes
                    });
                }

                var viewModel = new ExchangeDashboardVM
                {
                    ExchangeItems = dashboardItems,
                    CurrentPage = page,
                    TotalPages = totalPages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading exchange dashboard for user {UserId}", GetCurrentUserId());
                TempData["ErrorMessage"] = "An error occurred while loading the exchange dashboard.";
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: /Exchange/Details/{id}
        public async Task<IActionResult> Details(int id, int timelinePage = 1, string search = null, string sortOrder = "desc")
        {
            try
            {
                int timelinePageSize = 10;

                // Retrieve the exchange record along with its related Offer and History records.
                var exchange = await _context.TblExchanges
                    .Include(e => e.Offer)
                    .Include(e => e.TblExchangeHistories)
                    .FirstOrDefaultAsync(e => e.ExchangeId == id);

                if (exchange == null)
                {
                    _logger.LogWarning("Exchange with id {Id} not found.", id);
                    return NotFound();
                }

                // Filter histories based on the search term if provided.
                var allTimelineHistory = exchange.TblExchangeHistories.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    allTimelineHistory = allTimelineHistory.Where(h =>
                        h.ChangedStatus.Contains(search) ||
                        h.Reason.Contains(search));
                }

                allTimelineHistory = allTimelineHistory.OrderBy(h => h.ChangeDate);

                // Retrieve the contract associated with this exchange using the OfferId.
                var contract = await _context.TblContracts
                    .Where(c => c.OfferId == exchange.OfferId)
                    .OrderByDescending(c => c.ContractId)
                    .FirstOrDefaultAsync();

                string normalizedMode = (exchange.ExchangeMode ?? "").Trim().ToLowerInvariant();
                // Retrieve meeting records based on the exchange mode and map into a common view model.
                List<ExchangeEventVM> meetingEvents = new List<ExchangeEventVM>();
                int meetingSessionNumber = 1;

                if (normalizedMode.Contains("online"))
                {
                    // For online meetings, query the TblMeetings table.
                    var onlineMeetings = await _context.TblMeetings
                        .Where(m => m.ExchangeId == exchange.ExchangeId)
                        .OrderBy(m => m.CreatedDate)
                        .ToListAsync();

                    foreach (var meeting in onlineMeetings)
                    {
                        DateTime meetingStartTime = meeting.MeetingStartTime;
                        TimeSpan duration = TimeSpan.FromMinutes(meeting.DurationMinutes);
                        string notes = meeting.MeetingNotes;
                        int? createdBy = meeting.CreatorUserId;
                        string creatorName = "N/A";
                        if (createdBy.HasValue)
                        {
                            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == createdBy.Value);
                            if (user != null)
                                creatorName = user.UserName;
                        }

                        meetingEvents.Add(new ExchangeEventVM
                        {
                            EventDate = meeting.CreatedDate,
                            EventType = "Online Meeting",
                            StepOrMeetingType = "Session",
                            Description = notes,
                            Duration = duration,
                            SessionNumber = meetingSessionNumber++,
                            MeetingStartTime = meetingStartTime,
                            StatusChangedByName = creatorName,
                            MeetingRank = meeting.MeetingRating.ToString()
                        });
                    }
                }
                else
                {
                    // For in-person meetings, query the TblInPersonMeetings table.
                    var inPersonMeetings = await _context.TblInPersonMeetings
                        .Where(m => m.ExchangeId == exchange.ExchangeId)
                        .OrderBy(m => m.CreatedDate)
                        .ToListAsync();

                    foreach (var meeting in inPersonMeetings)
                    {
                        DateTime meetingStartTime = meeting.MeetingScheduledDateTime ?? meeting.CreatedDate;
                        TimeSpan duration = meeting.InpersonMeetingDurationMinutes.HasValue
                            ? TimeSpan.FromMinutes(meeting.InpersonMeetingDurationMinutes.Value)
                            : TimeSpan.Zero;
                        string notes = meeting.MeetingNotes;
                        int? createdBy = meeting.CreatedByUserId;
                        string creatorName = "N/A";
                        if (createdBy.HasValue)
                        {
                            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == createdBy.Value);
                            if (user != null)
                                creatorName = user.UserName;
                        }

                        meetingEvents.Add(new ExchangeEventVM
                        {
                            EventDate = meeting.CreatedDate,
                            EventType = "In-Person Meeting",
                            StepOrMeetingType = "Session",
                            Description = notes,
                            Duration = duration,
                            SessionNumber = meetingSessionNumber++,
                            MeetingStartTime = meetingStartTime,
                            StatusChangedByName = creatorName
                        });
                    }
                }

                var timelineEvents = exchange.TblExchangeHistories.Select(h =>
                {
                    // you can set StatusChangedByName as needed.
                    string statusChangedBy = "N/A";
                    if (h.ChangedBy.HasValue)
                    {
                        var user = _context.TblUsers.FirstOrDefault(u => u.UserId == h.ChangedBy.Value);
                        if (user != null)
                            statusChangedBy = user.UserName;
                    }

                    return new ExchangeEventVM
                    {
                        EventDate = h.ChangeDate,
                        EventType = "Timeline/ Change",
                        StepOrMeetingType = h.ChangedStatus,
                        Description = h.Reason,
                        SessionNumber = null,
                        // These are not meeting events so MeetingStartTime is not set.
                        MeetingStartTime = null,
                        StatusChangedByName = statusChangedBy
                    };
                }).ToList();

                // Merge and sort both events by date (for example, ascending order)
                var allCombinedEvents = timelineEvents
                    .Concat(meetingEvents)
                    .ToList();

                if (sortOrder.ToLowerInvariant() == "asc")
                {
                    allCombinedEvents = allCombinedEvents.OrderBy(e => e.MeetingStartTime ?? e.EventDate).ToList();
                }
                else
                {
                    allCombinedEvents = allCombinedEvents.OrderByDescending(e => e.MeetingStartTime ?? e.EventDate).ToList();
                }

                // Paginate the combined events.
                int totalCombinedCount = allCombinedEvents.Count();
                int combinedTotalPages = (int)Math.Ceiling(totalCombinedCount / (double)timelinePageSize);

                var pagedCombinedEvents = allCombinedEvents
                    .Skip((timelinePage - 1) * timelinePageSize)
                    .Take(timelinePageSize)
                    .Select((e, index) => { e.SrNo = index + 1 + ((timelinePage - 1) * timelinePageSize); return e; })
                    .ToList();

                // For in-person meeting details, if available, get from meetingRecord.
                string meetingLocation = "-";
                string scheduledTime = "-";
                if (!normalizedMode.Contains("online"))
                {
                    var inPersonMeetingRecord = await _context.TblInPersonMeetings.FirstOrDefaultAsync(m => m.ExchangeId == exchange.ExchangeId);
                    if (inPersonMeetingRecord != null)
                    {
                        meetingLocation = inPersonMeetingRecord.MeetingLocation ?? "-";
                        scheduledTime = inPersonMeetingRecord.MeetingScheduledDateTime.HasValue
                            ? inPersonMeetingRecord.MeetingScheduledDateTime.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
                            : "-";
                    }
                }
                var viewModel = new ExchangeDetailsVM
                {
                    Exchange = exchange,
                    Contract = contract,
                    SelectedExchange = exchange,
                    PagedHistory = allTimelineHistory.ToList(),
                    InpersonMeetingRecords = normalizedMode.Contains("online")
                        ? new List<TblInPersonMeeting>()
                        : await _context.TblInPersonMeetings
                                .Where(m => m.ExchangeId == exchange.ExchangeId)
                                .OrderBy(m => m.CreatedDate)
                                .ToListAsync(),
                    TimelineCurrentPage = timelinePage,
                    TimelineTotalPages = combinedTotalPages,
                    SearchTerm = search,
                    CombinedEvents = pagedCombinedEvents,
                    SortOrder = sortOrder,
                    MeetingLocation = meetingLocation,
                    MeetingScheduledTime = scheduledTime
                };

                viewModel.CombinedEvents = pagedCombinedEvents;

                return View(viewModel);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving exchange details for ExchangeId {ExchangeId}", id);
                TempData["ErrorMessage"] = "An error occurred while retrieving exchange details.";
                return RedirectToAction("Index", "ExchangeDashboard");
            }
        }

        /// <summary>
        /// Retrieves the current user's ID from the claims.
        /// </summary>
        /// <returns>The user ID as an integer.</returns>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogInformation("Current user ID from claims: {UserId}", userId);
                return userId;
            }
            throw new Exception("User ID not found in claims.");
        }
    }
}
