using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SkillSwap_Platform.HelperClass.Extensions;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.OfferPublicVM;

namespace SkillSwap_Platform.Services.Matchmaking
{
    public class MatchmakingService : IMatchmakingService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            SkillSwapDbContext db,
            ILogger<MatchmakingService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<OfferCardVM>> GetSuggestedOffersForUserAsync(int userId)
        {
            // 1) Load the user’s CSV fields
            var user = await _db.TblUsers
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.OfferedSkillAreas,
                    u.DesiredSkillAreas
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return Array.Empty<OfferCardVM>();

            var myOffered = user.OfferedSkillAreas.ToSkillList();
            var myDesired = user.DesiredSkillAreas.ToSkillList();
            if (!myOffered.Any() || !myDesired.Any())
                return Array.Empty<OfferCardVM>();

            // 2) Load all “active” offers first (we’ll triage in memory)
            var query = _db.TblOffers
        .AsNoTracking()
        .Where(o =>
            o.IsActive &&
            !o.IsDeleted &&
            o.UserId != userId &&
            !string.IsNullOrWhiteSpace(o.SkillIdOfferOwner) &&
            !string.IsNullOrWhiteSpace(o.WillingSkill))
        .Include(o => o.User)
        // group‐join to reviews
        .GroupJoin(_db.TblReviews,
            offer => offer.OfferId,
            review => review.OfferId,
            (offer, reviews) => new { offer, reviews })
        .Select(x => new
        {
            x.offer,
            // compute the stats server-side
            AvgRating = x.reviews.Any() ? x.reviews.Average(r => (double?)r.Rating) : 0,
            ReviewCount = x.reviews.Count()
        });

            // 3) Materialize and filter in memory
            var raw = await query.ToListAsync();

            var skillLookup = await _db.TblSkills
                .AsNoTracking()
                .ToDictionaryAsync(s => s.SkillId, s => s.SkillName);

            var matches = raw
                .Select(x =>
                {
                    // parse their offered IDs → names
                    var offeredIds = x.offer.SkillIdOfferOwner
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var i) ? (int?)i : null)
                        .Where(i => i.HasValue)
                        .Select(i => i.Value)
                        .ToList();

                    var offeredNames = offeredIds
                        .Where(id => skillLookup.ContainsKey(id))
                        .Select(id => skillLookup[id])
                        .ToList();

                    // parse their willing skills
                    var wantsMe = x.offer.WillingSkill.ToSkillList();

                    // checks
                    var offersSomethingIWant = offeredNames.Intersect(myDesired, StringComparer.OrdinalIgnoreCase).Any();
                    var wantsSomethingIOffer = wantsMe.Intersect(myOffered, StringComparer.OrdinalIgnoreCase).Any();

                    return new
                    {
                        Offer = x.offer,
                        Passes = offersSomethingIWant && wantsSomethingIOffer,
                        x.AvgRating,
                        x.ReviewCount
                    };
                })
                .Where(x => x.Passes)
                .Select(x => new OfferCardVM
                {
                    OfferId = x.Offer.OfferId,
                    Title = x.Offer.Title,
                    Category = x.Offer.Category,
                    TimeCommitmentDays = x.Offer.TimeCommitmentDays,
                    PortfolioImages = string.IsNullOrWhiteSpace(x.Offer.Portfolio)
                                          ? new List<string>()
                                          : JsonConvert.DeserializeObject<List<string>>(x.Offer.Portfolio),
                    AverageRating = x.AvgRating ?? 0,
                    ReviewCount = x.ReviewCount,
                    UserName = x.Offer.User.UserName,
                    UserProfileImage = x.Offer.User.ProfileImageUrl
                })
                .Take(4)
                .ToList();

            return matches;
        }
    }
}


