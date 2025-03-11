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
    public IActionResult Index()
    {
        try
        {
            ViewBag.SuccessMessage = TempData.ContainsKey("SuccessMessage") ? TempData["SuccessMessage"] : null;
            ViewBag.ErrorMessage = TempData.ContainsKey("ErrorMessage") ? TempData["ErrorMessage"] : null;
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = "An unexpected error occurred.";
        }

        return View();
    }

    #region Register

    // GET: /Home/Register
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
            var existingUser = await _userService.GetUserByUserNameOrEmailAsync(model.UserName);
            if (existingUser != null)
            {
                TempData["ErrorMessage"] = "❌ Username or email is already taken.";
                return View(model);
            }

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

            HttpContext.Session.SetInt32("TempUserId", tempUser.UserId);  // Store temporary user ID
            HttpContext.Session.SetObjectAsJson("TempUser", tempUser);
            HttpContext.Session.SetString("TempUser_Password", model.Password);

            return RedirectToAction("Setup2FA"); // ✅ Redirect to Setup2FA
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred during registration.";
            return View(model);
        }
    }

    #endregion

    #region Login

    // GET: /Home/Login
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
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await _userService.ValidateUserCredentialsAsync(model.LoginName, model.Password);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Invalid username or password.";
                return View(model);
            }

            // ✅ Force fresh fetch from database
            user = await _userService.GetUserByIdAsync(user.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Unable to retrieve user data.";
                return View(model);
            }

            if (user.FailedOtpAttempts >= 5 && user.LockoutEndTime.HasValue && user.LockoutEndTime > DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "🚫 Too many failed attempts. Try again later.";
                return View(model);
            }


            // 🚀 CHECK IF USER COMPLETED ONBOARDING
            if (!user.IsOnboardingCompleted)
            {
                HttpContext.Session.SetInt32("TempUserId", user.UserId); // 🔥 Store UserId for onboarding
                return RedirectToAction("SelectRole", "Onboarding"); // 🚀 Redirect to first onboarding step
            }

            if (!user.IsVerified)
            {
                TempData["ApprovalMessage"] = "🔎 Your account is not yet approved.";
                return View(model);
            }

            // If TOTP is enabled (i.e. user has a TotpSecret), redirect to OTP verification step.
            if (!string.IsNullOrEmpty(user.TotpSecret))
            {
                // Store user info in session for OTP verification at login
                HttpContext.Session.SetObjectAsJson("LoginUser", user);
                HttpContext.Session.SetString("LoginUser_Password", model.Password);
                TempData["InfoMessage"] = "Please enter your OTP.";
                return RedirectToAction("LoginOtp", new { returnUrl });
            }
            else
            {
                await SignInUserAsync(user, model.RememberMe);
                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction("Index");
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred while logging in.";
            return View(model);
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
            // Verify OTP using the stored Base32 TotpSecret with IST time
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
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An unexpected error occurred while verifying OTP.";
            return RedirectToAction("LoginOtp", new { returnUrl });
        }
    }

    #endregion

    #region Two-Factor Authentication (2FA)

    [HttpGet]
    public async Task<IActionResult> Setup2FA(string email)
    {
        // ✅ Retrieve user from session instead of DB
        var tempUser = HttpContext.Session.GetObjectFromJson<TblUser>("TempUser");

        if (tempUser == null)
        {
            TempData["ErrorMessage"] = "⚠️ User session expired. Please register again.";
            return RedirectToAction("Register");
        }

        // ✅ Generate QR Code URL
        string qrCodeUrl = TotpHelper.GenerateQrCodeUrl(tempUser.TotpSecret, tempUser.Email);
        ViewBag.QrCodeUrl = qrCodeUrl;
        return View(tempUser); // ✅ Ensure model is passed to the view
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

            // ✅ Verify directly using stored Base32 secret (no decryption needed)
            if (!TotpHelper.VerifyTotpCode(tempUser.TotpSecret, otp))
            {
                TempData["ErrorMessage"] = "❌ Invalid OTP. Try again.";
                return View("Setup2FA");
            }

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
            Response.Cookies.Delete(".AspNetCore.SkillSwapAuth");
            TempData["SuccessMessage"] = "✅ Successfully logged out.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "⚠️ An error occurred during logout. Please try again.";
        }
        return RedirectToAction("Login", "Home");
    }
    #endregion

    /// <summary>
    /// Encapsulates the sign-in process by creating a claims identity and issuing an authentication cookie.
    /// This method is used by both Register and Login actions.
    /// </summary>
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

            var roles = await _userService.GetUserRolesAsync(user.UserId);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var claimsIdentity = new ClaimsIdentity(claims, "SkillSwapAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("SkillSwapAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Sign-in failed: {ex.Message}");
        }
    }
    #endregion
}