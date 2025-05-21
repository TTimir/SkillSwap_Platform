using System.Net;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using SkillSwap_Platform.HelperClass;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using SkillSwap_Platform.Models.ViewModels.OfferFilterVM;
using Newtonsoft.Json;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.PasswordReset;
using SkillSwap_Platform.HelperClass.Extensions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using SkillSwap_Platform.Services.Newsletter;
using System.Net;
using System.Globalization;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SkillSwap_Platform.Models.ViewModels.OfferPublicVM;
using SkillSwap_Platform.Controllers.AdminDashboard;
using SkillSwap_Platform.Services.AdminControls.UserManagement;
using Microsoft.Extensions.Primitives;
using SkillSwap_Platform.Services.Blogs;

namespace SkillSwap_Platform.Controllers;

public class HomeController : Controller
{
    private const string _recentCookie = "RecentlyViewedOffers";
    private readonly IUserServices _userService;
    private readonly IPasswordResetService _passwordReset;
    private readonly SkillSwapDbContext _dbcontext;
    private readonly IEmailService _emailService;
    private readonly INewsletterService _newsletter;
    private readonly IConfiguration _config;
    private readonly int _satisfactionThreshold;
    private readonly ILogger<HomeController> _logger;
    private readonly IUserManagmentService _usv;
    private readonly IBlogService _blogService;

