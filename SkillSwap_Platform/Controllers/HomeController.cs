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

namespace SkillSwap_Platform.Controllers;

public class HomeController : Controller
{
    private readonly IUserServices _userService;
    private readonly IPasswordResetService _passwordReset;
    private readonly SkillSwapDbContext _dbcontext;
    private readonly IEmailService _emailService;
    private readonly INewsletterService _newsletter;
    private readonly IConfiguration _config;
    private readonly int _satisfactionThreshold;
    public HomeController(IUserServices userService, SkillSwapDbContext context, IEmailService emailService, IPasswordResetService passwordReset, INewsletterService newsletter, IConfiguration config)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _dbcontext = context ?? throw new ArgumentNullException(nameof(context));
        _passwordReset = passwordReset;
        _emailService = emailService;
        _newsletter = newsletter;
        _config = config;
        _satisfactionThreshold = config.GetValue<int>("TrustMetrics:SatisfactionThreshold", 4);
    }

    // This runs on every request for views that share the layout
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        // Grab distinct categories
        var cats = _dbcontext.TblOffers
            .Select(o => o.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        ViewData["FooterCategories"] = cats;
        base.OnActionExecuting(ctx);
    }

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

        //await _newsletter.UnsubscribeAsync(email);
        ViewBag.Message = $"✅ {email} has been removed from our mailing list.";
        return View("UnsubscribeNewsletter");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubscribeNewsletter(string email)
    {
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
                <p>Hi there,</p>
                <p>🎉 Thank you for subscribing to the SkillSwap newsletter! You’ll now be among the first to hear about new features, tips, and exclusive offers.</p>
                <hr/>
                <p>If you ever wish to unsubscribe, just click the link at the bottom of any newsletter.
                    <a href=""{unsubscribeUrl}"">unsubscribe</a> at any time.
                </p>
                <p>Welcome aboard!<br/>— The SkillSwap Team</p>
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
            .Where(u => u.IsActive && u.IsVerified)
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
            // build a comma‑separated list of parameters
            var idParams = string.Join(", ", tOfferIds);

            // your raw SQL—no CTE, just a plain GROUP BY
            var sql = $@"
                        SELECT 
                            OfferId, 
                            COUNT(*)           AS Count, 
                            AVG(CAST(Rating AS float)) AS Avg 
                          FROM TblReviews 
                         WHERE OfferId IN ({idParams})
                         GROUP BY OfferId;
                    ";

            var aggregates = await _dbcontext
                .Set<ReviewAggregate>()              // a “keyless” DbSet<ReviewAggregate> you configure in your DbContext
                .FromSqlRaw(sql)
                .AsNoTracking()
                .ToListAsync();

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
                .Where(u => u.IsVerified && u.IsActive)
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
            .Where(u => u.IsVerified && u.IsActive)
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
        return View(new HomePageVM());
    }

    public async Task<IActionResult> Contact()
    {
        return View(new HomePageVM());
    }

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
                IsVerified = true,
                IsActive = true
            };
            await _userService.RegisterUserAsync(user, Guid.NewGuid().ToString("N"));
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
    public async Task<IActionResult> Login(UserLoginVM model, string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Validate user credentials.
            var user = await _userService.ValidateUserCredentialsAsync(model.LoginName, model.Password);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Invalid username or password.";
                return View(model);
            }

            // Force a fresh DB fetch to ensure updated user data.
            user = await _userService.GetUserByIdAsync(user.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Unable to retrieve user data.";
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
            if (!user.IsVerified)
            {
                TempData["ApprovalMessage"] = "🔎 Your account is not yet approved.";
                return View(model);
            }

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

        try
        {
            var emailOtp = HttpContext.Session.GetString("LoginEmailOtp");
            bool isTotp = TotpHelper.VerifyTotpCode(loginUser.TotpSecret, otp);
            bool isEmailOk = emailOtp != null && emailOtp == otp;

            if (!isTotp && !isEmailOk)
            {
                TempData["ErrorMessage"] = "❌ Invalid code. Try your app or check your email.";
                return RedirectToAction("LoginOtp", new { returnUrl });
            }

            //// Verify the OTP.
            //if (!TotpHelper.VerifyTotpCode(loginUser.TotpSecret, otp))
            //{
            //    TempData["ErrorMessage"] = "❌ Invalid OTP. Try again.";
            //    return RedirectToAction("LoginOtp", new { returnUrl });
            //}

            var expires = DateTimeOffset.Parse(HttpContext.Session.GetString("LoginEmailOtp_Expires"));
            if (DateTimeOffset.UtcNow > expires)
            {
                TempData["ErrorMessage"] = "❌ That code has expired. Please request a new one.";
                return RedirectToAction(nameof(LoginOtp));
            }

            // clear it so it can't be replayed
            HttpContext.Session.Remove("LoginEmailOtp");

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
            bool isTotpValid = TotpHelper.VerifyTotpCode(tempUser.TotpSecret, otp);
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
        if (string.IsNullOrWhiteSpace(email) || !ModelState.IsValidEmail(nameof(email)))
        {
            TempData["Error"] = "Please enter a valid email address.";
            return View();
        }

        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            await _passwordReset.SendResetLinkAsync(email, origin);
            TempData["Info"] = "If that email is in our system, you will receive a password reset link shortly.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred. Please try again later.";
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(string token)
    {
        if (string.IsNullOrEmpty(token))
            return RedirectToAction(nameof(ForgotPassword));

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
}