using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM;
using SkillSwap_Platform.Services.ProfileVerification;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class VerificationController : Controller
    {
        private readonly IVerificationService _svc;
        private readonly ILogger<VerificationController> _log;

        public VerificationController(IVerificationService svc, ILogger<VerificationController> log)
        {
            _svc = svc;
            _log = log;
        }

        [HttpGet]
        public IActionResult Submit()
        {
            var vm = new SubmitRequestVm();
            // seed one blank block in each section so the view always has something to clone
            vm.Certificates.Add(new SubmitRequestVm.SkillCertificate());
            vm.EducationRecords.Add(new SubmitRequestVm.EducationRecord());
            vm.ExperienceRecords.Add(new SubmitRequestVm.ExperienceRecord());
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SubmitRequestVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                var userId = GetCurrentUserId();
                await _svc.SubmitAsync(userId.ToString(), vm);
                TempData["SuccessMessage"] = "We’ve received your details and will review them within 48 hours.";
                return RedirectToAction(nameof(ThankYou));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Submit failed for user {User}", User.Identity.Name);
                ModelState.AddModelError("", "An error occurred. Please try again later.");
                return View(vm);
            }
        }

        [HttpGet]
        public IActionResult ThankYou()
        {
            ViewData["Title"] = "Thank You";
            return View();
        }

        /// <summary>
        /// Retrieves the current user's ID from the claims.
        /// </summary>
        /// <returns>The user ID as an integer.</returns>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new Exception("User ID not found in claims.");
        }
    }
}
