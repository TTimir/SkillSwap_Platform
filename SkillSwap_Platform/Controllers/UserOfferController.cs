using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserOfferController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<OfferController> _logger;

        public UserOfferController(SkillSwapDbContext context, ILogger<OfferController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /UserOffer/Details/{offerId}
        [HttpGet]
        public async Task<IActionResult> OfferDetails(int offerId)
        {
            int userId = GetUserId(); // get the user id from the claims or session

            var user = await _context.TblUsers
                .Include(u => u.TblReviewReviewees)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound("User Not Found");
            }

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
                        Location = u.Location,
                        City = u.City,
                        Country = u.Country,
                        ProfileImage = u.ProfileImageUrl // ✅ Fetch profile image if available
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

                double recommendedPercentage = user.RecommendedPercentage ?? 0;
                double jobSuccessRate = user.JobSuccessRate ?? 0;

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
                        _logger.LogInformation($"Generated ShortTitle: {shortTitle} for Offer ID: {o.OfferId}"); // Debug log

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
                            ProfileImage = o.User != null && !string.IsNullOrEmpty(o.User.ProfileImageUrl)
                                ? o.User.ProfileImageUrl
                                : "/template_assets/images/No_Profile_img.png"
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
                    CompareOffers = comparableOffers
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

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            else
                throw new Exception("Invalid user identifier claim.");
        }
    }
}
