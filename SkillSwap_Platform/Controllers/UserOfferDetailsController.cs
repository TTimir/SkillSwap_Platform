﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Models.ViewModels.OfferFilterVM;
using SkillSwap_Platform.Models.ViewModels.OfferPublicVM;
using SkillSwap_Platform.Services;
using SkillSwap_Platform.Services.AdminControls.OfferFlag;
using SkillSwap_Platform.Services.Matchmaking;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [AllowAnonymous]
    public class UserOfferDetailsController : Controller
    {
        private const string RecentCookie = "RecentlyViewedOffers";
        private readonly SkillSwapDbContext _context;
        private readonly IMatchmakingService _matchSvc;
        private readonly ILogger<UserOfferManageController> _logger;
        private readonly IOfferFlagService _svc;
        private readonly IAuthorizationService _authZ;

        public UserOfferDetailsController(SkillSwapDbContext context, ILogger<UserOfferManageController> logger, IOfferFlagService svc, IMatchmakingService offerMatchService, IAuthorizationService authZ)
        {
            _context = context;
            _matchSvc = offerMatchService;
            _logger = logger;
            _svc = svc;
            _authZ = authZ;
        }

        #region Offer Details
        // GET: /UserOffer/Details/{offerId}
        [HttpGet]
        public async Task<IActionResult> OfferDetails(int offerId, CancellationToken ct)
        {
            using var tx = _context.Database.BeginTransaction();
            try
            {
                // Fetch the offer including its portfolio items.
                var offer = await _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .Include(o => o.User)
                    .Include(o => o.TblReviews)
                        .ThenInclude(r => r.TblReviewReplies)
                    .FirstOrDefaultAsync(o => o.OfferId == offerId);
                if (offer == null)
                {
                    return RedirectToAction("EP404", "EP");
                }

                // 2. Retrieve reviews related to the offer.
                var reviews = await _context.TblReviews
                    .Where(r => r.OfferId == offerId)
                    .Include(r => r.Reviewer)
                    .Include(r => r.TblReviewReplies)
                        .ThenInclude(rep => rep.ReplierUser)
                    .OrderByDescending(r => r.CreatedDate)
                    .ToListAsync();

                // Get all reviews written for this user – assuming the TblReview table stores a UserId for the reviewed user.
                // (If you store user reviews in a separate table or using a different mechanism, adjust accordingly.)
                var userReviews = await _context.TblReviews
                    .Where(r => r.UserId == offer.UserId)
                    .ToListAsync();

                double userRating = userReviews.Count > 0 ? userReviews.Average(r => r.Rating) : 0;
                int reviewCount = userReviews.Count;
                double recommendedPercentage = 0;
                if (reviewCount > 0)
                {
                    int positiveReviews = userReviews.Count(r => r.Rating >= 4);
                    recommendedPercentage = (positiveReviews / (double)reviewCount) * 100;
                }

                // (You must also compute the user's exchange success rate – for example, based on completed exchanges
                // where the user was either the offer owner or the other party.)
                double userJobSuccessRate = 0;
                var userExchanges = await _context.TblExchanges
                    .Where(e => e.OfferOwnerId == offer.UserId || e.OtherUserId == offer.UserId)
                    .ToListAsync();
                if (userExchanges.Any())
                {
                    int completedExchanges = userExchanges.Count(e =>
                        e.Status != null && e.Status.Trim().ToLower() == "completed");
                    userJobSuccessRate = (completedExchanges / (double)userExchanges.Count) * 100;
                }

                // Determine the online status of the offer owner.
                bool isOnline = false;
                var offerOwner = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == offer.UserId);
                if (offerOwner != null && offerOwner.LastActive.HasValue)
                {
                    // Consider the user online if their last activity was within the last 5 minutes.
                    isOnline = (DateTime.UtcNow - offerOwner.LastActive.Value) < TimeSpan.FromMinutes(5);
                }

                List<string> portfolioUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(offer.Portfolio))
                {
                    try
                    {
                        portfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(offer.Portfolio)
                                        ?? new List<string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing portfolio JSON for offer {OfferId}", offer.OfferId);
                    }
                }

                // Fetch all languages associated with the offer owner (assuming TblLanguages has a UserId field).
                List<UserLanguageVM> userLanguages = await _context.TblLanguages
                    .Where(l => l.UserId == offer.UserId && l.Language == "English")
                    .Select(l => new UserLanguageVM
                    {
                        Language = l.Language, // The language name
                        Proficiency = l.Proficiency // The level column
                    })
                    .ToListAsync();

                List<UserDetailsVM> userDetails = await _context.TblUsers
                    .Where(u => u.UserId == offer.UserId)
                    .Select(u => new UserDetailsVM
                    {
                        UserId = u.UserId,
                        Username = u.UserName,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Location = u.CurrentLocation,
                        City = u.City,
                        Country = u.Country,
                        ProfileImage = u.ProfileImageUrl
                    })
                    .ToListAsync();


                List<string> skillNames = new List<string>();
                if (!string.IsNullOrWhiteSpace(offer.SkillIdOfferOwner))
                {
                    // Parse the comma-separated skill IDs.
                    var skillIds = offer.SkillIdOfferOwner
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(idStr => int.Parse(idStr.Trim()))
                        .ToList();

                    // For each skill ID, retrieve the skill using FindAsync.
                    foreach (var id in skillIds)
                    {
                        var skill = await _context.TblSkills.FindAsync(id);
                        if (skill != null)
                        {
                            skillNames.Add(skill.SkillName);
                        }
                    }
                }

                var WillingSkills = !string.IsNullOrWhiteSpace(offer.WillingSkill)
                    ? offer.WillingSkill.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim())
                                         .ToList()
                    : new List<string>();

                // Fetch comparable offers (excluding the current offer) in a separate try-catch block.
                List<CompareOfferVM> comparableOffers = new List<CompareOfferVM>();
                try
                {
                    // Fetch comparable offers from the database (raw data without processing)
                    var comparableOfferEntities = await _context.TblOffers
                        .Where(o => o.OfferId != offerId && o.Category == offer.Category)
                        .Include(o => o.User)
                        .OrderByDescending(o => o.JobSuccessRate)
                        .ThenByDescending(o => o.TokenCost)
                        .Take(3)
                        .ToListAsync();


                    // Process the fetched data in memory
                    comparableOffers = comparableOfferEntities.Select(o =>
                    {
                        // Calculate the user's review metrics for this specific offer owner.
                        var userReviewsForCompare = _context.TblReviews.Where(r => r.UserId == o.UserId).ToList();
                        int compareReviewCount = userReviewsForCompare.Count;
                        double compareAvgRating = compareReviewCount > 0 ? userReviewsForCompare.Average(r => r.Rating) : 0;
                        int positiveCompareReviews = compareReviewCount > 0 ? userReviewsForCompare.Count(r => r.Rating >= 4) : 0;
                        double compareRecommendedPercentage = compareReviewCount > 0 ? (positiveCompareReviews / (double)compareReviewCount) * 100 : 0;

                        // Calculate exchange success rate for the comparable user.
                        var userExchangesForCompare = _context.TblExchanges.Where(e => e.OfferOwnerId == o.UserId || e.OtherUserId == o.UserId).ToList();
                        double compareJobSuccessRate = 0;
                        if (userExchangesForCompare.Any())
                        {
                            int completedExchangesForCompare = userExchangesForCompare.Count(e =>
                                e.Status != null && e.Status.Trim().ToLower() == "completed");
                            compareJobSuccessRate = (completedExchangesForCompare / (double)userExchangesForCompare.Count) * 100;
                        }
                        var shortTitle = GenerateShortTitle(o.Title);  // Generate short title

                        return new CompareOfferVM
                        {
                            OfferId = o.OfferId,
                            UserId = o.UserId,
                            Title = o.Title,
                            ShortTitle = shortTitle,  // Now processed in memory (valid)
                            TokenCost = (int)o.TokenCost,
                            TimeCommitmentDays = o.TimeCommitmentDays,
                            Category = o.Category,
                            FreelanceType = o.FreelanceType,
                            RequiredSkillLevel = o.RequiredSkillLevel,
                            CollaborationMethod = o.CollaborationMethod ?? "Not Provided",
                            AssistanceRounds = o.AssistanceRounds ?? 0,
                            RecommendedPercentage = compareRecommendedPercentage,
                            JobSuccessRate = compareJobSuccessRate,
                            CompareWillingSkills = o.WillingSkill?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>(),
                            Username = o.User?.UserName,
                            ProfileImage = o.User.ProfileImageUrl ?? "/template_assets/images/No_Profile_img.png"
                        };
                    }).ToList();

                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex, "Error fetching comparable offers for Offer {OfferId}", offerId);
                    return RedirectToAction("EP500", "EP");
                }

                // ***************** NEW LOGIC FOR REVIEW ************************
                // Determine if the current logged-in user has completed an exchange for this offer.
                int? currentUserId = null;
                bool isExchangeCompleted = false;
                TblExchange completedExchange = null;

                if (User.Identity.IsAuthenticated)
                {
                    if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int uid))
                    {
                        currentUserId = uid;
                        // Rewrite the predicate so that it can be translated:
                        completedExchange = await _context.TblExchanges.FirstOrDefaultAsync(e =>
                            e.OfferId == offer.OfferId &&
                            (e.OfferOwnerId == uid || e.OtherUserId == uid) &&
                            e.Status != null && e.Status.ToLower() == "completed");
                        isExchangeCompleted = completedExchange != null;
                    }
                }
                // *****************************************************************

                int activeExchangeCount = 0;
                if (User.Identity.IsAuthenticated && offer != null)
                {
                    // Get the current logged in user's ID.
                    int uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                    // Use the offer's ID to limit the search only to exchanges for this offer.
                    // Then, decide which column to compare based on the user's role on this offer.
                    if (offer.UserId == uid)
                    {
                        // The current user is the offer owner, so count exchanges where they are the owner.
                        activeExchangeCount = _context.TblExchanges
                            .Where(e => e.OfferId == offer.OfferId
                                     && e.OfferOwnerId == uid
                                     && e.Status != null
                                     && e.Status.Trim().ToLower() != "completed")
                            .Count();
                    }
                    else
                    {
                        // The current user is not the owner, so count exchanges where they are the other party.
                        activeExchangeCount = _context.TblExchanges
                            .Where(e => e.OfferId == offer.OfferId
                                     && e.OtherUserId == uid
                                     && e.Status != null
                                     && e.Status.Trim().ToLower() != "completed")
                            .Count();
                    }
                }

                // Asynchronously update the view count without blocking the request.
                // Fire-and-forget:
                string sessionKey = $"OfferViewed_{offer.OfferId}";
                if (HttpContext.Session.GetString(sessionKey) == null)
                {
                    // This user hasn't viewed this offer in the current session; update the view count.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            offer.Views++;  // Increment the view count (make sure your model includes a Views property)
                            await _context.SaveChangesAsync();
                            await tx.CommitAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating view count for offer {OfferId}", offerId);
                            await tx.RollbackAsync(ct);
                        }
                    });
                    // Set the session flag so that subsequent refreshes in the same session won't increment the count.
                    HttpContext.Session.SetString(sessionKey, "true");
                }

                // Retrieve related offers and exclude current user's offers if logged in.
                IQueryable<TblOffer> relatedOffersQuery = _context.TblOffers
                    .Where(o => o.Category == offer.Category && o.OfferId != offer.OfferId);
                if (currentUserId.HasValue)
                {
                    relatedOffersQuery = relatedOffersQuery.Where(o => o.UserId != currentUserId.Value);
                }
                var relatedOffers = await relatedOffersQuery
                    .OrderByDescending(o => o.Views)
                    .Take(4)
                    .ToListAsync();

                bool isFlaggedByMe = false;
                if (User.Identity.IsAuthenticated
                    && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var me))
                {
                    isFlaggedByMe = await _svc.HasPendingFlagAsync(
                        offerId: offerId,
                        flaggedByUserId: currentUserId.Value
                    );
                }

                var model = new OfferDisplayVM
                {
                    OfferId = offer.OfferId,
                    Title = offer.Title,
                    ShortTitle = GenerateShortTitle(offer.Title),
                    ScopeOfWork = offer.ScopeOfWork,
                    TokenCost = (int)offer.TokenCost,
                    TimeCommitmentDays = offer.TimeCommitmentDays,
                    Category = offer.Category,
                    FreelanceType = offer.FreelanceType,
                    CollaborationMethod = offer.CollaborationMethod ?? "Not Provided",
                    RequiredSkillLevel = offer.RequiredSkillLevel,
                    PortfolioUrls = portfolioUrls,
                    CreatedDate = offer.CreatedDate,
                    WillingSkills = WillingSkills,
                    UserLanguages = userLanguages,
                    UserDetails = userDetails,
                    SkillNames = skillNames,
                    Device = offer.Device,
                    Tools = offer.Tools,
                    UserRating = userRating,
                    ReviewCount = reviewCount,
                    RecommendedPercentage = recommendedPercentage,  // e.g., "75.00"
                    JobSuccessRate = userJobSuccessRate, // e.g., 75.00 (you can format in the view)
                    CompareOffers = comparableOffers,
                    IsOnline = isOnline,
                    IsExchangeCompleted = isExchangeCompleted,
                    Review = new OfferExchangeReviewVM()
                    {
                        ExchangeId = completedExchange?.ExchangeId ?? 0,
                    },
                    Reviews = reviews,
                    ActiveExchangeCount = activeExchangeCount,
                    Views = offer.Views,
                    RelatedOffers = relatedOffers,
                    IsFlagged = isFlaggedByMe
                };

                // load FAQs for this offer
                var faqs = await _context.TblOfferFaqs
                    .Where(f => f.OfferId == offerId && !f.IsDeleted)
                    .OrderBy(f => f.CreatedDate)
                    .Select(f => new OfferFaqVM
                    {
                        FaqId = f.FaqId,
                        OfferId = f.OfferId,
                        Question = f.Question,
                        Answer = f.Answer
                    })
                    .ToListAsync();

                model.Faqs = faqs;

                // at the bottom of OfferDetails, before `return View(model);`
                const string sessionsKey = "RecentOfferSummaries";

                // pull whatever is already there (or start fresh)
                var recents = HttpContext.Session
                    .GetObjectFromJson<List<OfferCardVM>>(sessionsKey)
                    ?? new List<OfferCardVM>();

                // build a tiny summary of *this* offer
                var summary = new OfferCardVM
                {
                    OfferId = model.OfferId,
                    ShortTitle = model.ShortTitle,
                    UserName = model.UserDetails.FirstOrDefault()?.Username ?? model.UserDetails[0].Username,
                    UserProfileImage = model.UserDetails.FirstOrDefault()?.ProfileImage,
                    PortfolioImages = model.PortfolioUrls,
                    AverageRating = model.UserRating,
                    ReviewCount = model.ReviewCount,
                    TimeCommitmentDays = model.TimeCommitmentDays
                };

                // remove any old entry for the same offer, insert at front, cap at 8
                recents.RemoveAll(o => o.OfferId == summary.OfferId);
                recents.Insert(0, summary);
                if (recents.Count > 8) recents = recents.Take(8).ToList();

                if (recents.Count > 8)
                    recents = recents.Take(8).ToList();

                // ← **ADD THIS**: save it back into session
                HttpContext.Session.SetObjectAsJson(sessionsKey, recents);

                return View(model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error fetching details for offer {OfferId}", offerId);
                TempData["ErrorMessage"] = "An error occurred while loading offer details.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Public Marketplace/Offer List
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PublicOfferList(
            string category, int? skillId, string keyword, string sortOption,
            int? maxTimeCommitment, string skillLevel, string designTool,
            string freelanceType, string interactionMode, int page = 1, int pageSize = 20)
        {
            using var tx = _context.Database.BeginTransaction();
            try
            {
                var currentUserId = User.Identity.IsAuthenticated
                    ? _context.TblUsers.FirstOrDefault(u => u.UserName == User.Identity.Name)?.UserId
                    : null;

                var offersQuery = _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .Include(o => o.User)
                    .Where(o => o.IsActive && !o.IsDeleted);

                if (currentUserId.HasValue)
                    offersQuery = offersQuery.Where(o => o.UserId != currentUserId.Value);

                // Apply filters
                if (!string.IsNullOrEmpty(category))
                {
                    var decoded = category.Replace("-", " ").Trim().ToLower();
                    offersQuery = offersQuery.Where(o =>
                        o.Category.Trim().ToLower() == decoded);
                }

                if (skillId.HasValue)
                {
                    var token = $",{skillId.Value},";
                    offersQuery = offersQuery.Where(o =>
                        ("," + o.SkillIdOfferOwner + ",").Contains(token));
                }

                if (maxTimeCommitment.HasValue)
                    offersQuery = offersQuery.Where(o => o.TimeCommitmentDays <= maxTimeCommitment);

                if (!string.IsNullOrEmpty(skillLevel))
                    offersQuery = offersQuery.Where(o => o.RequiredSkillLevel == skillLevel);

                if (!string.IsNullOrEmpty(freelanceType))
                    offersQuery = offersQuery.Where(o => o.FreelanceType == freelanceType);

                if (!string.IsNullOrEmpty(interactionMode))
                    offersQuery = offersQuery.Where(o => o.CollaborationMethod == interactionMode);

                if (!string.IsNullOrEmpty(designTool))
                    offersQuery = offersQuery.Where(o =>
                        !string.IsNullOrEmpty(o.Tools) &&
                        o.Tools.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(t => t.Trim())
                               .Contains(designTool));

                var skillNameIdPairs = await _context.TblSkills
                    .Select(s => new { s.SkillId, s.SkillName })
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    // Remove spaces and convert the search term to lower-case.
                    var terms = keyword.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToLowerInvariant())
                        .ToList();

                    var matchingSkillIds = skillNameIdPairs
                        .Where(x => terms.Contains(x.SkillName.ToLowerInvariant()))
                        .Select(x => x.SkillId)
                        .ToList();

                    // 3) If any, filter by the *first* matching ID via LIKE
                    if (matchingSkillIds.Any())
                    {
                        var firstId = matchingSkillIds[0];
                        // pattern "%,42,%" to match ",42," anywhere in your CSV
                        var pattern = "%," + firstId + ",%";

                        offersQuery = offersQuery.Where(o =>
                            EF.Functions.Like(
                                "," + o.SkillIdOfferOwner + ",",
                                pattern
                            )
                        );
                    }

                    foreach (var t in terms)
                    {
                        offersQuery = offersQuery.Where(o =>
                            (o.Title != null && o.Title.ToLower().Contains(t))
                            || (o.ScopeOfWork != null && o.ScopeOfWork.ToLower().Contains(t))
                            || (o.Category != null && o.Category.ToLower().Contains(t))
                            || (o.TokenCost.ToString().Contains(t))
                            || (o.TimeCommitmentDays.ToString().Contains(t))
                            || (o.WillingSkill != null && o.WillingSkill.ToLower().Contains(t))
                            || (o.SkillIdOfferOwner != null && o.SkillIdOfferOwner.ToLower().Contains(t))
                            || (o.CollaborationMethod != null && o.CollaborationMethod.ToLower().Contains(t))
                            || (o.JobSuccessRate.ToString().Contains(t))
                            || (o.RecommendedPercentage.ToString().Contains(t))
                            || (o.Tools != null && o.Tools.ToLower().Contains(t))
                            || (o.Device != null && o.Device.ToLower().Contains(t))
                            || (o.FreelanceType != null && o.FreelanceType.ToLower().Contains(t))
                            || (o.RequiredSkillLevel != null && o.RequiredSkillLevel.ToLower().Contains(t))
                            // Search in related user fields
                            || (o.User != null && (
                                   (o.User.UserName != null && o.User.UserName.ToLower().Contains(t))
                                || (o.User.FirstName != null && o.User.FirstName.ToLower().Contains(t))
                                || (o.User.LastName != null && o.User.LastName.ToLower().Contains(t))
                                || (o.User.Designation != null && o.User.Designation.ToLower().Contains(t))
                                || (o.User.City != null && o.User.City.ToLower().Contains(t))
                                || (o.User.Country != null && o.User.Country.ToLower().Contains(t))
                                || (o.User.CurrentLocation != null && o.User.CurrentLocation.ToLower().Contains(t))
                                || (o.User.Languages != null && o.User.Languages.ToLower().Contains(t))
                            ))
                        );
                    }
                }

                // Design Tool Options
                var allTools = await _context.TblOffers
                    .Where(o => !string.IsNullOrEmpty(o.Tools))
                    .Select(o => o.Tools)
                    .ToListAsync();

                var toolSet = new HashSet<string>();
                foreach (var tools in allTools)
                {
                    foreach (var tool in tools.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        toolSet.Add(tool.Trim());
                    }
                }

                // Build time commitment ranges dynamically.
                var timeCommitmentRanges = new List<SelectListItem>();
                int maxDays = await _context.TblOffers.MaxAsync(o => o.TimeCommitmentDays);
                int step = 10;
                for (int i = 0; i < maxDays; i += step)
                {
                    int upper = i + step;
                    timeCommitmentRanges.Add(new SelectListItem
                    {
                        Text = $"{i + 1} - {upper} Days",
                        Value = upper.ToString(),
                        Selected = maxTimeCommitment.HasValue && maxTimeCommitment.Value == upper
                    });
                }

                // Dynamic Skill Level Options
                var skillLevelOptions = await _context.TblOffers
                    .Select(o => o.RequiredSkillLevel)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .Select(s => new SelectListItem
                    {
                        Text = s,
                        Value = s,
                        Selected = s == skillLevel
                    })
                    .ToListAsync();

                if (string.IsNullOrEmpty(sortOption))
                {
                    sortOption = "newArrivals";
                }

                // Apply sorting based on sortOption value
                switch (sortOption)
                {
                    case "bestSeller":
                        offersQuery = offersQuery.OrderByDescending(o => o.JobSuccessRate)
                                                 .ThenByDescending(o => o.TokenCost);
                        break;
                    case "recommended":
                        offersQuery = offersQuery.OrderByDescending(o => o.TokenCost);
                        break;
                    case "newArrivals":
                    default:
                        offersQuery = offersQuery.OrderByDescending(o => o.CreatedDate);
                        break;
                }

                int totalOffers = await offersQuery.CountAsync();
                var offers = await offersQuery
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize)
                    .ToListAsync();

                var reviewAggregates = await _context.TblReviews
                    .GroupBy(r => r.OfferId)
                    .Select(g => new
                    {
                        OfferId = g.Key,
                        ReviewCount = g.Count(),
                        AverageRating = g.Average(r => r.Rating)
                    })
                    .ToDictionaryAsync(x => x.OfferId);

                var offerCards = offers.Select(o =>
                {
                    var portfolio = string.IsNullOrWhiteSpace(o.Portfolio)
                        ? new List<string>()
                        : JsonConvert.DeserializeObject<List<string>>(o.Portfolio) ?? new List<string>();

                    // Default values if no reviews are available.
                    int reviewCount = 0;
                    double avgRating = 0;

                    // Look for aggregated review data.
                    if (reviewAggregates.TryGetValue(o.OfferId, out var agg))
                    {
                        reviewCount = agg.ReviewCount;
                        avgRating = agg.AverageRating;
                    }

                    return new OfferCardVM
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        ShortTitle = o.Title?.Length > 35 ? o.Title.Substring(0, 35) + "..." : o.Title,
                        Category = o.Category,
                        TokenCost = (int)o.TokenCost,
                        TimeCommitmentDays = o.TimeCommitmentDays,
                        ScopeOfWork = o.ScopeOfWork,
                        CreatedDate = o.CreatedDate,
                        UserName = o.User?.UserName ?? "Unknown",
                        Location = o.User?.City,
                        Country = o.User?.Country,
                        UserProfileImage = string.IsNullOrEmpty(o.User?.ProfileImageUrl)
                                            ? "/template_assets/images/No_Profile_img.png"
                                            : o.User.ProfileImageUrl,
                        Thumbnail = portfolio.FirstOrDefault(),
                        PortfolioImages = portfolio,
                        AverageRating = avgRating,
                        ReviewCount = reviewCount
                    };
                }).ToList();

                IReadOnlyList<OfferCardVM> suggestedCards = Array.Empty<OfferCardVM>();
                if (User.Identity.IsAuthenticated)
                {
                    // Only fetch suggestions for Pro or Growth (because Growth ≥ Pro)
                    var authResult = await _authZ.AuthorizeAsync(User, "ProPlan");
                    if (authResult.Succeeded)
                    {
                        var uid = _context.TblUsers
                        .Where(u => u.UserName == User.Identity.Name)
                        .Select(u => (int?)u.UserId)
                        .FirstOrDefault();

                        if (uid.HasValue)
                            suggestedCards = await _matchSvc
                                .GetSuggestedOffersForUserAsync(uid.Value);
                    }
                }

                var textInfo = CultureInfo.CurrentCulture.TextInfo;
                // Load filter options
                var vm = new OfferFilterVM
                {
                    Offers = offerCards,
                    SuggestedOffers = suggestedCards,
                    CategoryOptions = await _context.TblOffers
                        .Where(o => !string.IsNullOrWhiteSpace(o.Category))
                        .Select(o => o.Category)
                        .Distinct()
                    .OrderBy(c => c)
                    .Select(c => new SelectListItem { Text = textInfo.ToTitleCase(c), Value = c })
                        .ToListAsync(),

                    SkillOptions = await _context.TblSkills
                        .Select(s => new SelectListItem { Text = textInfo.ToTitleCase(s.SkillName), Value = s.SkillId.ToString() })
                        .ToListAsync(),

                    // Populate language options from your global language table
                    LanguageOptions = await _context.TblLanguages
                        .Where(l => !string.IsNullOrWhiteSpace(l.Language))
                        .Select(l => l.Language)  // Assuming 'Language' is the property
                        .Distinct()
                        .OrderBy(lang => lang)
                        .Select(lang => new SelectListItem { Text = textInfo.ToTitleCase(lang), Value = lang })
                        .ToListAsync(),

                    // Populate location options (for example, from users' countries)
                    LocationOptions = await _context.TblUsers
                        .Where(u => !string.IsNullOrWhiteSpace(u.Country))
                        .Select(u => u.Country)
                        .Distinct()
                        .OrderBy(c => c)
                        .Select(c => new SelectListItem { Text = textInfo.ToTitleCase(c), Value = c })
                        .ToListAsync(),

                    FreelanceTypeOptions = await _context.TblOffers
                        .Where(o => !string.IsNullOrEmpty(o.FreelanceType))
                        .Select(o => o.FreelanceType)
                        .Distinct()
                        .OrderBy(f => f)
                        .Select(f => new SelectListItem
                        {
                            Text = f,
                            Value = f,
                            Selected = f == freelanceType
                        })
                        .ToListAsync(),

                    DesignToolOptions = toolSet.Select(t => new SelectListItem { Text = t, Value = t }).ToList(),
                    SkillLevelOptions = skillLevelOptions,
                    TimeCommitmentOptions = timeCommitmentRanges,

                    DesignTool = designTool,
                    SkillLevel = skillLevel,
                    MaxTimeCommitment = maxTimeCommitment,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling((double)totalOffers / pageSize)
                };

                await tx.CommitAsync();
                return View("PublicOfferList", vm);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error loading public offer list");
                TempData["ErrorMessage"] = "An error occurred while loading offers.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        [Authorize(Policy = "PlusPlan")]
        [HttpGet]
        public async Task<IActionResult> NearbyOffers(double lat, double lng, int max = 8)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 1) pull only active, non‐deleted offers that aren't yours
            //    and that have valid offer‐level coords
            var candidates = await _context.TblOffers
                .Include(o => o.User)
                .Include(o => o.TblOfferPortfolios)
                .Where(o => o.IsActive
                         && !o.IsDeleted
                         && o.UserId != userId
                         && o.Latitude.HasValue
                         && o.Longitude.HasValue)
                .ToListAsync();

            // 2) compute Haversine in‐memory
            static double ToRad(double v) => v * Math.PI / 180;
            static double Distance(double la1, double lo1, double la2, double lo2)
            {
                var R = 6371.0;
                var dLat = ToRad(la2 - la1);
                var dLon = ToRad(lo2 - lo1);
                la1 = ToRad(la1); la2 = ToRad(la2);
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                      + Math.Cos(la1) * Math.Cos(la2)
                      * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            }

            var nearest = candidates
                .Select(o => new
                {
                    Offer = o,
                    Dist = Distance(lat, lng, o.Latitude.Value, o.Longitude.Value)
                })
                .OrderBy(x => x.Dist)
                .Take(max)
                .Select(x => x.Offer)
                .ToList();

            // 3) pull one GROUP BY over the entire review set
            var aggDict = (await _context.TblReviews
                .GroupBy(r => r.OfferId)
                .Select(g => new
                {
                    OfferId = g.Key,
                    ReviewCount = g.Count(),
                    AvgRating = g.Average(r => (double)r.Rating)
                })
                .ToListAsync())
                .ToDictionary(x => x.OfferId);

            // 4) project to your OfferCardVM
            var cards = nearest.Select(o =>
            {
                var imgs = o.TblOfferPortfolios?.Any() == true
                                   ? o.TblOfferPortfolios.Select(p => p.FileUrl).ToList()
                                   : (!string.IsNullOrWhiteSpace(o.Portfolio)
                                        ? JsonConvert.DeserializeObject<List<string>>(o.Portfolio) ?? new List<string>()
                                        : new List<string>());

                aggDict.TryGetValue(o.OfferId, out var a);

                return new OfferCardVM
                {
                    OfferId = o.OfferId,
                    Title = o.Title,
                    ShortTitle = o.Title.Length > 35 ? o.Title.Substring(0, 35) + "…" : o.Title,
                    Category = o.Category,
                    TokenCost = (int)o.TokenCost,
                    TimeCommitmentDays = o.TimeCommitmentDays,
                    PortfolioImages = imgs,
                    AverageRating = a?.AvgRating ?? 0,
                    ReviewCount = a?.ReviewCount ?? 0,
                    UserName = o.User.UserName,
                    UserProfileImage = o.User.ProfileImageUrl
                };
            }).ToList();

            return PartialView("_NearbyOfferCards", cards);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Flag(int offerId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("Reason is required.");


            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userIdInt))
                return StatusCode(500, "Unable to determine your user ID.");


            bool alreadyFlagged = await _context.TblOfferFlags
                .AnyAsync(f => f.OfferId == offerId && f.FlaggedByUserId == userIdInt);

            if (alreadyFlagged)
            {
                TempData["ErrorMessage"] = "You’ve already reported this swap offer.";
                return RedirectToAction("OfferDetails", new { offerId = offerId });
            }

            try
            {
                await _svc.FlagOfferAsync(offerId, userIdInt, reason);
                TempData["SuccessMessage"] = "Thank you, we’ve received your report and will review it to find the best solution.";
                return RedirectToAction("OfferDetails", new { offerId = offerId });
            }
            catch
            {
                return StatusCode(500, "Something went wrong while submitting your report. Please try again later.");
            }
        }

        #region Helper Class
        private string GenerateShortTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return title;

            // Common stop words (non-essential words)
            HashSet<string> stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "i", "can", "you", "a", "an", "the", "and", "or", "of", "to", "for", "your",
                "with", "my", "in", "on", "at", "from", "by", "is", "this", "that", "will",
                "it", "we", "us", "our", "me", "mine", "yourself", "their", "ourself", "complete"
            };

            // Split words and remove stop words
            List<string> words = title.Split(' ')
                                      .Where(word => !stopWords.Contains(word.ToLower()))
                                      .ToList();

            // If no meaningful words remain, return original title
            if (!words.Any())
                return title;

            // If title is already short (≤3 words), return it as is
            if (words.Count <= 3)
                return string.Join(" ", words);

            // **Step 1: Prioritize Important Words**
            List<string> importantWords = words.Where(w => w.Length > 3).ToList(); // Filter longer words

            // Ensure at least 3 words remain in the final title
            if (importantWords.Count >= 3)
                return string.Join(" ", importantWords.Take(3));  // Take first 3 meaningful words

            // If not enough important words, take from original list to make it 3
            while (importantWords.Count < 3 && words.Count > importantWords.Count)
            {
                importantWords.Add(words[importantWords.Count]);
            }

            return string.Join(" ", importantWords);
        }

        private async Task UpdateUserAggregatesAsync(int userId)
        {
            // Get all reviews written for this user.
            var userReviews = await _context.TblReviews
                                .Where(r => r.UserId == userId)
                                .ToListAsync();

            int count = userReviews.Count;
            decimal avgRating = count > 0 ? (decimal)userReviews.Average(r => r.Rating) : 0;

            // For example, consider ratings of 4 or higher as positive.
            int positiveCount = count > 0 ? userReviews.Count(r => r.Rating >= 4) : 0;
            decimal recommendedPercentage = count > 0 ? (positiveCount / (decimal)count) * 100 : 0;

            // Get exchanges in which this user participated (as offer owner or other party).
            var userExchanges = await _context.TblExchanges
                                 .Where(e => e.OfferOwnerId == userId || e.OtherUserId == userId)
                                 .ToListAsync();
            int totalExchanges = userExchanges.Count;
            int completedExchanges = totalExchanges > 0
                                      ? userExchanges.Count(e => e.Status != null && e.Status.Trim().ToLower() == "completed")
                                      : 0;
            decimal jobSuccessRate = totalExchanges > 0 ? (completedExchanges / (decimal)totalExchanges) * 100 : 0;

            // Retrieve and update the user.
            var user = await _context.TblUsers.FindAsync(userId);
            if (user != null)
            {
                user.ReviewCount = count;
                user.AverageRating = avgRating;
                user.RecommendedPercentage = (double?)recommendedPercentage;
                user.JobSuccessRate = (double?)jobSuccessRate;

                await _context.SaveChangesAsync();
            }
        }

        #endregion
    }
}
