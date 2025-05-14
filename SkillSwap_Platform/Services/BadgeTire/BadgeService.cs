using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.BadgeTire;
using static SkillSwap_Platform.Models.ViewModels.BadgeTire.BadgeDefinition;

namespace SkillSwap_Platform.Services.BadgeTire
{
    public class BadgeService
    {
        private readonly SkillSwapDbContext _ctx;
        private readonly List<BadgeDefinition> _defs;
        private const int SwapSavantId = 9;
        private const int MasterofMastersId = 11;

        public BadgeService(
            SkillSwapDbContext ctx)
        {
            _ctx = ctx;

            // 4.1 Central badge definitions
            _defs = new List<BadgeDefinition>
            {
                new BadgeDefinition(1, "Onboarding Ace",
                    "Complete all onboarding steps", BadgeTier.Common, IsProfileComplete, "~/template_assets/images/BadgeTire/Profile_Complete_1.png"),

                new BadgeDefinition(2, "Initiation Trailblazer",
                    "Complete your first session", BadgeTier.Common, HasCompletedFirstExchange, "~/template_assets/images/BadgeTire/First_Exchange_2.png"),

                new BadgeDefinition(3, "5 Exchanges",
                    "Complete five sessions", BadgeTier.Uncommon, HasCompletedFiveExchanges, "~/template_assets/images/BadgeTire/5_Exchanges_3.png"),

                new BadgeDefinition(4, "Domain Voyager",
                    "Explore three distinct categories", BadgeTier.Uncommon, HasExploredThreeCategories, "~/template_assets/images/BadgeTire/Polyglot_Starter_4.png"),

                new BadgeDefinition(5, "Stellar Guide",
                    "Teach ≥10 sessions with avg rating ≥4.5", BadgeTier.Rare, HasEarnedFiveStarMentor, "~/template_assets/images/BadgeTire/5-Stars_Instructor_5.png"),

                new BadgeDefinition(6, "Category Conqueror",
                    "Complete 20 exchanges in a single category", BadgeTier.Rare, HasCompletedTwentyTasksSingleCategory, "~/template_assets/images/BadgeTire/Language_Master_6.png"),

                new BadgeDefinition(7, "Feedback Champion",
                    "Write ≥10 high-quality reviews (rating≥4 & helpful net ≥2)", BadgeTier.Epic, HasGivenHelpfulReviews, "~/template_assets/images/BadgeTire/Community_Builder_7.png"),
                    
                new BadgeDefinition(8, "Summit Leader",
                    "Maintain the highest job-success rate and average review rating", BadgeTier.Epic, IsTopPerformer, "~/template_assets/images/BadgeTire/Champion_8.png"),

                new BadgeDefinition(9, "Badge Connoisseur",
                    "Earn five different badges", BadgeTier.Legendary, HasEarnedFiveBadges, "~/template_assets/images/BadgeTire/Gold_tire.png"),

                new BadgeDefinition(10, "Badge Connoisseur",
                    "Earn all other eight badges", BadgeTier.Legendary, HasAchievedUltimateMastery, "~/template_assets/images/BadgeTire/Ultimate_tire.png"),

                new BadgeDefinition(11, "Legendary Virtuoso",
                    "Earn every badge twice", BadgeTier.Mythic, HasEarnedAllBadgesTwice, "~/template_assets/images/BadgeTire/Ultimate_tire_2.png")
            };  
        }

