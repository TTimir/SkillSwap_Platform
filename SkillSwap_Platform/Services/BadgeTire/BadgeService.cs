﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.BadgeTire;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static SkillSwap_Platform.Models.ViewModels.BadgeTire.BadgeDefinition;

namespace SkillSwap_Platform.Services.BadgeTire
{
    public class BadgeService
    {
        private readonly SkillSwapDbContext _ctx;
        private readonly IEmailService _email;
        private readonly List<BadgeDefinition> _defs;
        private const int SwapSavantId = 9;
        private const int MasterofMastersId = 11;
        private readonly ISubscriptionService _subs;

        public BadgeService(
            SkillSwapDbContext ctx, IEmailService email, ISubscriptionService subscription)
        {
            _ctx = ctx;
            _email = email;
            _subs = subscription;

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
            var user = _ctx.TblUsers.Find(userId);
            if (user == null) return;

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

                        SendBadgeAwardEmailAsync(userId, user.Email, user.UserName, def)
                            .ConfigureAwait(false);
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

        private async Task SendBadgeAwardEmailAsync(int userId, string toEmail, string userName, BadgeDefinition def)
        {
            // 1) figure out their plan & SLA
            var sub = await _subs.GetActiveAsync(userId);
            var plan = sub?.PlanName ?? "Free";

            var (label, sla) = plan switch
            {
                "Plus" => ("Plus Support", "72h SLA"),
                "Pro" => ("Pro Support", "48h SLA"),
                "Growth" => ("Growth Support", "24h SLA"),
                _ => ("Free Support", "120h SLA")
            };

            // 2) prefix subject
            var subject = $"[{label} · {sla}] 🏅 New Achievement Unlocked: {def.Name}!";
            var html = $@"
                <div style=""font-family:Arial,sans-serif;line-height:1.6;color:#333;"">
                  <h1 style=""margin-bottom:0.5em;color:#2a9d8f;"">🎉 Congratulations, {userName}! 🎉</h1>
                  <p style=""font-size:1.1em;"">
                    You’ve outdone yourself and just earned the <strong style=""color:#264653;"">{def.Name}</strong> badge!
                  </p>
                
                  <div style=""text-align:center;margin:1.5em 0;"">
                    <img 
                      src=""{def.IconPath}"" 
                      alt=""{def.Name} badge"" 
                      style=""width:140px;height:140px;object-fit:cover;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.15);"" />
                  </div>
                
                  <h2 style=""color:#e76f51;margin-top:0;"">{def.Name}</h2>

                  <div style=""margin-bottom:1em;"">
                     <span style=""display:inline-block;padding:6px 14px;border-radius:14px;
                                    text-transform:uppercase;font-weight:600;
                                    background:#2a9d8f11;color:#2a9d8f;font-size:0.9em;"">
                       {def.Tier} Tier
                     </span>
                   </div>
                
                  <p style=""font-weight:500;color:#555;margin-bottom:0.5em;"">
                    Here’s what you’ve unlocked:
                  </p>
                  <p style=""margin-top:0;color:#444;"">
                    {def.Description}
                  </p>
                
                  <blockquote style=""margin:2em 0;padding:1em 1.5em;border-left:5px solid #2a9d8f;background:#f0f4f8;color:#2a2a2a;font-style:italic;"">
                    “Every badge is a milestone on your journey—wear it proudly and let it inspire your next adventure!”  
                  </blockquote>
                
                  <p style=""font-size:0.95em;color:#666;"">
                    Head over to your your skillswap profile to admire your new accolade and see the other badges you’ve collected.
                  </p>
                
                  <p style=""margin-top:2em;color:#333;"">Keep up the great work,<br/><strong>The SkillSwap Team</strong></p>
                </div>";

            await _email.SendEmailAsync(toEmail, subject, html);
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