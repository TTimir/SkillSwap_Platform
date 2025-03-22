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

namespace SkillSwap_Platform.Controllers;

public class HomeController : Controller
{
    private readonly IUserServices _userService;
    private readonly SkillSwapDbContext _dbcontext;

    public HomeController(IUserServices userService, SkillSwapDbContext context)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _dbcontext = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var trendingOffers = await _dbcontext.TblOffers
                .Include(o => o.User)  // Eager load the related User
                .Where(o => o.IsActive)
                .OrderByDescending(o => o.JobSuccessRate)
                .ThenByDescending(o => o.TokenCost)
                .Take(8)
                .ToListAsync();

            var trendingOfferVMs = trendingOffers.Select(o => new OfferCardVM
            {
                OfferId = o.OfferId,
                Title = o.Title,
                ShortTitle = o.Title?.Length > 35 ? o.Title.Substring(0, 35) + "..." : o.Title,
                UserProfileImage = string.IsNullOrEmpty(o.User?.ProfileImageUrl)
                     ? "/template_assets/images/No_Profile_img.png"
                     : o.User.ProfileImageUrl,
                // Deserialize the portfolio JSON string into a List<string>.
                PortfolioImages = !string.IsNullOrWhiteSpace(o.Portfolio)
                 ? JsonConvert.DeserializeObject<List<string>>(o.Portfolio)
                 : new List<string>(),
                Category = o.Category,
                UserName = o.User?.UserName ?? "Unknown",
                TimeCommitmentDays = o.TimeCommitmentDays
            }).ToList();

            // Query highest rated freelancers (limit to 10)
            // Adjust the filtering criteria as needed (e.g. only active, and only freelancers)
            var highestRatedUsers = await _dbcontext.TblUsers
                .Include(u => u.TblReviewReviewees)
                .Include(u => u.TblUserSkills).ThenInclude(us => us.Skill)
                .Where(u => u.IsActive)
                .Take(10)
                .ToListAsync();

            var highestRatedFreelancersVM = highestRatedUsers.Select(u => new FreelancerCardVM
            {
                UserId = u.UserId,
                Name = u.UserName,
                ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                    ? "/template_assets/images/No_Profile_img.png"
                    : u.ProfileImageUrl,
                Location = u.Location,
                Rating = u.TblReviewReviewees != null && u.TblReviewReviewees.Any()
                    ? u.TblReviewReviewees.Average(r => r.Rating)
                    : 0,
                JobSuccess = u.JobSuccessRate,
                Skills = u.TblUserSkills.Select(us => us.Skill.SkillName).ToList(),
                Recommendation = u.RecommendedPercentage
            }).ToList();

            var vm = new HomePageVM
            {
                TrendingOffers = trendingOfferVMs,
                HighestRatedFreelancers = highestRatedFreelancersVM
            };

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

            // Redirect to 2FA setup.
            return RedirectToAction("Setup2FA");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred during registration.";
            Debug.WriteLine($"[Register Error] {ex.Message}");
            return RedirectToAction("EP500", "EP");
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
    public IActionResult LoginOtp(string returnUrl = null)
    {
        var loginUser = HttpContext.Session.GetObjectFromJson<TblUser>("LoginUser");
        if (loginUser == null)
        {
            TempData["ErrorMessage"] = "Session expired. Please log in again.";
            return RedirectToAction("Login");
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

        try
        {
            // Verify the OTP.
            if (!TotpHelper.VerifyTotpCode(loginUser.TotpSecret, otp))
            {
                TempData["ErrorMessage"] = "❌ Invalid OTP. Try again.";
                return RedirectToAction("LoginOtp", new { returnUrl });
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
            string password = HttpContext.Session.GetString("TempUser_Password");

            if (tempUser == null || tempUser.Email != email)
            {
                TempData["ErrorMessage"] = "⚠️ Session expired. Please register again.";
                return RedirectToAction("Register");
            }

            // Verify OTP.
            if (!TotpHelper.VerifyTotpCode(tempUser.TotpSecret, otp))
            {
                TempData["ErrorMessage"] = "❌ Invalid OTP. Try again.";
                return View("Setup2FA");
            }

            // Create and register the new user.
            var newUser = new TblUser
            {
                FirstName = tempUser.FirstName,
                LastName = tempUser.LastName,
                UserName = tempUser.UserName,
                Email = tempUser.Email,
                ContactNo = tempUser.ContactNo,
                TotpSecret = tempUser.TotpSecret, // ✅ Store plain Base32 secret
                IsOnboardingCompleted = false // 🚀 Ensuring user lands on onboarding
            };

            var result = await _userService.RegisterUserAsync(newUser, password);
            if (!result)
            {
                TempData["ErrorMessage"] = "⚠️ Registration failed. Please try again.";
                return View("Setup2FA");
            }

            HttpContext.Session.Clear();

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

    #region Helper Methods
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
    #endregion
}