using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Diagnostics;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserDashboardController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<UserProfileController> _logger;

        public UserDashboardController(SkillSwapDbContext context, ILogger<UserProfileController> logger)
        {
            _db = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Load your various counts—replace these with your real queries
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var servicesOffered = await _db.TblOffers.CountAsync(o => o.UserId == userId);
                var newServicesOffered = await _db.TblOffers.CountAsync(o =>
                    o.UserId == userId && o.CreatedDate >= DateTime.UtcNow.AddDays(-7));
                var completedServices = await _db.TblExchanges.CountAsync(e =>
                    (e.OfferOwnerId == userId || e.OtherUserId == userId) && e.Status == "Completed");
                var rawNewCompleted = await _db.TblExchanges.CountAsync(e =>
                    (e.OfferOwnerId == userId || e.OtherUserId == userId)
                    && e.Status == "Completed"
                    && e.LastStatusChangeDate >= DateTime.UtcNow.AddDays(-7));
                var newCompleted = Math.Max(0, rawNewCompleted - 2);
                var inQueue = await _db.TblExchanges.CountAsync(e =>
                    (e.OfferOwnerId == userId || e.OtherUserId == userId)
                    && e.Status != "Completed");
                var rawNewInQueue = await _db.TblExchanges.CountAsync(e =>
                    (e.OfferOwnerId == userId || e.OtherUserId == userId)
                    && e.RequestDate >= DateTime.UtcNow.AddDays(-7));
                var newInQueue = Math.Max(0, rawNewInQueue - 2);
                var totalReviews = await _db.TblReviews.CountAsync(r => r.Offer.UserId == userId);
                var rawNewReviews = await _db.TblReviews.CountAsync(r =>
                    r.Offer.UserId == userId && r.CreatedDate >= DateTime.UtcNow.AddDays(-7));
                var newReviews = Math.Max(0, rawNewReviews - 2);

                // 1️⃣ Top 7 offers by total Views
                var allOffers = await _db.TblOffers
                    .Where(o => o.UserId == userId)
                    .Select(o => new { o.Title, o.Views })
                    .ToListAsync();

                var top7 = allOffers
                    .OrderByDescending(x => x.Views)
                    .Take(7)
                    .ToList();

                // 2️⃣ Views aggregated by Category
                var byCat = await _db.TblOffers
                    .Where(o => o.UserId == userId)
                    .GroupBy(o => o.Category)
                    .Select(g => new {
                        Category = g.Key,
                        Total = g.Sum(o => o.Views)
                    })
                    .ToListAsync();

                // Top 3 most viewed services
                var topServices = await _db.TblOffers
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.Views)
                    .Take(3)
                    .Select(o => new ServiceSummary
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        Rating = o.TblReviews.Any() ? o.TblReviews.Average(r => r.Rating) : 0,
                        Location = o.User.Country,
                        ThumbnailUrl = o.TblOfferPortfolios.FirstOrDefault().FileUrl ?? "/images/default.png",
                        DetailsUrl = Url.Action("OfferDetails", "UserOfferDetails", new { id = o.OfferId }),
                        PortfolioJson = JsonConvert.SerializeObject(
                        o.TblOfferPortfolios
                         .Select(p => p.FileUrl)
                         .ToList()
                         )
                    })
                    .ToListAsync();

                var raw = await (
                    from ex in _db.TblExchanges
                    join off in _db.TblOffers on ex.OfferId equals off.OfferId
                    join usr in _db.TblUsers on ex.OtherUserId equals usr.UserId into buyers
                    from buyer in buyers.DefaultIfEmpty()
                    where ex.OfferOwnerId == userId
                    select new
                    {
                        Exchange = ex,
                        Offer = off,
                        Buyer = buyer,
                        Initiated = ex.RequestDate.HasValue
                                        ? ex.RequestDate.Value
                                        : ex.ExchangeDate
                    }
                )
                .ToListAsync();

                var recentExchanges = raw
                    .OrderByDescending(x => x.Initiated)
                    .Take(3)
                    .Select(x => new PurchaseSummary
                    {
                        BuyerName = x.Buyer?.UserName ?? "Unknown user",
                        ServiceTitle = x.Offer.Title,
                        InitiatedDate = x.Initiated,
                        Amount = x.Offer.TokenCost,
                        BuyerAvatarUrl = !string.IsNullOrWhiteSpace(x.Buyer?.ProfileImageUrl)
                                             ? x.Buyer.ProfileImageUrl
                                             : "/images/avatar-default.png"
                    })
                    .ToList();

                // Recent activity
                var activity = await _db.TblNotifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .Select(n => new ActivityItem
                    {
                        Timestamp = n.CreatedAt,
                        Title = n.Title,
                        Subtitle = n.Message,
                        // e.g. map different notification titles to different badge colors:
                        BadgeColorClass = n.Title.Contains("Swap") ? "color1"
                                        : n.Title.Contains("Aggreement") ? "color2"
                                        : n.Title.Contains("Profile") ? "color3"
                                        : "color4"
                    })
                    .ToListAsync();

                var vm = new DashboardVM
                {
                    ServicesOffered = servicesOffered,
                    NewServicesOffered = newServicesOffered,
                    CompletedServices = completedServices,
                    NewServicesCompleted = newCompleted,
                    InQueueServices = inQueue,
                    NewInQueue = newInQueue,
                    TotalReviews = totalReviews,
                    NewReviews = newReviews,
                    OfferViewLabels = top7.Select(x => x.Title).ToList(),
                    OfferViewData = top7.Select(x => (int)x.Views).ToList(),

                    CategoryLabels = byCat.Select(x => x.Category).ToList(),
                    CategoryData = byCat.Select(x => (int)x.Total).ToList(),
                    MostViewedServices = topServices,
                    RecentPurchases = recentExchanges,
                    RecentActivity = activity
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard for user {User}", User.Identity.Name);

                var errorVm = new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                };

                return View("Error", errorVm);
            }
        }
    }
}