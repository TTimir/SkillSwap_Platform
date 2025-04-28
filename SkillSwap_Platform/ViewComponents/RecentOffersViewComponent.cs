using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SkillSwap_Platform.Models.ViewModels.OfferFilterVM;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models.ViewModels.OfferPublicVM;
using SkillSwap_Platform.HelperClass;

namespace SkillSwap_Platform.ViewComponents
{
    public class RecentOffersViewComponent : ViewComponent
    {
        private readonly SkillSwapDbContext _db;
        private const string CookieKey = "RecentOfferSummaries";

        public RecentOffersViewComponent(SkillSwapDbContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            const string sessionsKey = "RecentOfferSummaries";

            var recents = HttpContext.Session
                .GetObjectFromJson<List<OfferCardVM>>(sessionsKey)
                ?? new List<OfferCardVM>();

            // 2) sanitize & cap at 8
            var viewedIds = recents
                .Where(o => o.OfferId > 0)
                .Select(o => o.OfferId)
                .Distinct()
                .Take(8)
                .ToList();

            var vm = new List<OfferCardVM>();

            // 3) now do N+1 fetching, no CTEs, no GroupBy
            foreach (var offerId in viewedIds)
            {
                // a) get the offer
                var offer = await _db.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.OfferId == offerId && o.IsActive);
                if (offer == null)
                    continue;

                // b) get the user
                var user = await _db.TblUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == offer.UserId);

                // c) get all reviews for that offer
                var reviews = await _db.TblReviews
                    .AsNoTracking()
                    .Where(r => r.OfferId == offerId)
                    .ToListAsync();

                int reviewCount = reviews.Count;
                double avgRating = reviewCount > 0
                    ? reviews.Average(r => (double)r.Rating)
                    : 0;

                // d) parse portfolio JSON
                var portfolio = new List<string>();
                if (offer.TblOfferPortfolios?.Any() == true)
                {
                    // your TblOfferPortfolios presumably has an ImageUrl (adjust field name as needed)
                    portfolio = offer.TblOfferPortfolios
                        .Select(p => p.FileUrl)
                        .Where(u => !string.IsNullOrEmpty(u))
                        .ToList();
                }
                else if (!string.IsNullOrWhiteSpace(offer.Portfolio))
                {
                    try
                    {
                        portfolio = JsonConvert
                          .DeserializeObject<List<string>>(offer.Portfolio)
                          ?? new();
                    }
                    catch { /* malformed JSON? skip */ }
                }

                // e) build the VM
                vm.Add(new OfferCardVM
                {
                    OfferId = offer.OfferId,
                    ShortTitle = offer.Title.Length > 35
                                            ? offer.Title.Substring(0, 35) + "…"
                                            : offer.Title,
                    Category = offer.Category,
                    TimeCommitmentDays = offer.TimeCommitmentDays,
                    TokenCost = (int)offer.TokenCost,
                    PortfolioImages = portfolio,
                    UserName = user?.UserName ?? "Unknown",
                    UserProfileImage = string.IsNullOrEmpty(user?.ProfileImageUrl)
                                            ? "/template_assets/images/No_Profile_img.png"
                                            : user.ProfileImageUrl,
                    AverageRating = avgRating,
                    ReviewCount = reviewCount
                });
            }

            return View(vm);
        }
    }
}