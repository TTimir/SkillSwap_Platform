using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.OfferFilterVM;
using SkillSwap_Platform.Services;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [AllowAnonymous]
    public class UserOfferDetailsController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserOfferManageController> _logger;

        public UserOfferDetailsController(SkillSwapDbContext context, ILogger<UserOfferManageController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Offer Details
        // GET: /UserOffer/Details/{offerId}
        [HttpGet]
        public async Task<IActionResult> OfferDetails(int offerId)
        {
            // Use default values for recommendedPercentage and jobSuccessRate.
            double recommendedPercentage = 0;
            double jobSuccessRate = 0;

            try
            {
                // Fetch the offer including its portfolio items.
                var offer = await _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .FirstOrDefaultAsync(o => o.OfferId == offerId);

                if (offer == null)
                {
                    return NotFound("Offer not found.");
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
                        .OrderByDescending(o => o.JobSuccessRate)
                        .ThenByDescending(o => o.TokenCost)
                        .Take(3)
                        .ToListAsync();


                    // Process the fetched data in memory
                    comparableOffers = comparableOfferEntities.Select(o =>
                    {
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
                            RecommendedPercentage = recommendedPercentage.ToString("N2"),
                            JobSuccessRate = jobSuccessRate,
                            CompareWillingSkills = o.WillingSkill?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>(),
                            Username = o.User?.UserName,
                            ProfileImage = portfolioUrls.FirstOrDefault()
                        };
                    }).ToList();

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching comparable offers for Offer {OfferId}", offerId);
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
                    RecommendedPercentage = recommendedPercentage.ToString("N2"),
                    JobSuccessRate = jobSuccessRate,
                    CompareOffers = comparableOffers,
                    IsOnline = isOnline
                };

                return View(model);
            }
            catch (Exception ex)
            {
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
            try
            {
                var offersQuery = _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .Include(o => o.User)
                    .Where(o => o.IsActive && !o.IsDeleted);

                // Apply filters
                if (!string.IsNullOrEmpty(category))
                    offersQuery = offersQuery.Where(o => o.Category == category);

                if (skillId.HasValue)
                    offersQuery = offersQuery.Where(o => o.SkillIdOfferOwner.Contains(skillId.ToString())); // String match on skill list

                if (maxTimeCommitment.HasValue)
                    offersQuery = offersQuery.Where(o => o.TimeCommitmentDays <= maxTimeCommitment);

                if (!string.IsNullOrEmpty(skillLevel))
                    offersQuery = offersQuery.Where(o => o.RequiredSkillLevel == skillLevel);

                if (!string.IsNullOrEmpty(designTool))
                    offersQuery = offersQuery.Where(o => o.Tools.Contains(designTool));

                if (!string.IsNullOrEmpty(freelanceType))
                    offersQuery = offersQuery.Where(o => o.FreelanceType == freelanceType);
               
                if (!string.IsNullOrEmpty(interactionMode))
                    offersQuery = offersQuery.Where(o => o.CollaborationMethod == interactionMode);

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    // Remove spaces and convert the search term to lower-case.
                    string normalizedKeyword = keyword.Replace(" ", "").ToLower();

                    offersQuery = offersQuery.Where(o =>
                        // Search in offer fields:
                        (o.Title != null && o.Title.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.ScopeOfWork != null && o.ScopeOfWork.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.Category != null && o.Category.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.TokenCost.ToString().Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.TimeCommitmentDays.ToString().Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.WillingSkill != null && o.WillingSkill.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.SkillIdOfferOwner != null && o.SkillIdOfferOwner.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.CollaborationMethod != null && o.CollaborationMethod.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.JobSuccessRate.ToString().Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.RecommendedPercentage.ToString().Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.Tools != null && o.Tools.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.Device != null && o.Device.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.FreelanceType != null && o.FreelanceType.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                        (o.RequiredSkillLevel != null && o.RequiredSkillLevel.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||

                        // Search in related user fields:
                        (o.User != null && (
                            (o.User.UserName != null && o.User.UserName.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.FirstName != null && o.User.FirstName.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.LastName != null && o.User.LastName.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.Designation != null && o.User.Designation.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.City != null && o.User.City.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.Country != null && o.User.Country.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.CurrentLocation != null && o.User.CurrentLocation.Replace(" ", "").ToLower().Contains(normalizedKeyword)) ||
                            (o.User.Languages != null && o.User.Languages.Replace(" ", "").ToLower().Contains(normalizedKeyword))
                        ))
                    );
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
                    _logger.LogDebug("No sortOption provided, defaulting to: {SortOption}", sortOption);
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
                    .OrderByDescending(o => o.CreatedDate)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize)
                    .ToListAsync();

                var offerCards = offers.Select(o =>
                {
                    var portfolio = string.IsNullOrWhiteSpace(o.Portfolio)
                        ? new List<string>()
                        : Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(o.Portfolio) ?? new List<string>();

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
                        PortfolioImages = portfolio 
                    };
                }).ToList();

                var textInfo = CultureInfo.CurrentCulture.TextInfo;
                // Load filter options
                var vm = new OfferFilterVM
                {
                    Offers = offerCards,
                    CategoryOptions = await _context.TblOffers
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
                        .Select(l => l.Language)  // Assuming 'Language' is the property
                        .Distinct()
                        .OrderBy(lang => lang)
                        .Select(lang => new SelectListItem { Text = textInfo.ToTitleCase(lang), Value = lang })
                        .ToListAsync(),

                    // Populate location options (for example, from users' countries)
                    LocationOptions = await _context.TblUsers
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

                return View("PublicOfferList", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public offer list");
                TempData["ErrorMessage"] = "An error occurred while loading offers.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

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

        #endregion
    }
}