    public HomeController(IUserServices userService, SkillSwapDbContext context, IEmailService emailService, IPasswordResetService passwordReset, INewsletterService newsletter, IConfiguration config, ILogger<HomeController> logger, IUserManagmentService usv, IBlogService blogService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _dbcontext = context ?? throw new ArgumentNullException(nameof(context));
        _passwordReset = passwordReset ?? throw new ArgumentNullException(nameof(passwordReset)); ;
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService)); ;
        _newsletter = newsletter;
        _config = config;
        _satisfactionThreshold = config.GetValue<int>("TrustMetrics:SatisfactionThreshold", 4);
        _logger = logger;
        _usv = usv;
        _blogService = blogService;
    }

    #region Layout Helpers

    public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        await EnforceNotHeldAsync(ctx);
        PopulateFooterCategories();
        await next();
    }

    private async Task EnforceNotHeldAsync(ActionExecutingContext ctx)
    {
        if (!User.Identity.IsAuthenticated) return;
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return;

        var isHeld = await _dbcontext.TblUsers
                           .Where(u => u.UserId == uid)
                           .Select(u => u.IsHeld)
                           .FirstOrDefaultAsync();

        if (isHeld)
        {
            await HttpContext.SignOutAsync("SkillSwapAuth");
            TempData["ErrorMessage"] = "Your account has been held. Please contact support.";
            ctx.Result = RedirectToAction(nameof(Login), "Home");
        }
    }

    private void PopulateFooterCategories()
    {
        var cats = _dbcontext.TblOffers
            .AsNoTracking()
            .Select(o => o.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        ViewData["FooterCategories"] = cats;
    }

    #endregion

    #region Subscribe Newsletter
    // GET /Footer/UnsubscribeNewsletter?email=foo@bar.com
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> UnsubscribeNewsletter(string email)
    {
        if (string.IsNullOrWhiteSpace(email)
         || !new EmailAddressAttribute().IsValid(email))
        {
            ViewBag.Message = "🔴 That address doesn’t look valid.";
            return View("UnsubscribeNewsletter");
        }

        try
        {
            bool removed = await _newsletter.UnsubscribeAsync(email);
            if (removed)
                ViewBag.Message = $"✅ {email} has been successfully unsubscribed from our mailing list.";
            else
                ViewBag.Message = $"⚠️ {email} was not found in our mailing list.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe {Email}", email);
            return RedirectToAction("EP500", "EP");
        }
        return View("UnsubscribeNewsletter");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubscribeNewsletter([EmailAddress] string email)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid newsletter signup attempt: '{Email}'", email);
            TempData["NewsletterError"] = "🔴 Please enter a valid email.";
            return RedirectToReferrer();
        }

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            TempData["NewsletterError"] = "🔴 Please enter a valid email.";
        }
        else
        {
            await _newsletter.SubscribeAsync(email);

            var unsubscribeUrl = Url.Action(
                "UnsubscribeNewsletter",
                "Home",
                new { email = WebUtility.UrlEncode(email) },
                protocol: Request.Scheme);

            var subject = "Thanks for subscribing to SkillSwap!";
            var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
    <tr>
      <td align=""center"" style=""padding:20px;"">
        <table width=""600"" style=""background:#fff;border-collapse:collapse;"">

          <!-- Header -->
          <tr>
            <td style=""background:#00A88F;padding:20px;"">
              <h1 style=""margin:0;color:#fff;font-size:24px;"">SkillSwap</h1>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""padding:20px;color:#333;line-height:1.5;"">
              <h2 style=""margin:0 0 15px;font-size:20px;color:#333;"">Welcome to the SkillSwap Community!</h2>
              <p>Hi there,</p>
              <p>🎉 Thank you for subscribing to the SkillSwap newsletter! You’ll now be among the first to hear about new features, tips, and exclusive offers.</p>
              <hr style=""border:none;border-top:1px solid #e0e0e0;margin:20px 0;"" />
              <p>If you ever wish to unsubscribe, just click the link below:</p>
              <p style=""text-align:center;margin:20px 0;"">
                <a href=""{unsubscribeUrl}"" style=""color:#00A88F;text-decoration:underline;"">Unsubscribe from Newsletter</a>
              </p>
              <p>Welcome aboard!<br/>— The SkillSwap Team</p>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background:#00A88F;padding:10px;text-align:center;color:#e0f7f1;font-size:11px;"">
              © {DateTime.UtcNow.ToLocalTime().ToString("yyyy")} SkillSwap Inc. | 
              <a href=""mailto:skillswap360@gmail.com"" style=""color:#fff;text-decoration:underline;"">Support</a>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>
";

            await _emailService.SendEmailAsync(
                to: email,
                subject: subject,
                body: htmlBody,
                isBodyHtml: true
            );
            TempData["NewsletterSuccess"] = "✅ Thanks for subscribing!";
        }
        // refresh the same page
        return Redirect(Request.Headers["Referer"].ToString());
    }
    #endregion

    #region Menu Pages
    public async Task<IActionResult> Index()
    {
        try
        {
            int? currentUserId = User.Identity.IsAuthenticated
                ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
                : null;

            double globalAvgRating = 0;
            if (await _dbcontext.TblReviews.AnyAsync())
            {
                globalAvgRating = await _dbcontext.TblReviews
                    .AverageAsync(r => (double)r.Rating);
            }

            var totalReviews = await _dbcontext.TblReviews.CountAsync();

            // 2) Reviews at or above threshold
            var happyReviews = await _dbcontext.TblReviews
                .Where(r => r.Rating >= _satisfactionThreshold)
                .CountAsync();

            int satisfactionPct = totalReviews == 0
                ? 100 // or 0, however you prefer to handle no data
                : (int)Math.Round(happyReviews * 100m / totalReviews);

            int actualUsers = await _dbcontext.TblUsers.CountAsync(u => u.IsActive);
            decimal bonusPct = _config.GetValue<decimal>("TrustMetrics:FreelancerBonusPercent") / 100m;
            int displayCount = actualUsers + (int)Math.Ceiling(actualUsers * bonusPct);

            int actualTalents = await _dbcontext.TblUsers
            .Where(u => u.IsActive)
            .CountAsync();

            // 2) read bonus percentage from appsettings.json
            decimal bonusPercent = _config
                .GetValue<decimal>("TrustMetrics:TalentsBonusPercent")
                / 100m;

            // 3) compute displayed number
            int displayTalents = actualTalents
                + (int)Math.Ceiling(actualTalents * bonusPercent);

            (string fDisp, string fSfx) = FormatNumber(displayTalents);

            int actualExchanges = await _dbcontext.TblExchanges
                .Where(x => x.Status == "Completed")
                .CountAsync();

            int displayExchanges = actualExchanges
                + (int)Math.Ceiling(actualExchanges * bonusPercent);

            (string eDisp, string eSfx) = FormatNumber(displayExchanges);

            // 1) Group your offers by Category, count them:
            var cats = await _dbcontext.TblOffers
                .GroupBy(o => o.Category)
                .Select(g => new CategoryCardVm
                {
                    Name = g.Key,
                    Slug = g.Key.Replace(" ", "-").ToLowerInvariant(),
                    IconClass = PickIconClass(g.Key),       // helper to choose the right <span> icon
                    SkillCount = g.Count(),
                    Description = $"{g.Count()} skills available"
                })
                .ToListAsync();

            // count total skills
            int totalSkills = await _dbcontext.TblSkills
                .Select(s => s.SkillName!.Trim().ToLower())  // normalize if needed
                .Distinct()
                .CountAsync();

            var trendingOffersQuery = _dbcontext.TblOffers
                .Include(o => o.User)
                .Where(o => o.IsActive);

            if (currentUserId.HasValue)
            {
                trendingOffersQuery = trendingOffersQuery.Where(o => o.UserId != currentUserId.Value);
            }

            // Retrieve trending offers with eager loading for related User
            var trendingOffers = await trendingOffersQuery
                .Include(o => o.User)  // Eager load the related User
                .Where(o => o.IsActive)
                .OrderByDescending(o => o.JobSuccessRate)
                .ThenByDescending(o => o.TokenCost)
                .Take(8)
                .ToListAsync();

            // fetch all reviews for those offers in one DB hit
            var tOfferIds = trendingOffers.Select(o => o.OfferId).ToList();
            
            List<ReviewAggregate> aggregates;

            if (tOfferIds.Any())
            {
                var idParams = string.Join(", ", tOfferIds);
                var sql = $@"
                  SELECT OfferId,
                         COUNT(*)           AS Count,
                         AVG(CAST(Rating AS float)) AS Avg
                    FROM TblReviews
                   WHERE OfferId IN ({idParams})
                   GROUP BY OfferId;
                ";
                aggregates = await _dbcontext
                    .Set<ReviewAggregate>()
                    .FromSqlRaw(sql)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else
            {
                aggregates = new List<ReviewAggregate>();
            }

            var tAggregates = aggregates.ToDictionary(x => x.OfferId);

            var trendingOfferVMs = trendingOffers.Select(o =>
            {
                tAggregates.TryGetValue(o.OfferId, out var agg);
                return new OfferCardVM
                {
                    OfferId = o.OfferId,
                    Title = o.Title,
                    ShortTitle = o.Title?.Length > 35 ? o.Title[..35] + "..." : o.Title,
                    UserProfileImage = string.IsNullOrEmpty(o.User?.ProfileImageUrl)
                                         ? "/template_assets/images/No_Profile_img.png"
                                         : o.User.ProfileImageUrl,
                    PortfolioImages = !string.IsNullOrWhiteSpace(o.Portfolio)
                                        ? JsonConvert.DeserializeObject<List<string>>(o.Portfolio)
                                        : new List<string>(),
                    Category = o.Category,
                    UserName = o.User?.UserName ?? "Unknown",
                    TimeCommitmentDays = o.TimeCommitmentDays,

                    // ⭐ NEW
                    AverageRating = agg?.Avg ?? 0,
                    ReviewCount = agg?.Count ?? 0
                };
            }).ToList();

            // Query highest rated freelancers (limit to 10)
            // Adjust the filtering criteria as needed (e.g. only active, and only freelancers)
            var highestRatedUsers = await _dbcontext.TblUsers
                .Include(u => u.TblReviewReviewees)
                .Include(u => u.TblUserSkills).ThenInclude(us => us.Skill)
                .Where(u => u.IsActive)
                .Where(u => !currentUserId.HasValue || u.UserId != currentUserId.Value) // ✅ exclude current user
                .Take(10)
                .ToListAsync();

            var highestRatedFreelancersVM = highestRatedUsers.Select(u => new FreelancerCardVM
            {
                UserId = u.UserId,
                Name = u.UserName,
                Designation = u.Designation,
                ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                    ? "/template_assets/images/No_Profile_img.png"
                    : u.ProfileImageUrl,
                Location = u.Country,
                Rating = (double)(u.AverageRating ?? 0m),
                ReviewCount = u.ReviewCount ?? 0,
                JobSuccess = u.JobSuccessRate ?? 0,
                Recommendation = u.RecommendedPercentage ?? 0,
                OfferedSkillAreas = u.TblUserSkills
                    .Where(us => us.IsOffering)
                    .OrderByDescending(us => us.ProficiencyLevel)
                    .Take(3)
                    .Select(us => us.Skill.SkillName)
                    .ToList(),
                IsVerified = u.IsVerified
            }).ToList();

            //// 4) Top skills in the visitor’s country
            //var acceptLang = HttpContext.Request.Headers["Accept-Language"].ToString();
            //string lang = acceptLang.Split(',').FirstOrDefault() ?? "";

            //RegionInfo region;
            //try
            //{
            //    // 2) Create a CultureInfo ("en-US") then RegionInfo("US")
            //    var ci = new CultureInfo(lang);
            //    region = new RegionInfo(ci.Name);
            //}
            //catch
            //{
            //    // fallback if it wasn’t a well-formed culture
            //    region = RegionInfo.CurrentRegion;
            //}

            //// Now you have both:
            //string countryIso = region.TwoLetterISORegionName; // e.g. "IN"
            //string countryName = region.EnglishName;             // e.g. "India"

            bool isVerified = false;
            if (currentUserId.HasValue)
            {
                isVerified = await _dbcontext.TblUsers
                    .Where(u => u.UserId == currentUserId.Value)
                    .Select(u => u.IsVerified)
                    .FirstOrDefaultAsync();
            }

            var vm = new HomePageVM
            {
                TrendingOffers = trendingOfferVMs,
                HighestRatedFreelancers = highestRatedFreelancersVM,
                TrendingOffersByCategory = new List<CategoryOffers>(),
                PopularCategories = cats,
                TotalSkills = totalSkills,
                TalentsDisplayValue = fDisp,
                TalentsSuffix = fSfx,
                ExchangeDisplayValue = eDisp,
                ExchangeSuffix = eSfx,
                GlobalAverageRating = Math.Round(globalAvgRating, 1),
                SwapSatisfactionPercent = satisfactionPct,
                EarlyAdopterCount = displayCount,
                //UserCountryIso = countryIso,
                //UserCountryName = countryName,
                IsVerified = isVerified,
                RecentBlogPosts = (await _blogService.ListAsync(1, 3))
                        .Items
                        .ToList()
            };

            // 1) TOP SKILLS overall (by # of offers)
            var topSkills = await _dbcontext.TblUserSkills
                .Where(us => us.IsOffering)
                .GroupBy(us => us.Skill.SkillName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToListAsync();
            vm.TopSkills = topSkills.Select(x => x.Name).ToList();

            // 2) TRENDING SKILLS (last 7-day spike)
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var trending = await _dbcontext.TblUserSkills
                .Where(us => us.IsOffering)
                .GroupBy(us => us.Skill.SkillName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToListAsync();
            vm.TrendingSkills = trending.Select(x => x.Name).ToList();

            // 4) PROJECT CATALOG — e.g. distinct portfolio tags, or reuse topSkills:
            vm.ProjectCatalog = vm.TopSkills.Take(20).ToList();

            var goodSwaps = await _dbcontext.TblOffers
                .Where(o => o.IsActive && !o.IsDeleted && (!currentUserId.HasValue || o.UserId != currentUserId.Value))
                .OrderByDescending(o => o.JobSuccessRate)
                .ThenByDescending(o => o.TokenCost)
                .Take(10)
                .Select(o => new OfferCardVM
                {
                    OfferId = o.OfferId,
                    ShortTitle = o.Title!.Length > 35
                                          ? o.Title.Substring(0, 35) + "…"
                                          : o.Title,
                })
                .ToListAsync();
            vm.GoodSwaps = goodSwaps;

            const string sessionsKey = "RecentOfferSummaries";
            List<int> recentIds;
            if (Request.Cookies.TryGetValue(sessionsKey, out var json))
            {
                try
                {
                    recentIds = JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
                }
                catch
                {
                    recentIds = new List<int>();
                }
            }
            else
            {
                recentIds = new List<int>();
            }

            vm.RecentlyViewedOffers = HttpContext.Session
                .GetObjectFromJson<List<OfferCardVM>>(sessionsKey)
                ?? new List<OfferCardVM>();

            ViewBag.SuccessMessage = TempData.ContainsKey("SuccessMessage") ? TempData["SuccessMessage"] : null;
            ViewBag.ErrorMessage = TempData.ContainsKey("ErrorMessage") ? TempData["ErrorMessage"] : null;

            return View(vm);
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = "An unexpected error occurred.";
            return RedirectToAction("EP500", "EP");
        }

    }

    public async Task<IActionResult> HowItWorks()
    {
        // 1) Active Swappers
        int actualUsers = await _dbcontext.TblUsers.CountAsync(u => u.IsActive);
        decimal talentsBonus = _config.GetValue<decimal>("TrustMetrics:TalentsBonusPercent") / 100m;
        int displayTalents = actualUsers + (int)Math.Ceiling(actualUsers * talentsBonus);
        var (tDisp, tSfx) = FormatNumber(displayTalents);

        // 2) Member Satisfaction (% positive reviews)
        int totalReviews = await _dbcontext.TblReviews.CountAsync();
        int happyReviews = await _dbcontext.TblReviews
            .CountAsync(r => r.Rating >= _satisfactionThreshold);
        int satisfactionPct = totalReviews == 0
            ? 100
            : (int)Math.Round(happyReviews * 100m / totalReviews);

        // 3) Swaps Completed
        //int completedExchanges = await _dbcontext.TblExchanges
        //    .CountAsync(e => e.Status == "Completed");
        //decimal exchangeBonus = _config.GetValue<decimal>("TrustMetrics:FreelancerBonusPercent") / 100m;
        //int displayExchanges = completedExchanges
        //    + (int)Math.Ceiling(completedExchanges * exchangeBonus);
        //var (eDisp, eSfx) = FormatNumber(displayExchanges);

        // 4) Swap Success Rate (global % of completed / total)
        //int totalExchanges = await _dbcontext.TblExchanges.CountAsync();
        //int globalSuccess = totalExchanges == 0
        //    ? 100
        //    : (int)Math.Round(completedExchanges * 100m / totalExchanges);

        // read the 35% bonus
        int bonusPct = _config.GetValue<int>("TrustMetrics:TalentsBonusPercent");

        // 1) raw completed swaps
        int completedSwaps = await _dbcontext.TblExchanges
            .CountAsync(e => e.Status == "Completed");

        // apply bonus
        int bonusSwaps = (int)Math.Ceiling(completedSwaps * (bonusPct / 100m));
        int displaySwaps = completedSwaps + bonusSwaps;

        // format with K/M suffix
        var (swapDisp, swapSfx) = FormatNumber(displaySwaps);

        // 2) raw success rate
        int totalSwaps = await _dbcontext.TblExchanges.CountAsync();
        int rawSuccessPct = totalSwaps == 0
            ? 100
            : (int)Math.Round(completedSwaps * 100m / totalSwaps);

        // apply that same bonus percent to the rate
        int adjustedSuccess = rawSuccessPct
            + (int)Math.Ceiling(rawSuccessPct * (bonusPct / 100m));

        var vm = new HowItWorksVM
        {
            TalentsDisplayValue = tDisp,
            TalentsSuffix = tSfx,
            SwapSatisfactionPercent = satisfactionPct,
            //ExchangeDisplayValue = eDisp,
            //ExchangeSuffix = eSfx,
            //GlobalSuccessRate = globalSuccess,
            SwapsCompletedValue = swapDisp,
            SwapsCompletedSuffix = swapSfx,

            AdjustedSuccessRate = adjustedSuccess,
        };

        // Now load your spotlight members (top 5 for example)
        var topUsers = await _dbcontext.TblUsers
            .Include(u => u.TblUserSkills).ThenInclude(us => us.Skill)
            .Include(u => u.TblReviewReviewees)
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.JobSuccessRate)
            .ThenByDescending(u => u.ReviewCount)
            .Take(6)
            .ToListAsync();

        // Map to FreelancerCardVM
        vm.CommunitySpotlight = topUsers.Select(u => new FreelancerCardVM
        {
            UserId = u.UserId,
            Name = u.UserName,
            Designation = u.Designation,
            ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                              ? "/template_assets/images/No_Profile_img.png"
                              : u.ProfileImageUrl,
            Location = u.Country,
            Rating = (double)(u.AverageRating ?? 0m),
            ReviewCount = u.ReviewCount ?? 0,
            JobSuccess = u.JobSuccessRate ?? 0,
            Recommendation = u.RecommendedPercentage ?? 0,
            OfferedSkillAreas = u.TblUserSkills
                .Where(us => us.IsOffering)
                .OrderByDescending(us => us.ProficiencyLevel)
                .Take(3)
                .Select(us => us.Skill.SkillName)
                .ToList()
        }).ToList();


        return View(vm);
    }

    public async Task<IActionResult> About()
    {
        // 1) Active Swappers + bonus
        int actualUsers = await _dbcontext.TblUsers.CountAsync(u => u.IsActive);
        decimal bonusPct = _config.GetValue<decimal>("TrustMetrics:TalentsBonusPercent") / 100m;
        int displayTalentsRaw = actualUsers + (int)Math.Ceiling(actualUsers * bonusPct);
        var (tDisp, tSfx) = FormatNumber(displayTalentsRaw);

        // 2) Satisfaction %
        int totalReviews = await _dbcontext.TblReviews.CountAsync();
        int happyReviews = await _dbcontext.TblReviews.CountAsync(r => r.Rating >= _config.GetValue<int>("TrustMetrics:SatisfactionThreshold"));
        int satisfactionPct = totalReviews == 0
            ? 100
            : (int)Math.Round(happyReviews * 100m / totalReviews);

        // 3) Completed swaps + bonus
        int completedSwaps = await _dbcontext.TblExchanges.CountAsync(e => e.Status == "Completed");
        int bonusSwaps = (int)Math.Ceiling(completedSwaps * bonusPct);
        int displaySwaps = completedSwaps + bonusSwaps;
        var (swapDisp, swapSfx) = FormatNumber(displaySwaps);

        // 4) Adjusted success rate
        int totalSwaps = await _dbcontext.TblExchanges.CountAsync();
        int rawSuccessPct = totalSwaps == 0
            ? 100
            : (int)Math.Round(completedSwaps * 100m / totalSwaps);
        int adjustedSuccess = rawSuccessPct + (int)Math.Ceiling(rawSuccessPct * bonusPct);

        int displayCount = actualUsers + (int)Math.Ceiling(actualUsers * bonusPct);

        // 1. Pull down only the Experience strings:
        var experienceStrings = await _dbcontext.TblUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.Experience))
            .Select(u => u.Experience)
            .ToListAsync();

        // 2. Parse & compute average in C#:
        var regex = new Regex(@"(\d+(\.\d+)?)");
        var numericExps = experienceStrings
            .Select(s =>
            {
                // Find the first numeric match (e.g. "1.6")
                var m = regex.Match(s);
                if (m.Success &&
                    double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var yrs))
                {
                    return yrs;
                }
                return 0d;
            })
            .Where(v => v > 0)
            .ToList();

        // 3. Compute the average (or zero if no valid entries)
        var avgExp = numericExps.Any()
            ? numericExps.Average()
            : 0d;

        // 4. Round or format as desired before sending to the view-model
        var roundedAvg = Math.Round(avgExp, 1);  // e.g. 1.6

        // 1) Pull the top 4 verified, active users ordered by success → reviews
        var top4 = await _dbcontext.TblUsers
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.UserId,
                u.UserName,
                u.Designation,
                ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                                    ? "/template_assets/images/No_Profile_img.png"
                                    : u.ProfileImageUrl,
                u.Country,
                // compute review metrics inline
                ReviewCount = _dbcontext.TblReviews.Count(r => r.UserId == u.UserId),
                AvgRating = _dbcontext.TblReviews
                                  .Where(r => r.UserId == u.UserId)
                                  .Select(r => (double?)r.Rating)
                                  .Average() ?? 0,
                RecPct = _dbcontext.TblReviews
                                  .Where(r => r.UserId == u.UserId && r.Rating >= 4)
                                  .Count() * 100.0
                              / Math.Max(1, _dbcontext.TblReviews.Count(r => r.UserId == u.UserId)),
                // compute success %
                TotalEx = _dbcontext.TblExchanges
                                .Count(e => e.OfferOwnerId == u.UserId || e.OtherUserId == u.UserId),
                CompletedEx = _dbcontext.TblExchanges
                                .Count(e => (e.OfferOwnerId == u.UserId || e.OtherUserId == u.UserId)
                                            && e.Status == "Completed"),
                // take up to 3 offered skills
                TopSkills = _dbcontext.TblUserSkills
                                .Where(us => us.UserId == u.UserId && us.IsOffering)
                                .OrderByDescending(us => us.ProficiencyLevel)
                                .Take(3)
                                .Select(us => us.Skill.SkillName)
                                .ToList()
            })
            .ToListAsync();

        // 2) project into your view‐model
        var spotlight = top4
            .Select(x => new FreelancerCardVM
            {
                UserId = x.UserId,
                Name = x.UserName,
                Designation = x.Designation,
                ProfileImage = x.ProfileImage,
                Location = x.Country,
                Rating = x.AvgRating,
                ReviewCount = x.ReviewCount,
                Recommendation = x.RecPct,
                JobSuccess = x.TotalEx > 0
                                     ? x.CompletedEx * 100.0 / x.TotalEx
                                     : 0,
                OfferedSkillAreas = x.TopSkills
            })
            .OrderByDescending(u => u.Rating)         // optional: sort the final 4 how you like
            .ThenByDescending(u => u.Recommendation)
            .ToList();

        // base count:
        int baseCount = spotlight.Count;

        // apply it:
        int boostedCount = baseCount + (int)Math.Ceiling(baseCount * bonusPct);

        // format with your existing helper:
        var (vfDisp, vfSuffix) = FormatNumber(boostedCount);

        var vm = new AboutUsVM
        {
            TalentsDisplayValue = tDisp,
            TalentsSuffix = tSfx,
            SwapSatisfactionPercent = satisfactionPct,
            SwapsCompletedValue = swapDisp,
            SwapsCompletedSuffix = swapSfx,
            AdjustedSuccessRate = adjustedSuccess,
            EarlyAdopterCount = displayCount,
            AverageExperience = avgExp,
            CommunitySpotlight = spotlight,
            VerifiedCountDisplay = vfDisp,
            VerifiedCountSuffix = vfSuffix
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Contact()
        => View(new ContactFormVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormVM form)
    {
        if (form.Attachment != null && form.Attachment.Length > 0)
        {
            const long MAX_BYTES = 2 * 1024 * 1024;           // 2 MB
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var ext = Path.GetExtension(form.Attachment.FileName)?.ToLowerInvariant();

            if (form.Attachment.Length > MAX_BYTES)
                ModelState.AddModelError(
                    nameof(form.Attachment),
                    "Max file size is 2 MB.");

            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                ModelState.AddModelError(
                    nameof(form.Attachment),
                    "Allowed file types: .jpg, .png, .pdf");
        }

        if (!ModelState.IsValid)
            return View(form);

        using var tx = await _dbcontext.Database.BeginTransactionAsync();
        try
        {
            // 1) Save to DB
            var msg = new TblUserContactRequest
            {
                Name = form.Name,
                Email = form.Email,
                Phone = form.Phone,
                Subject = form.Subject,
                Category = form.Category,
                Message = form.Message,
                IsResolved = false,
                HasSupportContacted = false,
                CreatedAt = DateTime.UtcNow
            };
            _dbcontext.TblUserContactRequests.Add(msg);
            await _dbcontext.SaveChangesAsync();

            if (form.Attachment != null && form.Attachment.Length > 0)
            {
                using var ms = new MemoryStream();
                await form.Attachment.CopyToAsync(ms);

                msg.AttachmentData = ms.ToArray();
                msg.AttachmentFilename = form.Attachment.FileName;
                msg.AttachmentContentType = form.Attachment.ContentType;

                // Update the record
                _dbcontext.TblUserContactRequests.Update(msg);
                await _dbcontext.SaveChangesAsync();
            }

            // 2) Send confirmation to user
            var userBody = $@"
            <p>Hi {form.Name},</p>

            <p>Thanks for reaching out to SkillSwap! We’ve received your message about “<b>{form.Subject}</b>” in the {form.Category} category:</p>

            <blockquote style=""border-left: 4px solid #ccc; margin: 1em 0; padding-left: 1em;"">
                {form.Message}
            </blockquote>

            <p>One of our team members will review your request and be in touch within 24 hours. We appreciate you being part of our community and look forward to helping you swap skills with confidence.</p>

            <p>Warm regards,<br/>
            The SkillSwap Team</p>";


            await _emailService.SendEmailAsync(
                to: form.Email,
                subject: "We’ve received your support request",
                body: userBody,
                isBodyHtml: true
            );

            TempData["ContactSuccess"] = "Thank you! Your message has been sent.";
            return RedirectToAction(nameof(Contact));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["ContactError"] = "⚠️ An error occurred while sending your message.";
            _logger.LogError(ex, "Failed to send contact form");
            return RedirectToAction("EP500", "EP");
        }
    }

    #endregion

    #region External Login

    [AllowAnonymous, HttpGet]
    public IActionResult ExternalLogin(string provider, string returnUrl = "/")
    {
        if (string.IsNullOrEmpty(provider))
            return RedirectToAction(nameof(Login));

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var props = new AuthenticationProperties
        {
            RedirectUri = redirectUrl,
            Items = { { "scheme", provider } }
        };
        return Challenge(props, provider);
    }

    // handles Google’s callback
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
    {
        // grab the result
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded)
            return RedirectToAction(nameof(Login));

        // pull the email claim
        var email = result.Principal.FindFirst(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return RedirectToAction(nameof(Login));

        // lookup or create your user
        var user = await _userService.GetUserByUserNameOrEmailAsync(null, email);
        if (user == null)
        {
            user = new TblUser
            {
                Email = email,
                UserName = email,
                IsActive = true
            };
            await _userService.RegisterUserAsync(user, Guid.NewGuid().ToString("N"));
        }

        if (user.IsHeld)
        {
            TempData["ErrorMessage"] = "🚫 Your account has been held by an administrator. Please contact support.";
            return RedirectToAction(nameof(Login));
        }

        // If their hold has expired, auto-release them now
        if (user.IsHeld && user.HeldUntil.HasValue && user.HeldUntil <= DateTime.UtcNow)
        {
            await _usv.ReleaseUserAsync(
                user.UserId,
                reason: "Auto-released on login",
                adminId: null);
            // Refresh the user object
            user = await _userService.GetUserByIdAsync(user.UserId);
        }

        // sign in locally
        await SignInUserAsync(user, true);

        // clear the external cookie
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return LocalRedirect(returnUrl);
    }

    #endregion

    #region Register

    // GET: /Home/Register
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // POST: /Home/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(UserRegisterationVM model)
    {
        if (!string.IsNullOrEmpty(model.ContactNo))
        {
            model.ContactNo = Regex.Replace(model.ContactNo, @"\D", "");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Start a transaction for registration flow.
        using (var transaction = await _dbcontext.Database.BeginTransactionAsync())
        {
            try
            {
                // Check for duplicate username or email.
                var existingUser = await _userService.GetUserByUserNameOrEmailAsync(model.UserName, model.Email);
                if (existingUser != null)
                {
                    if (existingUser.Email == model.Email)
                        TempData["ErrorMessage"] = "❌ This email is already registered.";
                    if (existingUser.UserName == model.UserName)
                        TempData["ErrorMessage"] = "❌ This username is already taken.";
                    return View(model);
                }

                // Generate TOTP secret (stored as plain Base32) for 2FA.
                string TotpSecret = TotpHelper.GenerateSecretKey(); // 🔥 Store plain Base32 TOTP secret

                // Create a temporary user object.
                var tempUser = new TblUser
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UserName = model.UserName,
                    Email = model.Email,
                    ContactNo = model.ContactNo,
                    TotpSecret = TotpSecret, // 🔥 Store plain Base32 secret
                    IsOnboardingCompleted = false // Ensure onboarding starts
                };

                // Save temporary user info in session for 2FA setup.
                HttpContext.Session.SetInt32("TempUserId", tempUser.UserId);
                HttpContext.Session.SetObjectAsJson("TempUser", tempUser);
                HttpContext.Session.SetString("TempUser_Password", model.Password);

                // Commit transaction since session state is not transactional but registration flow is completed.
                await transaction.CommitAsync();

                // Redirect to 2FA setup.
                return RedirectToAction("Setup2FA");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "⚠️ An unexpected error occurred during registration.";
                Debug.WriteLine($"[Register Error] {ex.Message}");
                return RedirectToAction("EP500", "EP");
            }
        }
    }

    #endregion

    #region Login

    // GET: /Home/Login
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.ErrorMessage = TempData["ErrorMessage"];
        return View();
    }

    // POST: /Home/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireHttps]                      // ensure this endpoint is always HTTPS
    public async Task<IActionResult> Login(UserLoginVM model, string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // 1) Look up the user by login name or email
            var user = await _userService.GetUserByUserNameOrEmailAsync(model.LoginName, model.LoginName);
            if (user == null)
            {
                // generic error, don’t reveal if username or password was bad
                _logger.LogWarning("Login failed for unknown user '{LoginName}'", model.LoginName);
                TempData["ErrorMessage"] = "Invalid username or password.";
                return View(model);
            }

            // 2) Check for lockout
            if (user.LockoutEndTime.HasValue && user.LockoutEndTime > DateTime.UtcNow)
            {
                var unlock = TimeZoneInfo.ConvertTimeFromUtc(
                    user.LockoutEndTime.Value,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));

                TempData["ErrorMessage"] = $"Your account is locked until {unlock:dd MMM yyyy hh:mm tt} IST.";
                return View(model);
            }

            // 3) Validate credentials via your service (which uses parameterized EF under the hood)
            var authenticatedUser = await _userService.ValidateUserCredentialsAsync(model.LoginName, model.Password);
            if (authenticatedUser == null)
            {
                // increment failure count
                user.FailedOtpAttempts++;

                if (user.FailedOtpAttempts >= 5)
                {
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(15);
                    _logger.LogWarning("User {UserId} locked out until {LockoutEnd}", user.UserId, user.LockoutEndTime);
                }

                await _dbcontext.SaveChangesAsync();

                TempData["ErrorMessage"] = "Invalid username or password.";
                return View(model);
            }

            // 4) Success!  Reset failure counters.
            user.FailedOtpAttempts = 0;
            user.LockoutEndTime = null;
            await _dbcontext.SaveChangesAsync();

            // Force a fresh DB fetch to ensure updated user data.
            user = await _userService.GetUserByIdAsync(user.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Unable to retrieve user data.";
                return View(model);
            }


            // If their hold has expired, auto-release them now
            if (user.IsHeld && user.HeldUntil.HasValue && user.HeldUntil <= DateTime.UtcNow)
            {
                await _usv.ReleaseUserAsync(
                    user.UserId,
                    reason: "Auto-released on login",
                    adminId: null);
                // Refresh the user object
                user = await _userService.GetUserByIdAsync(user.UserId);
            }

            if (user.IsHeld)
            {
                // you could also read LockoutEndTime if you want to show how long
                TempData["ErrorMessage"] = "🚫 Your account has been held by an administrator. Please contact support.";
                return View(model);
            }

            // Check if the user is locked out.
            if (user.FailedOtpAttempts >= 5 && user.LockoutEndTime.HasValue && user.LockoutEndTime > DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "🚫 Too many failed attempts. Try again later.";
                return View(model);
            }

            // Redirect to onboarding if not completed.
            if (!user.IsOnboardingCompleted)
            {
                HttpContext.Session.SetInt32("TempUserId", user.UserId); // 🔥 Store UserId for onboarding
                return RedirectToAction("SelectRole", "Onboarding"); // 🚀 Redirect to first onboarding step
            }

            // If not approved by admin, do not allow login.
            //if (!user.IsVerified)
            //{
            //    TempData["ApprovalMessage"] = "🔎 Your account is not yet approved.";
            //    return View(model);
            //}

            // Set TempData to show the banner once
            TempData["ShowAuthBanner"] = true;

            // If TOTP (2FA) is enabled, redirect to OTP verification.
            if (!string.IsNullOrEmpty(user.TotpSecret))
            {
                HttpContext.Session.SetObjectAsJson("LoginUser", user);
                HttpContext.Session.SetString("LoginUser_Password", model.Password);
                TempData["InfoMessage"] = "Please enter your OTP.";
                return RedirectToAction("LoginOtp", new { returnUrl });
            }
            else
            {
                // Sign in the user.
                await SignInUserAsync(user, model.RememberMe);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred while logging in.";
            Debug.WriteLine($"[Login Error] {ex.Message}");
            return RedirectToAction("EP500", "EP");
        }
    }
    #endregion

    #region Login OTP

    // GET: /Home/LoginOtp
    [HttpGet]
    public async Task<IActionResult> LoginOtp(string returnUrl = null)
    {
        var loginUser = HttpContext.Session.GetObjectFromJson<TblUser>("LoginUser");
        if (loginUser == null)
        {
            TempData["ErrorMessage"] = "Session expired. Please log in again.";
            return RedirectToAction("Login");
        }

        // Check if we already have an OTP and it’s still valid
        var existing = HttpContext.Session.GetString("LoginEmailOtp");
        var expiresString = HttpContext.Session.GetString("LoginEmailOtp_Expires");
        var needsNewOtp = string.IsNullOrEmpty(existing)
                      || !DateTimeOffset.TryParse(expiresString, out var exp)
                      || DateTimeOffset.UtcNow >= exp;

        // read the raw string from session
        var rawNext = HttpContext.Session.GetString("NextResendAt");

        DateTimeOffset? nextAllowed = null;
        if (!string.IsNullOrEmpty(rawNext)
            && DateTimeOffset.TryParse(rawNext, out var parsed))
        {
            nextAllowed = parsed;
        }

        int retryAfter = 0;
        if (nextAllowed.HasValue && nextAllowed.Value > DateTimeOffset.UtcNow)
        {
            retryAfter = (int)(nextAllowed.Value - DateTimeOffset.UtcNow).TotalSeconds;
        }

        ViewBag.ResendRetryAfter = retryAfter;
        ViewBag.ResendAttemptsLeft = Math.Max(
            0,
            3 - (HttpContext.Session.GetInt32("ResendCount") ?? 0)
        );

        if (needsNewOtp)
        {
            // generate & email OTP
            var emailOtp = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("LoginEmailOtp", emailOtp);
            HttpContext.Session.SetString("LoginEmailOtp_Expires", DateTimeOffset.UtcNow.AddMinutes(5).ToString());

            var htmlBody = $@"
                  <p>Hi {loginUser.UserName},</p>
                  <p>Here’s your one‑time login code: <strong>{emailOtp}</strong></p>
                  <p>
                    Enter this code on the SkillSwap login screen to access your account.<br/>
                    The code is valid for the next 05 minutes.
                  </p>
                  <p>If you didn’t request this code, you can safely ignore this email.</p>
                  <p>Thanks for being part of SkillSwap!<br/>
                     The SkillSwap Team
                  </p>
                ";

            await _emailService.SendEmailAsync(
                loginUser.Email,
                "Your SkillSwap login code",
                htmlBody,
                isBodyHtml: true    // or however your service flags an HTML payload
            );
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(loginUser);
    }

    // POST: /Home/LoginOtp
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginOtp(string otp, string returnUrl = null)
    {
        var loginUser = HttpContext.Session.GetObjectFromJson<TblUser>("LoginUser");
        if (loginUser == null)
        {
            TempData["ErrorMessage"] = "Session expired. Please log in again.";
            return RedirectToAction("Login");
        }

        // Pull a fresh copy from the database so we can update their counters
        var user = await _dbcontext.TblUsers
            .IgnoreQueryFilters()                              // ← bypass the “no-Admin” filter
            .FirstOrDefaultAsync(u => u.UserId == loginUser.UserId);
        if (user == null)
        {
            _logger.LogError("LoginOtp: user {UserId} not found in System", loginUser.UserId);
            TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
            return RedirectToAction("Login");
        }

        if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value <= DateTime.UtcNow)
        {
            user.LockoutEndTime = null;
            user.FailedOtpAttempts = 0;
            await _dbcontext.SaveChangesAsync();
        }

        if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value > DateTime.UtcNow)
        {
            // convert to local/IST for display
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(
                              DateTime.SpecifyKind(user.LockoutEndTime.Value, DateTimeKind.Utc),
                              TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
                           );
            TempData["ErrorMessage"] =
                $"🚫 Too many failed OTP attempts. Try again at {localEnd:dd MMM yyyy hh:mm tt} IST.";
            return RedirectToAction(nameof(LoginOtp), new { returnUrl });
        }

        // 1) Gather context
        string ip;
        // 1a) First check for X-Forwarded-For (may be a comma-separated list; take the first)
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd)
            && !StringValues.IsNullOrEmpty(fwd))
        {
            ip = fwd.ToString().Split(',')[0].Trim();
        }
        else if (HttpContext.Connection.RemoteIpAddress != null)
        {
            var addr = HttpContext.Connection.RemoteIpAddress!;
            // if it’s any loopback (127.0.0.1 or ::1), normalize to IPv4 127.0.0.1
            if (IPAddress.IsLoopback(addr))
            {
                ip = IPAddress.Loopback.ToString();  // "127.0.0.1"
            }
            else
            {
                // for IPv6-mapped IPv4, or real IPv6, this will map back to IPv4 where possible
                ip = addr.MapToIPv4().ToString();
            }
        }
        else
        {
            ip = "unknown";
        }

        bool isTotpOk = TotpHelper.VerifyTotpCode(loginUser.TotpSecret, otp, out string totpFailure);
        var emailOtp = HttpContext.Session.GetString("LoginEmailOtp");
        bool isEmailOk = emailOtp != null && emailOtp == otp;
        var method = isTotpOk ? "TOTP" : "Email";

        // 2) Log the attempt immediately (fail by default)
        var attempt = new OtpAttempt
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AttemptedAt = DateTime.UtcNow,
            WasSuccessful = false,
            Method = method,
            IpAddress = ip
        };
        _dbcontext.OtpAttempts.Add(attempt);
        await _dbcontext.SaveChangesAsync();

        try
        {
            if (!isTotpOk && !isEmailOk)
            {
                // 1) Increment the failure count
                user.FailedOtpAttempts++;

                // 2) If they've now reached 5 failures, lock them out for 15 minutes
                if (user.FailedOtpAttempts >= 5)
                {
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(15);

                    // Convert for the email body
                    var utcLock = DateTime.SpecifyKind(user.LockoutEndTime.Value, DateTimeKind.Utc);
                    var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                    var localLockEnd = TimeZoneInfo.ConvertTimeFromUtc(utcLock, istZone);

                    // Notify by email
                    var lockSubject = "Your SkillSwap Login Temporarily Locked";
                    var lockBody = $@"
                        <p>Hi {user.FirstName},</p>
                        <p>Due to multiple unsuccessful login attempts, your account is locked for 15 minutes from now.</p>
                        <p>Please wait until <strong>{localLockEnd:dd MMM yyyy HH:mm tt} IST</strong> before trying again.</p>
                        <p>If this wasn’t you, contact us at <a href=""mailto:support@skillswap.com"">support@skillswap.com</a>.</p>
                        <p>— The SkillSwap Team</p>";
                    await _emailService.SendEmailAsync(user.Email, lockSubject, lockBody, isBodyHtml: true);
                }

                // 3) Persist the incremented count (and lockout if set)
                await _dbcontext.SaveChangesAsync();

                // 5) Show the appropriate message
                if (user.FailedOtpAttempts >= 5)
                {
                    TempData["ErrorMessage"] = "🚫 Too many failed attempts. Your account is locked for 15 minutes.";
                }
                else
                {
                    TempData["ErrorMessage"] = "❌ The email code you entered is incorrect.";
                }

                ModelState.AddModelError(
                    string.Empty,
                    "❌ Your code has expired. Please request a new one.");

                ViewBag.ReturnUrl = returnUrl;
                return View(loginUser);
            }

            attempt.WasSuccessful = true;
            await _dbcontext.SaveChangesAsync();

            var expires = DateTimeOffset.Parse(HttpContext.Session.GetString("LoginEmailOtp_Expires"));
            if (DateTimeOffset.UtcNow > expires)
            {
                TempData["ErrorMessage"] = "❌ That code has expired. Please request a new one.";
                return RedirectToAction(nameof(LoginOtp));
            }

            // ✔️ Good OTP: reset failure count & lockout, persist once
            user.FailedOtpAttempts = 0;
            user.LockoutEndTime = null;
            await _dbcontext.SaveChangesAsync();

            HttpContext.Session.Remove("LoginEmailOtp");
            HttpContext.Session.Remove("LoginEmailOtp_Expires");

            // clear it so it can't be replayed
            if (user.FailedOtpAttempts >= 5)
            {
                HttpContext.Session.Remove("LoginEmailOtp");
                HttpContext.Session.Remove("LoginEmailOtp_Expires");
            }

            // OTP is valid, sign in the user
            await SignInUserAsync(loginUser, false);
            HttpContext.Session.Remove("LoginUser");
            HttpContext.Session.Remove("LoginUser_Password");
            TempData["SuccessMessage"] = "✅ OTP verification successful! You can log in now.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred while verifying OTP.";
            Debug.WriteLine($"[OTP Error] {ex.Message}");
            return RedirectToAction("LoginOtp", new { returnUrl });
        }
    }

    #endregion

    #region Two-Factor Authentication (2FA) Setup

    [HttpGet]
    public async Task<IActionResult> Setup2FA(string email)
    {
        // Retrieve temporary user from session.
        var tempUser = HttpContext.Session.GetObjectFromJson<TblUser>("TempUser");
        if (tempUser == null)
        {
            TempData["ErrorMessage"] = "⚠️ User session expired. Please register again.";
            return RedirectToAction("Register");
        }

        // 1️⃣ Generate email OTP
        var emailOtp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("EmailOtp", emailOtp);

        var htmlBody = $@"
              <p>Hi {tempUser.UserName},</p>
              <p>Thanks for joining <strong>SkillSwap</strong>! Here’s your one‑time verification code: <strong>{emailOtp}</strong></p>
              <p>Enter this code to verify your email and activate your account.<br/>
                 The code is valid for the next 05 minutes.</p>
              <p>If you didn’t request this, feel free to ignore this message.</p>
              <p>Welcome aboard,<br/>The SkillSwap Team</p>
            ".Trim();

        // 2️⃣ Send it
        await _emailService.SendEmailAsync(
            tempUser.Email,
            subject: "Your SkillSwap verification code",
            htmlBody,
            isBodyHtml: true
            );

        TempData["EmailOtpSent"] = true;

        // Generate QR Code URL for TOTP setup.
        string qrCodeUrl = TotpHelper.GenerateQrCodeUrl(tempUser.TotpSecret, tempUser.Email);
        ViewBag.QrCodeUrl = qrCodeUrl;
        return View(tempUser);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string email, string otp)
    {
        try
        {
            var tempUser = HttpContext.Session.GetObjectFromJson<TblUser>("TempUser");

            if (tempUser == null || tempUser.Email != email)
            {
                TempData["ErrorMessage"] = "⚠️ Session expired. Please register again.";
                return RedirectToAction("Register");
            }

            // pull the email‐OTP we stored
            var emailOtp = HttpContext.Session.GetString("EmailOtp");

            // check Google Auth TOTP
            string totpFailure;
            bool isTotpValid = TotpHelper.VerifyTotpCode(tempUser.TotpSecret, otp, out totpFailure);
            // check email‐OTP
            bool isEmailValid = emailOtp != null && emailOtp == otp;

            if (!isTotpValid && !isEmailValid)
            {
                TempData["ErrorMessage"] = "❌ Invalid code. Try your authenticator app or the email code.";
                return View("Setup2FA");
            }

            tempUser.EmailConfirmed = true;
            string password = HttpContext.Session.GetString("TempUser_Password");

            // Verify OTP.
            //if (!TotpHelper.VerifyTotpCode(tempUser.TotpSecret, otp))
            //{
            //    TempData["ErrorMessage"] = "❌ Invalid OTP. Try again.";
            //    return View("Setup2FA");
            //}

            // Create and register the new user.
            var newUser = new TblUser
            {
                FirstName = tempUser.FirstName,
                LastName = tempUser.LastName,
                UserName = tempUser.UserName,
                Email = tempUser.Email,
                ContactNo = tempUser.ContactNo,
                TotpSecret = tempUser.TotpSecret, // ✅ Store plain Base32 secret
                IsOnboardingCompleted = false, // 🚀 Ensuring user lands on onboarding
                EmailConfirmed = true
            };

            var result = await _userService.RegisterUserAsync(newUser, password);
            if (!result)
            {
                TempData["ErrorMessage"] = "⚠️ Registration failed. Please try again.";
                return View("Setup2FA");
            }

            // seed mining progress for this brand-new user
            _dbcontext.UserMiningProgresses.Add(new UserMiningProgress
            {
                UserId = newUser.UserId,
                LastEmittedUtc = DateTime.UtcNow,
                EmittedToday = 0m,
                IsMiningAllowed = true
            });
            await _dbcontext.SaveChangesAsync();

            // preserve TempUserId for onboardin
            HttpContext.Session.SetInt32("TempUserId", newUser.UserId);

            // Remove automatic login
            // await SignInUserAsync(newUser, false);

            TempData["SuccessMessage"] = "✅ OTP verification successful! You can log in now.";
            return RedirectToAction("SelectRole", "Onboarding"); // 🚀 Redirect to first onboarding step
        }

        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred while verifying OTP.";
            Debug.WriteLine($"[VerifyOtp Error] {ex.Message}");
            return View("Setup2FA");
        }
    }
    #endregion

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        const int MaxResends = 5;
        const int cooldownSeconds = 30;

        // retrieve the pending-login user
        var loginUser = HttpContext.Session.GetObjectFromJson<TblUser>("LoginUser");
        if (loginUser == null)
            return Json(new { success = false, message = "Session expired" });

        // 1) how many times we've already resent
        int sentCount = HttpContext.Session.GetInt32("ResendCount") ?? 0;

        // 2) when we're next allowed to resend
        var nextRaw = HttpContext.Session.GetString("NextResendAt");
        DateTimeOffset? nextAllowed = null;
        if (!string.IsNullOrEmpty(nextRaw)
            && DateTimeOffset.TryParse(nextRaw, out var parsed))
        {
            nextAllowed = parsed;
        }

        // 3) check cooldown
        if (nextAllowed.HasValue && nextAllowed.Value > DateTimeOffset.UtcNow)
        {
            var retryAfter = (int)(nextAllowed.Value - DateTimeOffset.UtcNow).TotalSeconds;
            return Json(new
            {
                success = false,
                retryAfter = retryAfter,
                message = $"Please wait {retryAfter}s before requesting another code."
            });
        }

        // 4) check total attempts
        if (sentCount >= MaxResends)
        {
            return Json(new
            {
                success = false,
                message = "You've reached the maximum number of resend attempts. Please try again later."
            });
        }

        // generate a new email OTP
        var emailOtp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("LoginEmailOtp", emailOtp);
        HttpContext.Session.SetString("LoginEmailOtp_Expires",
            DateTimeOffset.UtcNow.AddMinutes(5).ToString("o"));

        // increment and set a new cooldown
        HttpContext.Session.SetInt32("ResendCount", ++sentCount);
        HttpContext.Session.SetString("NextResendAt",
            DateTimeOffset.UtcNow.AddSeconds(cooldownSeconds).ToString());

        // send it
        var htmlBody = $@"
            <p>Hi {loginUser.UserName},</p>
            <p>Your new login code is: <strong>{emailOtp}</strong></p>
            <p>It’s valid for the next 05 minutes.</p>
            <p>If you didn’t request this, ignore this email.</p>";

        await _emailService.SendEmailAsync(
            loginUser.Email,
            "Your new SkillSwap login code",
            htmlBody,
            isBodyHtml: true);

        return Json(new
        {
            success = true,
            retryAfter = cooldownSeconds
        });
    }

    #region Logout
    // POST: /Home/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync("SkillSwapAuth");
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.SkillSwapAuth");
            TempData["SuccessMessage"] = "✅ Successfully logged out.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An error occurred during logout. Please try again.";
            Debug.WriteLine($"[Logout Error] {ex.Message}");
        }
        return RedirectToAction("Login", "Home");
    }
    #endregion

    #region PasswordReset

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        // 1) Basic validation
        if (string.IsNullOrWhiteSpace(email) || !ModelState.IsValidEmail(nameof(email)))
        {
            TempData["Error"] = "Please enter a valid email address.";
            return View();
        }

        // 2) Does this email exist?
        var user = await _userService.GetUserByUserNameOrEmailAsync(null, email);
        if (user == null)
        {
            TempData["Error"] = "We couldn’t find an account with that email.";
            return View();
        }

        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            await _passwordReset.SendResetLinkAsync(email, origin);
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }
        catch (Exception)
        {
            // log if you like
            ModelState.AddModelError("", "Something went wrong. Please try again later.");
            return View();
        }
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(string token, string email)
    {
        if (string.IsNullOrEmpty(token))
            return RedirectToAction(nameof(ForgotPassword));

        ViewBag.Email = email;
        ModelState.Clear();
        return View(model: token);
    }

    [HttpPost, AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("", "Passwords do not match.");
            return View(model: token);
        }
        if (newPassword.Length < 6)
        {
            ModelState.AddModelError("", "Password must be at least 8 characters.");
            return View(model: token);
        }

        // 1) Load the reset‐token entry
        var resetEntry = await _dbcontext.TblPasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.Expiration > DateTime.UtcNow);
        if (resetEntry == null)
        {
            ModelState.AddModelError("", "Invalid or expired reset link.");
            return View(model: token);
        }

        // 2) Fetch the actual user
        var user = await _userService.GetUserByIdAsync(resetEntry.UserId);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View(model: token);
        }

        // 3) Prevent re-using your *own* current password
        if (await _userService.ValidateUserCredentialsAsync(user.UserName, newPassword) != null)
        {
            ModelState.AddModelError(nameof(newPassword),
                "Your new password must be different from your current one.");
            return View(model: token);
        }

        // 4) Prevent using *any other* user’s current password
        if (await _passwordReset.IsPasswordInUseAsync(newPassword, excludingUserId: user.UserId))
        {
            ModelState.AddModelError(nameof(newPassword),
                "That password is already in use by another account. Please choose a different one.");
            return View(model: token);
        }

        // 5) All checks passed—actually reset
        var (succeeded, error) = await _passwordReset.ResetPasswordAsync(token, newPassword);
        if (!succeeded)
        {
            ModelState.AddModelError("", error!);
            return View(model: token);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPasswordConfirmation() => View();

    #endregion

    #region Helper Methods
    private static (string disp, string suffix) FormatNumber(int n)
    {
        if (n >= 1_000_000) return ((n / 1_000_000.0).ToString("0.#"), "M");
        if (n >= 1_000) return ((n / 1_000.0).ToString("0.#"), "K");
        return (n.ToString(), "");
    }

    private async Task SignInUserAsync(TblUser user, bool isPersistent)
    {
        try
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("OnboardingCompleted", user.IsOnboardingCompleted.ToString())
            };

            // Retrieve roles from the user service.
            var roles = await _userService.GetUserRolesAsync(user.UserId);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var claimsIdentity = new ClaimsIdentity(claims, "SkillSwapAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync("SkillSwapAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            // ensure mining progress exists on every login
            if (!await _dbcontext.UserMiningProgresses.AnyAsync(p => p.UserId == user.UserId))
            {
                _dbcontext.UserMiningProgresses.Add(new UserMiningProgress
                {
                    UserId = user.UserId,
                    LastEmittedUtc = DateTime.UtcNow,
                    EmittedToday = 0m,
                    IsMiningAllowed = true
                });
                await _dbcontext.SaveChangesAsync();
            }

            // Set a secure authentication cookie.
            //Response.Cookies.Append(".AspNetCore.SkillSwapAuth", "true", new CookieOptions
            //{
            //    Secure = true, // 🔥 Ensures HTTPS only
            //    HttpOnly = true, // Prevents JavaScript access (XSS protection)
            //    SameSite = SameSiteMode.Strict
            //});
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SignInUserAsync Error] {ex.Message}");
            throw;
        }
    }

    private static string PickIconClass(string category)
    {
        // map known categories to icon classes
        return category switch
        {
            "Programming & Tech" => "flaticon-developer",
            "Graphics & Design" => "flaticon-web-design-1",
            "Digital Marketing" => "flaticon-digital-marketing",
            "Writing & Translation" => "flaticon-translator",
            "Music & Audio" => "flaticon-microphone",
            "Video & Animation" => "flaticon-video-file",
            "Lifestyle" => "flaticon-ruler",
            "Business" => "flaticon-goal",
            _ => "flaticon-design"
        };
    }

    #endregion

    public async Task<IActionResult> TutorTeaching()
    {
        return View();
    }

    public async Task<IActionResult> PrivacyandPolicy()
    {
        return View();
    }

    public async Task<IActionResult> TermsofService()
    {
        return View();
    }

    public async Task<IActionResult> CookiePolicy()
    {
        return View();
    }

    public async Task<IActionResult> HelpandSupport()
    {
        return View();
    }

    public async Task<IActionResult> TrustandSafety()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet("Home/AccessDenied")]
    public IActionResult AccessDenied(string returnUrl)
    {
        // You can pass the returnUrl into the view if you want to show it.
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    private IActionResult RedirectToReferrer()
    {
        var referer = Request.Headers["Referer"].ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out _))
            return Redirect(referer);

        return RedirectToAction(nameof(Index));
    }

}