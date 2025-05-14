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
            var rawOffers = await _db.TblOffers
                .AsNoTracking()
                .Include(o => o.User)
                .Where(o =>
                    o.IsActive &&
                    !o.IsDeleted &&
                    o.UserId != userId &&
                    // Housekeeping #3 (for the offer): must have at least one SkillIdOfferOwner AND one WillingSkill
                    !string.IsNullOrWhiteSpace(o.SkillIdOfferOwner) &&
                    !string.IsNullOrWhiteSpace(o.WillingSkill)
                )
                .ToListAsync();

            if (!rawOffers.Any())
                return Array.Empty<OfferCardVM>();

            // 3) Build a skill-ID → skill-name lookup
            var skillLookup = await _db.TblSkills
                .AsNoTracking()
                .ToDictionaryAsync(s => s.SkillId, s => s.SkillName);

            var offerIds = rawOffers.Select(o => o.OfferId).ToList();

            // batch review stats
            var reviewStats = await _db.TblReviews
                .AsNoTracking()
                .Where(r => offerIds.Contains(r.OfferId))
                .GroupBy(r => r.OfferId)
                .Select(g => new
                {
                    g.Key,
                    AvgRating = g.Average(r => (double?)r.Rating) ?? 0,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Key);

            var matches = new List<OfferCardVM>();
            foreach (var o in rawOffers)
            {
                // --- parse their offered skill IDs → names
                var offeredIds = (o.SkillIdOfferOwner ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id.Trim(), out var x) ? (int?)x : null)
                    .Where(x => x.HasValue)
                    .Select(x => x.Value)
                    .ToList();

                var offeredNames = offeredIds
                    .Where(id => skillLookup.ContainsKey(id))
                    .Select(id => skillLookup[id])
                    .ToList();

                // --- parse their willing skills
                var wantsMe = o.WillingSkill
                    .ToSkillList();

                // --- check #1: do they offer any skill in myDesired?
                bool offersSomethingIWant = offeredNames
                    .Intersect(myDesired, StringComparer.OrdinalIgnoreCase)
                    .Any();

                // --- check #2: do they want any skill in myOffered?
                bool wantsSomethingIOffer = wantsMe
                    .Intersect(myOffered, StringComparer.OrdinalIgnoreCase)
                    .Any();

                if (!(offersSomethingIWant && wantsSomethingIOffer))
                    continue;

                // --- combine
                bool passesBothChecks = offersSomethingIWant && wantsSomethingIOffer;

                if (!passesBothChecks)
                    continue;

                if (passesBothChecks)
                {
                    var stats = reviewStats.GetValueOrDefault(o.OfferId);
                    // finally map to your OfferCardVM
                    matches.Add(new OfferCardVM
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        Category = o.Category,
                        TimeCommitmentDays = o.TimeCommitmentDays,
                        PortfolioImages = string.IsNullOrWhiteSpace(o.Portfolio)
                        ? new List<string>()
                        : JsonConvert.DeserializeObject<List<string>>(o.Portfolio),
                        AverageRating = stats?.AvgRating ?? 0,
                        ReviewCount = stats?.Count ?? 0,
                        UserName = o.User.UserName,
                        UserProfileImage = o.User.ProfileImageUrl
                    });
                }
            }

            // 4) Return up to 4 suggestions
            return matches.Take(4).ToList();
        }
    }
}


