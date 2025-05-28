using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis.Text;
using SkillSwap_Platform.Services.NotificationTrack;
using SkillSwap_Platform.Services.DigitalToken;
using Newtonsoft.Json;
using SkillSwap_Platform.Services.BadgeTire;
using Google.Apis.Calendar.v3.Data;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ExchangeDashboardController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ExchangeDashboardController> _logger;
        private readonly INotificationService _notif;
        private readonly IDigitalTokenService _tokenService;
        private readonly BadgeService _badgeService;

        public ExchangeDashboardController(SkillSwapDbContext context, ILogger<ExchangeDashboardController> logger, INotificationService notif, IDigitalTokenService tokenService, BadgeService badgeService)
        {
            _context = context;
            _logger = logger;
            _notif = notif;
            _tokenService = tokenService;
            _badgeService = badgeService;
        }

        #region Active Dashboard

        // GET: /ExchangeDashboard/
        public async Task<IActionResult> Index(int page = 1)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                int pageSize = 5;

                // Retrieve exchanges where the current user is either the requester or the offer owner
                var exchanges = await _context.TblExchanges
                    .Include(e => e.Offer)
                    .Where(e => (e.OtherUserId ?? 0) == currentUserId
                        || (e.OfferOwnerId ?? 0) == currentUserId)
                    .OrderByDescending(e => e.ExchangeDate)
                    .ToListAsync();

                var excludedStatuses = new[] { "Declined", "Cancellation Requested", "Cancelled" };

                // Filter active exchanges only.
                exchanges = exchanges
                    .Where(e => !e.IsCompleted
                             && !excludedStatuses.Contains(e.Status, StringComparer.OrdinalIgnoreCase))
                    .ToList();
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
                    string? offerImageUrl = null;
                    if (exchange.Offer != null && !string.IsNullOrEmpty(exchange.Offer.Portfolio))
                    {
                        var urls = JsonConvert.DeserializeObject<List<string>>(exchange.Offer.Portfolio);
                        offerImageUrl = urls?.FirstOrDefault();
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

                    // In the dashboard Items loop
                    bool isOnlineCompleted = false;
                    if (!string.IsNullOrEmpty(exchange.ExchangeMode) &&
                        exchange.ExchangeMode.Equals("online", StringComparison.OrdinalIgnoreCase))
                    {
                        // Find at least one online meeting session for this exchange marked as Completed.
                        var onlineMeetingCompleted = await _context.TblMeetings
                            .Where(m => m.ExchangeId == exchange.ExchangeId && m.Status == "Completed")
                            .FirstOrDefaultAsync();
                        isOnlineCompleted = (onlineMeetingCompleted != null);
                    }

                    var offer = exchange.Offer;

                    var proof = await _context.TblInpersonMeetingProofs
                        .FirstOrDefaultAsync(p => p.ExchangeId == exchange.ExchangeId);

                    // new:
                    bool isDeleted = offer?.IsDeleted ?? false;

                    // Build the dashboard item.
                    var dashboardItem = new ExchangeDashboardItemVM
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
                        IsOnlineMeetingCompleted = isOnlineCompleted,
                        MeetingScheduledDateTime = meetingRecord?.MeetingScheduledDateTime,
                        InpersonMeetingDurationMinutes = meetingRecord?.InpersonMeetingDurationMinutes,
                        InPersonOwnerVerified = meetingRecord?.IsInpersonMeetingVerifiedByOfferOwner ?? false,
                        InPersonOtherPartyVerified = meetingRecord?.IsInpersonMeetingVerifiedByOtherParty ?? false,
                        IsMeetingEnded = meetingRecord?.IsMeetingEnded ?? false,
                        OfferIsDeleted = isDeleted,
                        StatusChangeReason = exchange.StatusChangeReason,
                        StatusChangeBy = exchange.LastStatusChangedBy
                    };
                    dashboardItem.OfferImageUrl = offerImageUrl;
                    dashboardItems.Add(dashboardItem);
                }

                // Implement paging on active dashboard items.
                int totalActive = dashboardItems.Count;
                var pagedActive = dashboardItems
                    .OrderByDescending(item => item.Exchange.ExchangeDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var viewModel = new ExchangeDashboardVM
                {
                    ActiveExchangeItems = pagedActive,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling(totalActive / (double)pageSize)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading exchange dashboard for user {UserId}", GetCurrentUserId());
                TempData["ErrorMessage"] = "An error occurred while loading the exchange dashboard.";
                return RedirectToAction("EP500", "EP");
            }
        }

        #endregion

        #region Exchange History
        // GET: /ExchangeDashboard/ExchangeHistory
        [Authorize]
        public async Task<IActionResult> ExchangeHistory(int completedPage = 1, int declinedPage = 1)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                int pageSize = 5;

                // Retrieve all exchanges for the current user.
                var exchanges = await _context.TblExchanges
                    .Include(e => e.Offer)
                    .Where(e => (e.OtherUserId ?? 0) == currentUserId || (e.OfferOwnerId ?? 0) == currentUserId)
                    .OrderByDescending(e => e.ExchangeDate)
                    .ToListAsync();

                // Filter history exchanges into two categories:
                // Completed: exchanges that are marked as completed (and not declined).
                // Declined: exchanges whose Status equals "Declined" (you could also check for another flag if needed).
                var completedExchanges = exchanges
                    .Where(e => e.IsCompleted && !e.Status.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var declinedExchanges = exchanges
                    .Where(e => e.Status.Equals("Declined", StringComparison.OrdinalIgnoreCase)
                             || e.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Build dashboard items for both lists.
                Func<TblExchange, Task<ExchangeDashboardItemVM>> buildDashboardItem = async (exchange) =>
                {
                    var meetingRecord = await _context.TblInPersonMeetings.FirstOrDefaultAsync(m => m.ExchangeId == exchange.ExchangeId);

                    var history = await _context.TblExchangeHistories
                        .Where(h => h.ExchangeId == exchange.ExchangeId)
                        .OrderByDescending(h => h.ChangeDate)
                        .ToListAsync();

                    int offerOwnerId = exchange.OfferOwnerId ?? 0;
                    int otherUserIdFromExchange = exchange.OtherUserId ?? 0;
                    int otherUserId = (currentUserId == offerOwnerId) ? otherUserIdFromExchange : offerOwnerId;

                    int totalSessionsCount = await _context.TblMeetings
                        .Where(m => m.ExchangeId == exchange.ExchangeId)
                        .CountAsync();

                    string offerImageUrl = "/template_assets/images/default-offer.png";
                    if (exchange.Offer != null && !string.IsNullOrEmpty(exchange.Offer.Portfolio))
                    {
                        var portfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(exchange.Offer.Portfolio);
                        if (portfolioUrls != null && portfolioUrls.Any())
                        {
                            offerImageUrl = portfolioUrls.First();
                        }
                    }

                    var contract = await _context.TblContracts
                        .Where(c => c.OfferId == exchange.OfferId)
                        .OrderByDescending(c => c.ContractId)
                        .FirstOrDefaultAsync();
                    string contractUniqueId = contract != null ? contract.ContractUniqueId : exchange.ExchangeId.ToString();

                    string lastStatusChangedByName = "N/A";
                    if (exchange.LastStatusChangedBy.HasValue)
                    {
                        var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == exchange.LastStatusChangedBy.Value);
                        if (user != null)
                            lastStatusChangedByName = user.UserName;
                    }

                    bool isOnlineCompleted = false;
                    if (!string.IsNullOrEmpty(exchange.ExchangeMode) &&
                        exchange.ExchangeMode.Equals("online", StringComparison.OrdinalIgnoreCase))
                    {
                        var onlineMeetingCompleted = await _context.TblMeetings
                            .Where(m => m.ExchangeId == exchange.ExchangeId && m.Status == "Completed")
                            .FirstOrDefaultAsync();
                        isOnlineCompleted = (onlineMeetingCompleted != null);
                    }

                    var offer = exchange.Offer;

                    // new:
                    bool isDeleted = offer?.IsDeleted ?? false;

                    return new ExchangeDashboardItemVM
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
                        CanLaunchMeeting = false, // History view typically won't show meeting launch options.
                        RecentMeetingLaunchCount = totalSessionsCount,
                        Category = exchange.Offer.Category,
                        Token = contract.TokenOffer,
                        IsOnlineMeetingCompleted = isOnlineCompleted,
                        IsMeetingEnded = exchange.IsMeetingEnded,
                        MeetingScheduledDateTime = meetingRecord?.MeetingScheduledDateTime,
                        InpersonMeetingDurationMinutes = meetingRecord?.InpersonMeetingDurationMinutes,
                        OfferIsDeleted = isDeleted,
                        StatusChangeReason = exchange.StatusChangeReason
                    };
                };

                var completedItems = new List<ExchangeDashboardItemVM>();
                foreach (var ex in completedExchanges)
                {
                    completedItems.Add(await buildDashboardItem(ex));
                }

                var declinedItems = new List<ExchangeDashboardItemVM>();
                foreach (var ex in declinedExchanges)
                {
                    declinedItems.Add(await buildDashboardItem(ex));
                }

                // Apply paging separately.
                int totalCompleted = completedItems.Count;
                int totalDeclined = declinedItems.Count;

                var pagedCompleted = completedItems
                    .OrderByDescending(item => item.Exchange.ExchangeDate)
                    .Skip((completedPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                var pagedDeclined = declinedItems
                    .OrderByDescending(item => item.Exchange.ExchangeDate)
                    .Skip((declinedPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var purchasedExchangeIds = await _context.TblCertificatePurchases
                    .Where(c => c.UserId == currentUserId)
                    .Select(c => c.ExchangeId)
                    .ToListAsync();

                // For simplicity, assume paging only on one set if needed.
                // Here, we build the view model without paging controls.
                var viewModel = new ExchangeDashboardVM
                {
                    ActiveExchangeItems = new List<ExchangeDashboardItemVM>(), // Not shown on history page.
                    CompletedExchangeItems = pagedCompleted,
                    DeclinedExchangeItems = pagedDeclined,
                    CompletedCurrentPage = completedPage,
                    CompletedTotalPages = (int)Math.Ceiling(totalCompleted / (double)pageSize),
                    DeclinedCurrentPage = declinedPage,
                    DeclinedTotalPages = (int)Math.Ceiling(totalDeclined / (double)pageSize),
                    PurchasedCertificates = purchasedExchangeIds
                };

                return View("ExchangeHistory", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading exchange history for user {UserId}", GetCurrentUserId());
                TempData["ErrorMessage"] = "An error occurred while loading the exchange history.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Exchange Details
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
                    return RedirectToAction("EP404", "EP");
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

                var timelineEvents = allTimelineHistory
    .AsEnumerable()  // switch to LINQ-to-Objects so you can use a statement body lambda
    .Select(h =>
    {
        string statusChangedBy = "N/A";
        if (h.ChangedBy.HasValue)
        {
            // This lookup now occurs in memory.
            var user = _context.TblUsers.FirstOrDefault(u => u.UserId == h.ChangedBy.Value);
            if (user != null)
                statusChangedBy = user.UserName;
        }
        return new ExchangeEventVM
        {
            EventDate = h.ChangeDate,
            EventType = "Timeline/Change",
            StepOrMeetingType = h.ChangedStatus,
            Description = h.Reason,
            SessionNumber = null,
            MeetingStartTime = null,
            StatusChangedByName = statusChangedBy
        };
    })
    .ToList();


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
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Exchange Staus Marking
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkExchangeCompleted(int exchangeId)
        {
            var exchange = await _context.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("EP404", "EP");
            }

            var currentUserId = GetCurrentUserId();

            // 1) Ensure any meeting requirements are met...
            if (exchange.ExchangeMode.Equals("online", StringComparison.OrdinalIgnoreCase))
            {
                // Check that there is a completed online meeting (if required).
                var completedOnlineMeeting = await _context.TblMeetings
                .Where(m => m.ExchangeId == exchangeId && m.Status == "Completed")
                .FirstOrDefaultAsync();
                if (completedOnlineMeeting == null)
                {
                    TempData["ErrorMessage"] = "You must complete an online meeting before marking the exchange as complete.";
                    return RedirectToAction("Index", "ExchangeDashboard");
                }
            }
            else if (exchange.ExchangeMode.Contains("in-person", StringComparison.OrdinalIgnoreCase))
            {
                // 1) OTP verification:
                var inPerson = await _context.TblInPersonMeetings
                    .Where(m => m.ExchangeId == exchangeId)
                    .OrderByDescending(m => m.CreatedDate)
                    .FirstOrDefaultAsync();
                var proof = await _context.TblInpersonMeetingProofs
                    .FirstOrDefaultAsync(p => p.ExchangeId == exchangeId);

                if (inPerson == null
                    || !inPerson.IsInpersonMeetingVerifiedByOfferOwner
                    || !inPerson.IsInpersonMeetingVerifiedByOtherParty
                    || proof == null
                    || proof.EndProofDateTime == null
                    || string.IsNullOrWhiteSpace(proof.EndProofImageUrl))
                {
                    TempData["ErrorMessage"] = "Both parties must verify and submit end-meeting proof before completing.";
                    return RedirectToAction("Index");
                }
            }

            try
            {
                if (currentUserId == exchange.OfferOwnerId)
                    exchange.IsCompletedByOfferOwner = true;
                else if (currentUserId == exchange.OtherUserId)
                    exchange.IsCompletedByOtherParty = true;
                else
                {
                    return Forbid();
                }

                // 3) If *both* have now clicked “done”, finalize and release tokens
                if (exchange.IsCompletedByOfferOwner && exchange.IsCompletedByOtherParty)
                {
                    exchange.Status = "Completed";
                    exchange.IsCompleted = true;
                    exchange.CompletionDate = DateTime.UtcNow;

                    // release held tokens
                    await _tokenService.ReleaseTokensAsync(exchangeId);

                    var escrow = await _context.TblEscrows
                           .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                    if (escrow != null)
                    {
                        escrow.Status = "Released";
                        escrow.ReleasedAt = DateTime.UtcNow;
                        _context.TblEscrows.Update(escrow);
                        await _context.SaveChangesAsync();
                    }

                    // log a full‐exchange history entry
                    _context.TblExchangeHistories.Add(new TblExchangeHistory
                    {
                        ExchangeId = exchangeId,
                        OfferId = exchange.OfferId,
                        ChangeDate = DateTime.UtcNow,
                        ChangedStatus = "Exchange Completed",
                        Reason = "Both parties confirmed completion.",
                        ChangedBy = currentUserId
                    });
                }
                else
                {
                    // log a “partial” history entry
                    _context.TblExchangeHistories.Add(new TblExchangeHistory
                    {
                        ExchangeId = exchangeId,
                        OfferId = exchange.OfferId,
                        ChangeDate = DateTime.UtcNow,
                        ChangedStatus = "Completion Confirmation",
                        Reason = currentUserId == exchange.OfferOwnerId
                                           ? "Offer owner confirmed completion."
                                           : "Other party confirmed completion.",
                        ChangedBy = currentUserId
                    });
                    TempData["SuccessMessage"] = "Your completion has been recorded. Waiting on the other party.";
                }
                await _context.SaveChangesAsync();

                // Award *all* exchange‐based badges (First Exchange, 5 Exchanges,
                // Category Explorer, Skill Master, Performance Champion.) for both participants:
                _badgeService.EvaluateAndAward(exchange.OfferOwnerId!.Value);
                _badgeService.EvaluateAndAward(exchange.OtherUserId!.Value);

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = GetCurrentUserId(),
                    Title = "Exchange Marked as Completed",
                    Message = "You successfully completed your exchange.",
                    Url = Url.Action("Index", "ExchangeDashboard"),
                });

                if (currentUserId == exchange.OfferOwnerId)
                {
                    TempData["SuccessMessage"] = "🎉 Hooray! You’ve completed your exchange.";
                    return RedirectToAction("Index", "ExchangeDashboard");
                }

                TempData["SuccessMessage"] = "Exchange marked as completed.";

                // Redirect to the review page for that exchange.
                return RedirectToAction("ReviewExchange", new { exchangeId = exchange.ExchangeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking exchange {ExchangeId} as completed", exchangeId);
                TempData["ErrorMessage"] = "An error occurred while marking the exchange as completed.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CancelExchange(RequestCancelVM vm)
        {
            try
            {
                // 1) Verify the exchange exists and is not already completed/declined
                var exchange = await _context.TblExchanges
                    .FirstOrDefaultAsync(e => e.ExchangeId == vm.ExchangeId);

                if (exchange == null)
                {
                    TempData["ErrorMessage"] = "Exchange not found.";
                    return RedirectToAction("EP404", "EP");
                }

                // 1) If we are here with a "Cancellation Requested" _and_ it's the OTHER party approving:
                if (exchange.Status == "Cancellation Requested"
                    && exchange.LastStatusChangedBy.HasValue
                    && exchange.LastStatusChangedBy.Value != GetCurrentUserId())
                {
                    // partner has approved your cancellation request => finalize cancellation
                    exchange.Status = "Cancelled";
                    exchange.LastStatusChangedBy = null;
                    exchange.StatusChangeReason = null;

                    // refund tokens, log history, notify both parties...
                    await _tokenService.RefundTokensAsync(exchange.ExchangeId);

                    var escrow = await _context.TblEscrows
                           .FirstOrDefaultAsync(e => e.ExchangeId == vm.ExchangeId);
                    if (escrow != null)
                    {
                        escrow.Status = "Refunded";
                        escrow.RefundedAt = DateTime.UtcNow;
                        _context.TblEscrows.Update(escrow);
                        await _context.SaveChangesAsync();
                    }

                    _context.TblExchangeHistories.Add(new TblExchangeHistory
                    {
                        ExchangeId = exchange.ExchangeId,
                        OfferId = exchange.OfferId,
                        ChangeDate = DateTime.UtcNow,
                        ChangedStatus = "Cancelled",
                        Reason = "Cancellation approved by partner.",
                        ChangedBy = GetCurrentUserId()
                    });

                    await _context.SaveChangesAsync();

                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = GetCurrentUserId(),
                        Title = "Exchange Cancelled",
                        Message = $"Exchange #{exchange.ExchangeId} cancellation approved.",
                        Url = Url.Action("Index", "ExchangeDashboard")
                    });

                    TempData["SuccessMessage"] = "Cancellation approved and token held in escrow will refunded to original account.";
                    return RedirectToAction("Index");
                }

                // 2) Otherwise this is the _first_ click: the user is _requesting_ a cancellation
                if (exchange.IsCompleted || exchange.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "This exchange cannot be cancelled.";
                    return RedirectToAction("Index");
                }

                exchange.Status = "Cancellation Requested";
                exchange.LastStatusChangedBy = GetCurrentUserId();
                exchange.StatusChangeReason = vm.Reason;
                await _context.SaveChangesAsync();

                _context.TblExchangeHistories.Add(new TblExchangeHistory
                {
                    ExchangeId = exchange.ExchangeId,
                    OfferId = exchange.OfferId,
                    ChangeDate = DateTime.UtcNow,
                    ChangedStatus = "Cancellation Requested",
                    Reason = vm.Reason,
                    ChangedBy = GetCurrentUserId()
                });
                await _context.SaveChangesAsync();

                await _notif.AddAsync(new TblNotification
                {
                    UserId = GetCurrentUserId(),
                    Title = "Cancellation Requested",
                    Message = $"You requested cancellation for exchange #{exchange.ExchangeId}.",
                    Url = Url.Action("Index", "ExchangeDashboard")
                });

                TempData["SuccessMessage"] =
                    "Cancellation requested. Your partner will need to approve it before it’s final.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling exchange {ExchangeId}", vm.ExchangeId);
                TempData["ErrorMessage"] = "An error occurred while cancelling the exchange.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> DenyCancellation(RequestCancelVM vm)
        {
            var exchange = await _context.TblExchanges
                .FirstOrDefaultAsync(e => e.ExchangeId == vm.ExchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("Index");
            }

            var me = GetCurrentUserId();

            // only allow deny if there is a pending request by the other party
            if (exchange.Status == "Cancellation Requested"
                && exchange.LastStatusChangedBy.HasValue
                && exchange.LastStatusChangedBy.Value != me)
            {
                // reset back to active
                exchange.Status = "In Progress";
                exchange.LastStatusChangedBy = null;
                exchange.StatusChangeReason = null;
                await _context.SaveChangesAsync();

                _context.TblExchangeHistories.Add(new TblExchangeHistory
                {
                    ExchangeId = exchange.ExchangeId,
                    OfferId = exchange.OfferId,
                    ChangeDate = DateTime.UtcNow,
                    ChangedStatus = "Cancellation Denied",
                    Reason = "Partner denied cancellation.",
                    ChangedBy = me
                });
                await _context.SaveChangesAsync();

                await _notif.AddAsync(new TblNotification
                {
                    UserId = me,
                    Title = "Cancellation Denied",
                    Message = $"You denied cancellation for exchange #{exchange.ExchangeId}.",
                    Url = Url.Action("Index")
                });

                TempData["SuccessMessage"] = "Cancellation request denied, back to normal flow.";
            }
            else
            {
                TempData["ErrorMessage"] = "No pending cancellation request to deny.";
            }

            return RedirectToAction("Index");
        }
        #endregion

        [HttpGet]
        public async Task<IActionResult> ReviewExchange(int exchangeId)
        {
            // Retrieve the exchange record (optional, to show details on the review page).
            var exchange = await _context.TblExchanges
                .Include(e => e.Offer)
                .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("EP404", "EP");
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == exchange.OfferOwnerId)
            {
                TempData["SuccessMessage"] = "🎉 You’ve already wrapped up this swap!";
                return RedirectToAction("Index", "ExchangeDashboard");
            }

            // Check if cookies for reviewer details exist.
            string reviewerName = Request.Cookies["ReviewerName"] ?? string.Empty;
            string reviewerEmail = Request.Cookies["ReviewerEmail"] ?? string.Empty;

            // Create a view model pre-populated with some exchange details.
            var reviewVm = new ExchangeReviewVM
            {
                ExchangeId = exchange.ExchangeId,
                OfferTitle = exchange.Offer?.Title ?? "N/A",
                ReviewerName = reviewerName,
                ReviewerEmail = reviewerEmail
            };

            return View(reviewVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewExchange(ExchangeReviewVM model)
        {
            if (!ModelState.IsValid)
            {
                // Collect the validation error messages
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return View(model);
            }

            // Retrieve the exchange record to obtain the OfferId.
            var exchange = await _context.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == model.ExchangeId);
            if (exchange == null)
            {
                TempData["ErrorMessage"] = "Exchange not found.";
                return RedirectToAction("EP404", "EP");

            }

            // Determine the current user's id.
            int reviewerId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;

            // Determine the reviewee id.
            // For example, if the current user is the OfferOwner, then the other party (reviewee) is in OtherUserId.
            // Otherwise, if the current user equals OtherUserId, then the reviewee is the OfferOwnerId.
            int revieweeId = 0;
            if (reviewerId == exchange.OfferOwnerId)
            {
                revieweeId = exchange.OtherUserId ?? 0;
            }
            else if (reviewerId == exchange.OtherUserId)
            {
                revieweeId = exchange.OfferOwnerId ?? 0;
            }
            // Optionally, add error handling if revieweeId remains 0.
            if (revieweeId == 0)
            {
                TempData["ErrorMessage"] = "\"Unable to determine the reviewee for this exchange.";
                return RedirectToAction("EP404", "EP");
            }

            // Here you would store the review details in the database.
            // For example, assume you have a TblExchangeReview table.
            var review = new TblReview
            {
                ExchangeId = model.ExchangeId,
                OfferId = exchange.OfferId,
                ReviewerId = reviewerId,            // Current user is the reviewer.
                RevieweeId = revieweeId,            // The other party being reviewed.
                Rating = model.Rating,
                Comments = model.Comments,
                CreatedDate = DateTime.UtcNow,
                ReviewerName = model.ReviewerName,
                ReviewerEmail = model.ReviewerEmail,
                UserId = revieweeId
            };

            _context.TblReviews.Add(review);
            await _context.SaveChangesAsync();

            // If the user checked RememberMe, store their details in cookies (for 30 days).
            if (model.RememberMe)
            {
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddDays(30),
                    HttpOnly = false, // if you want client-side access for autofill
                    Secure = true     // if your site uses HTTPS
                };
                model.RememberMe = true; // Ensure the model reflects the user's choice.
                Response.Cookies.Append("ReviewerName", model.ReviewerName ?? string.Empty, cookieOptions);
                Response.Cookies.Append("ReviewerEmail", model.ReviewerEmail ?? string.Empty, cookieOptions);
            }
            else
            {
                // Optionally, remove cookies if the user didn't select RememberMe.
                Response.Cookies.Delete("ReviewerName");
                Response.Cookies.Delete("ReviewerEmail");
            }

            // log notification:
            await _notif.AddAsync(new TblNotification
            {
                UserId = GetCurrentUserId(),
                Title = "Exchange Review",
                Message = "You successfully reviwed your exchange.",
                Url = Url.Action("Index", "ExchangeDashboard"),
            });

            TempData["SuccessMessage"] = "Thank you for your review!";
            // Optionally redirect to a page that shows the exchange history or back to the dashboard.
            return RedirectToAction("Index", "ExchangeDashboard");
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
