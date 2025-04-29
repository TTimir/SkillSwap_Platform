using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Certificate;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class CertificateReviewController : Controller
    {
        private readonly ICertificateReviewService _service;
        private readonly ILogger<CertificateReviewController> _logger;
        private readonly SkillSwapDbContext _db;
        private const int PageSize = 10;

        public CertificateReviewController(
            ICertificateReviewService service,
            ILogger<CertificateReviewController> logger, SkillSwapDbContext db)
        {
            _service = service;
            _logger = logger;
            _db = db;
        }

        // GET: /Admin/CertificateReview
        public async Task<IActionResult> Index(int page = 1)
        {
            var model = await _service.GetPendingCertificatesAsync(page, PageSize);
            return View(model);
        }

        public async Task<IActionResult> Approved(int page = 1)
        {
            var model = await _service.GetApprovedCertificatesAsync(page, PageSize);
            return View(model);
        }

        public async Task<IActionResult> Rejected(int page = 1)
        {
            var model = await _service.GetRejectedCertificatesAsync(page, PageSize);
            return View(model);
        }

        // GET: /Admin/CertificateReview/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var cert = await _service.GetCertificateDetailAsync(id);
            if (cert == null) return NotFound();
            return View(cert);
        }


        // POST: /Admin/CertificateReview/Approve/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            int adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (await _service.ApproveCertificateAsync(id, adminId))
                TempData["Success"] = "Certificate approved.";
            else
                TempData["Error"] = "Failed to approve certificate.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/CertificateReview/Reject/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError("reason", "Rejection reason is required.");
                return RedirectToAction(nameof(Details), new { id });
            }

            int adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (await _service.RejectCertificateAsync(id, adminId, reason))
                TempData["Success"] = "Certificate rejected.";
            else
                TempData["Error"] = "Failed to reject certificate.";

            return RedirectToAction(nameof(Index));
        }
    }
}
