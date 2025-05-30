using Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.HelperClass;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.AdminControl;
using SkillSwap_Platform.Services.Email;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator, Support Agent")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminUsersController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public AdminUsersController(SkillSwapDbContext db, IEmailService email, IWebHostEnvironment env)
        {
            _db = db;
            _emailService = email;
            _env = env;
        }

        // GET: /AdminUsers
        public async Task<IActionResult> Index(int page = 1)
        {
            const int PageSize = 10;

            var allowedRoleIds = await _db.TblRoles
                .Where(r => r.RoleName == "Admin"
                         || r.RoleName == "Moderator"
                         || r.RoleName == "SupportAgent")
                .Select(r => r.RoleId)
                .ToListAsync();

            var allUserIds = new List<int>();
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            using (var cmd = conn.CreateCommand())
            {
                // build a parameterized IN clause
                var inParams = allowedRoleIds
                    .Select((id, i) => $"@p{i}")
                    .ToArray();

                cmd.CommandText = $@"
                    SELECT DISTINCT UserId
                      FROM tblUserRoles
                     WHERE RoleId IN ({string.Join(",", inParams)})
                ";

                for (int i = 0; i < allowedRoleIds.Count; i++)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = $"@p{i}";
                    p.Value = allowedRoleIds[i];
                    cmd.Parameters.Add(p);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    allUserIds.Add(reader.GetInt32(0));
                }
            }

            var totalCount = allUserIds.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            var pageUserIds = allUserIds
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            var userDict = new Dictionary<int, AdminUserListVM>();

            foreach (var id in pageUserIds)
            {
                var u = await _db.TblUsers
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x => x.UserId == id)
                    .Select(x => new AdminUserListVM
                    {
                        UserId = x.UserId,
                        Email = x.Email,
                        FirstName = x.FirstName,
                        LastName = x.LastName,
                        Roles = x.TblUserRoles.Select(ur => ur.Role.RoleName).ToList(),
                        CreatedDate = x.CreatedDate,
                        IsActive = x.IsActive,
                        IsHeld = x.IsHeld
                    })
                    .FirstAsync();

                userDict[id] = u;
            }

            var ordered = pageUserIds
                .Select(id => userDict[id])
                .ToList();

            var vm = new AdminUserIndexVM
            {
                Users = ordered,
                PageIndex = page,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // POST: /AdminUsers/Hold/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold(int id)
        {
            var user = await _db.TblUsers.FindAsync(id);
            if (user != null)
            {
                user.IsHeld = true;
                await _db.SaveChangesAsync();

                // 1) Build the hold‐notification email
                await _emailService.SendEmailAsync(
    to: user.Email,
    subject: "Your Swapo account has been put on hold",
    body: $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <tr><td style=""background:#00A88F;color:#fff;padding:15px;text-align:center;font-size:20px;"">
        Swapo
      </td></tr>
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <p>Hi {user.FirstName},</p>
        <p>Your Swapo account (Email: {user.UserName}) has just been <strong>placed on hold</strong> by an administrator.</p>
        <p><strong>When:</strong> {DateTime.UtcNow.ToLocalTime():dd MMM yyyy hh:mm tt} IST<br/>
           <strong>By:</strong> {User.Identity.Name}</p>
        <p>If you believe this was done in error or have questions, please contact 
           <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>
      </td></tr>
      <tr><td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
        Thank you for being part of Swapo! — The Swapo Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>",
    isBodyHtml: true
);
                TempData["SuccessMessage"] = $"User {user.Email} is now held.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminUsers/Unhold/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Unhold(int id)
        {
            var user = await _db.TblUsers.FindAsync(id);
            if (user != null)
            {
                user.IsHeld = false;
                await _db.SaveChangesAsync();

                await _emailService.SendEmailAsync(
    to: user.Email,
    subject: "Your Swapo account hold has been lifted",
    body: $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <tr><td style=""background:#00A88F;color:#fff;padding:15px;text-align:center;font-size:20px;"">
        Swapo
      </td></tr>
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <p>Hi {user.FirstName},</p>
        <p>Your Swapo account (Email: {user.UserName}) has been <strong>released from hold</strong>.</p>
        <p><strong>When:</strong> {DateTime.UtcNow.ToLocalTime():dd MMM yyyy hh:mm tt} IST<br/>
           <strong>By:</strong> {User.Identity.Name}</p>
        <p>You may now log in as usual. If you have any issues, contact 
           <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>
      </td></tr>
      <tr><td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
        Thank you for being part of Swapo! — The Swapo Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>",
    isBodyHtml: true
);

                TempData["SuccessMessage"] = $"User {user.Email} has been released from hold.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminUsers/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.TblUsers.FindAsync(id);
            if (user != null)
            {
                _db.TblUsers.Remove(user);
                await _db.SaveChangesAsync();

                var email = user.Email;
                var firstName = user.FirstName;
                var userId = user.UserId;

                _db.TblUsers.Remove(user);
                await _db.SaveChangesAsync();

                await _emailService.SendEmailAsync(
    to: email,
    subject: "Your Swapo account has been deleted",
    body: $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <tr><td style=""background:#00A88F;color:#fff;padding:15px;text-align:center;font-size:20px;"">
        Swapo
      </td></tr>
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <p>Hi {firstName},</p>
        <p>Your Swapo account (Email: {user.UserName}) has been <strong>permanently deleted</strong> by an administrator.</p>
        <p><strong>When:</strong> {DateTime.UtcNow.ToLocalTime():dd MMM yyyy hh:mm tt} IST<br/>
           <strong>By:</strong> {User.Identity.Name}</p>
        <p>If you need to reactivate or have questions, please reach out within 30 days at 
           <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>
      </td></tr>
      <tr><td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
        Thank you for being part of Swapo! — The Swapo Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>",
    isBodyHtml: true
);
                TempData["SuccessMessage"] = $"User {user.Email} has been deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminUsers/Register
        public IActionResult Register()
        {
            var vm = new RegisterAdminVM
            {
                AvailableRoles = _db.TblRoles
                        .Select(r => new SelectListItem
                        {
                            Value = r.RoleId.ToString(),
                            Text = r.RoleName
                        })
                        .ToList()
            };
            return View(vm);
        }

        // POST: /AdminUsers/Register
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterAdminVM vm)
        {
            if (vm.ProfileImage != null)
            {
                // 1 MB = 1_048_576 bytes
                if (vm.ProfileImage.Length > 1_048_576)
                {
                    ModelState.AddModelError(
                        nameof(vm.ProfileImage),
                        "Profile picture must be 1 MB or smaller.");
                }

                var ext = Path.GetExtension(vm.ProfileImage.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                {
                    ModelState.AddModelError(
                        nameof(vm.ProfileImage),
                        "Allowed file types: .jpg, .jpeg, .png");
                }
            }

            if (string.IsNullOrWhiteSpace(vm.ContactNo)
                || !Regex.IsMatch(vm.ContactNo, @"^\d{10}$"))
            {
                ModelState.AddModelError(
                    nameof(vm.ContactNo),
                    "Contact number is required and must be exactly 10 digits.");
            }

            if (!vm.IsVerified)
                ModelState.AddModelError(nameof(vm.IsVerified), "Please specify if the user is verified.”");

            if (!vm.IsActive)
                ModelState.AddModelError(nameof(vm.IsVerified), "Please specify if the user is active.”");

            if (!vm.IsHeld)
                ModelState.AddModelError(nameof(vm.IsVerified), "Please specify if the user is held.”");

            if (!vm.IsOnboardingCompleted)
                ModelState.AddModelError(nameof(vm.IsVerified), "Please specify if onboarding is completed.”");

            if (!ModelState.IsValid)
            {
                vm.AvailableRoles = _db.TblRoles.Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                }).ToList();
                return View(vm);
            }

            // 1. Check email uniqueness
            if (_db.TblUsers.Any(u => u.Email == vm.Email))
            {
                ModelState.AddModelError(nameof(vm.Email), "Email already in use.");
                vm.AvailableRoles = _db.TblRoles.Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                }).ToList();
                return View(vm);
            }

            // 2. Create salt+hash
            var salt = PasswordHelper.GenerateSalt();
            var hash = PasswordHelper.HashPassword(vm.Password, salt);

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // 3. Build User entity
            var user = new TblUser
            {
                Email = vm.Email,
                EmailConfirmed = false,               // not confirmed yet
                EmailChangeOtp = otp,                 // store code
                EmailChangeExpires = DateTime.UtcNow.AddMinutes(15),  // expiry
                UserName = vm.Email,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                Salt = salt,
                PasswordHash = hash,
                ContactNo = vm.ContactNo,
                CreatedDate = DateTime.UtcNow,
                IsVerified = vm.IsVerified,
                IsActive = vm.IsActive,
                IsHeld = vm.IsHeld,
                TotalHolds = vm.IsHeld ? 1 : 0,
                IsOnboardingCompleted = vm.IsOnboardingCompleted,
                SecurityStamp = Guid.NewGuid().ToString(),
                DigitalTokenBalance = 0,
                IsEscrowAccount = false,
                IsSupportAgent = false,
                IsFlagged = false,
                Role = vm.SelectedRoleId.ToString(),
            };

            if (string.IsNullOrEmpty(user.TotpSecret))
            {
                user.TotpSecret = TotpHelper.GenerateSecretKey();
            }

            if (vm.ProfileImage != null)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(vm.ProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await vm.ProfileImage.CopyToAsync(stream);

                // store the relative URL
                user.ProfileImageUrl = $"/uploads/profiles/{fileName}";
            }

            // 4. Save
            _db.TblUsers.Add(user);
            await _db.SaveChangesAsync();

            // 4. Assign role via the junction table
            var userRole = new Skill_Swap.Models.TblUserRole
            {
                UserId = user.UserId,
                RoleId = vm.SelectedRoleId
            };
            _db.TblUserRoles.Add(userRole);
            await _db.SaveChangesAsync();

            // 1) Look up the friendly role name
            var roleName = _db.TblRoles
                .AsNoTracking()
                .Where(r => r.RoleId == vm.SelectedRoleId)
                .Select(r => r.RoleName)
                .FirstOrDefault() ?? "User";

            // 2) Build and send the Super-Admin acknowledgement
            var loginUrl = Url.Action("Login", "Home", null, Request.Scheme);
            await _emailService.SendEmailAsync(
    to: vm.Email,
    subject: "Your new Swapo account has been created",
    body: $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <tr><td style=""background:#00A88F;color:#fff;padding:15px;text-align:center;font-size:20px;"">
        Swapo
      </td></tr>
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <p>Hi {vm.FirstName},</p>
        <p>Your account on <strong>Swapo</strong> has just been created by our Super-Admin team.</p>
        <p>
          <strong>Role:</strong> {roleName}<br/>
          <strong>Login URL:</strong> <a href=""{loginUrl}"">{loginUrl}</a><br/>
          <strong>Email:</strong> {vm.Email}<br/>
          <strong>Temporary Password:</strong> {vm.Password}
        </p>
        <hr/>
        <p style=""color:#c00;"">
          ⚠️ <strong>Do not share</strong> these credentials with anyone.
        </p>
        <p>Please log in immediately and change your password. If you suspect any unauthorized use, contact 
           <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.
        </p>
      </td></tr>
      <tr><td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
        Thank you for being part of Swapo! — The Swapo Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>",
    isBodyHtml: true
);

            TempData["SuccessMessage"] = $"‘{vm.Email}’ registered and OTP sent.";

            return RedirectToAction(nameof(RegisterConfirm), new { email = vm.Email });
        }

        // GET: /AdminUsers/RegisterConfirm?email=foo@bar.com
        [HttpGet]
        public IActionResult RegisterConfirm(string email)
        {
            var user = _db.TblUsers
                          .Where(u => u.Email == email)
                          .Select(u => new { u.Email, u.EmailChangeExpires })
                          .FirstOrDefault();
            if (user == null)
                return RedirectToAction(nameof(Index));

            var vm = new RegisterConfirmVM
            {
                ExpiresAt = user.EmailChangeExpires ?? DateTime.UtcNow
            };
            return View(vm);
        }

        // POST: /AdminUsers/RegisterConfirm
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterConfirm(RegisterConfirmVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Otp))
            {
                ModelState.AddModelError(
                    nameof(vm.Otp),
                    "Please enter the verification code sent to the user’s email.");
            }

            if (!ModelState.IsValid)
            {
                // We need ExpiresAt again so the view can display it.
                var user1 = _db.TblUsers.FirstOrDefault(u => u.Email == vm.Email);
                vm.ExpiresAt = user1?.EmailChangeExpires ?? DateTime.UtcNow;
                return View(vm);
            }

            var user = _db.TblUsers.SingleOrDefault(u => u.Email == vm.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Unknown email address.");
                vm.ExpiresAt = DateTime.UtcNow;
                return View(vm);
            }

            // Check expiry
            if (user.EmailChangeExpires == null || user.EmailChangeExpires < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty,
                    "That code has expired. Please register the user again to resend a new code.");
                vm.ExpiresAt = user.EmailChangeExpires ?? DateTime.UtcNow;
                return View(vm);
            }

            // Check OTP
            if (user.EmailChangeOtp != vm.Otp)
            {
                ModelState.AddModelError(nameof(vm.Otp), "The code you entered is invalid.");
                vm.ExpiresAt = user.EmailChangeExpires.Value;
                return View(vm);
            }

            // Mark confirmed
            user.EmailConfirmed = true;
            user.EmailChangeOtp = null;
            user.EmailChangeExpires = null;
            _db.TblUsers.Update(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"‘{vm.Email}’ has been confirmed and is now active.";
            return RedirectToAction(nameof(Index));
        }
    }
}