        /// <summary>
        /// 4.2 Evaluate all badges for a user and award any new ones.
        /// </summary>
        public void EvaluateAndAward(int userId)
        {
            foreach (var def in _defs)
            {
                try
                {
                    bool meets = def.Criteria(_ctx, userId);
                    bool awarded = _ctx.TblBadgeAwards
                                       .Any(a => a.UserId == userId && a.BadgeId == def.BadgeId);

                    if (meets && !awarded)
                    {
                        _ctx.TblBadgeAwards.Add(new TblBadgeAward
                        {
                            UserId = userId,
                            BadgeId = def.BadgeId,
                            AwardedAt = DateTime.UtcNow
                        });
                        _ctx.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    // Replace with your logging/telemetry framework
                    Console.Error.WriteLine(
                      $"[BadgeService] Error awarding “{def.Name}” to user {userId}: {ex}");
                }
            }
        }

        // --- 4.3 Criteria methods ---

        private static bool IsProfileComplete(SkillSwapDbContext ctx, int userId)
            => ctx.TblOnboardingProgresses.Count(p => p.UserId == userId) >= 3;

        private static bool HasCompletedFirstExchange(SkillSwapDbContext ctx, int userId)
        => ctx.TblExchanges.Any(e =>
               e.Status == "Completed"
            && (e.OfferOwnerId == userId || e.OtherUserId == userId));

        private static bool HasCompletedFiveExchanges(SkillSwapDbContext ctx, int userId)
            => ctx.TblExchanges.Count(e =>
                   e.Status == "Completed"
                && (e.OfferOwnerId == userId || e.OtherUserId == userId))
               >= 5;

        private static bool HasExploredThreeCategories(SkillSwapDbContext ctx, int userId)
            => ctx.TblExchanges
                  .Where(e =>
                     e.Status == "Completed"
                  && (e.OfferOwnerId == userId || e.OtherUserId == userId))
                  .Join(ctx.TblOffers,
                        exch => exch.OfferId,
                        off => off.OfferId,
                        (exch, off) => off.Category)
                  .Distinct()
                  .Count() >= 3;

        private static bool HasEarnedFiveStarMentor(SkillSwapDbContext ctx, int userId)
        {
            var ratings = ctx.TblReviews
                     .Where(r => r.RevieweeId == userId)
                     .Select(r => (double)r.Rating);

            int count = ratings.Count();
            if (count < 10)
                return false;

            double avg = ratings.Average();
            return avg >= 4.5;
        }

        private static bool HasCompletedTwentyTasksSingleCategory(SkillSwapDbContext ctx, int userId)
            => ctx.TblExchanges
                  .Where(e =>
                     e.Status == "Completed"
                  && (e.OfferOwnerId == userId || e.OtherUserId == userId))
                  .Join(ctx.TblOffers,
                        exch => exch.OfferId,
                        off => off.OfferId,
                        (exch, off) => off.Category)
                  .GroupBy(cat => cat)
                  .Any(g => g.Count() >= 20);

        private static bool HasGivenHelpfulReviews(SkillSwapDbContext ctx, int userId)
        {
            // Reviews authored by the user
            var positiveReviews = ctx.TblReviews
                .Where(r => r.ReviewerId == userId
                         && r.Rating >= 4
                && (r.HelpfulCount - r.NotHelpfulCount) >= 2)
                .Count();

            return positiveReviews >= 10;
        }

        /// <summary>
        /// No date window—simply picks the user with the highest composite score
        /// across all completed, successful exchanges.
        /// </summary>
        private static bool IsTopPerformer(SkillSwapDbContext ctx, int userId)
        {
            var top = ctx.TblExchanges
                .Where(e => e.Status == "Completed" && e.IsSuccessful)
                .GroupBy(e => e.OfferOwnerId)
                .Select(g => new
                {
                    UserId = g.Key,
                    ExchangeCount = g.Count(),
                    AvgIncomingRating = ctx.TblReviews
                        .Where(r => r.RevieweeId == g.Key)
                        .Select(r => r.Rating)
                        .DefaultIfEmpty(0)
                        .Average(),
                    RecPct = ctx.TblUsers
                        .Where(u => u.UserId == g.Key)
                        .Select(u => u.RecommendedPercentage)
                        .FirstOrDefault()
                })
                .Select(x => new
                {
                    x.UserId,
                    Score = x.ExchangeCount
                          + x.AvgIncomingRating * 2.0
                          + (x.RecPct / 100.0) * 1.5
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return top != null && top.UserId == userId;
        }

        private static bool HasEarnedFiveBadges(SkillSwapDbContext ctx, int userId)
        {
            // count how many badges this user already has
            int awardCount = ctx.TblBadgeAwards
                               .Count(a => a.UserId == userId);
            return awardCount >= 5;
        }

        private bool HasAchievedUltimateMastery(SkillSwapDbContext ctx, int userId)
        {
            var earned = ctx.TblBadgeAwards
                            .Where(a => a.UserId == userId)
                            .Select(a => a.BadgeId)
                            .ToHashSet();

            return _defs
                   .Where(d => d.BadgeId != SwapSavantId)
                   .All(d => earned.Contains(d.BadgeId));
        }

        private bool HasEarnedAllBadgesTwice(SkillSwapDbContext ctx, int userId)
        {
            // build set of all other badge IDs
            var otherBadgeIds = _defs
                    .Select(d => d.BadgeId)
                    .Where(id => id != MasterofMastersId);

            return otherBadgeIds
              .All(bid =>
                ctx.TblBadgeAwards.Count(a => a.UserId == userId && a.BadgeId == bid) >= 2
              );
        }

    }
}