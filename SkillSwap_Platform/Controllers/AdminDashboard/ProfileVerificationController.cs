using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using SkillSwap_Platform.Services.ProfileVerification;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class ProfileVerificationController : Controller
    {
        private readonly IVerificationService _svc;
        private readonly ILogger<ProfileVerificationController> _log;

        public ProfileVerificationController(
            IVerificationService svc,
            ILogger<ProfileVerificationController> log)
        {
            _svc = svc;
            _log = log;
        }

        // GET: /Admin/VerificationAdmin/Pending
        public async Task<IActionResult> Pending(int page = 1)
        {
            const int pageSize = 10;

            // 1) fetch all pending
            var all = await _svc.GetPendingAsync();    // IList<AdminListVm>

            var totalCount = all.Count;
            var skip = (page - 1) * pageSize;

            // 2) take just the page
            var items = all
                .OrderByDescending(r => r.SubmittedAt)  // optional: sort newest first
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            // 3) build paged VM
            var vm = new PagedVm<AdminListVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(vm);
        }

        // GET: /Admin/VerificationAdmin/Details/5
        public async Task<IActionResult> Details(long id)
        {
            try
            {
                var vm = await _svc.GetDetailsAsync(id);
                return View(vm);  // Views/Admin/VerificationAdmin/Details.cshtml
            }
            catch
            {
                return NotFound();
            }
        }

        // POST: /Admin/VerificationAdmin/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(long id, string comments)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await _svc.ApproveAsync(id, adminId, comments);
                TempData["Success"] = $"✅ Verification request #{id} approved.";
                return RedirectToAction(nameof(Pending));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Approve failed for request {RequestId}", id);
                return BadRequest();
            }
        }

        // POST: /Admin/VerificationAdmin/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(long id, string comments)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await _svc.RejectAsync(id, adminId, comments);
                TempData["Success"] = $"⚠️ Verification request #{id} rejected.";
                return RedirectToAction(nameof(Pending));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Reject failed for request {RequestId}", id);
                return BadRequest();
            }
        }

        // GET: /Admin/ProfileVerification/History/5
        [HttpGet]
        public async Task<IActionResult> History(string search, int page = 1)
        {
            const int pageSize = 10;

            // 1) get full history
            var history = await _svc.GetHistoryAsync();  // IList<HistoryItemVm>

            // 2) apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                history = history
                    .Where(h =>
                        h.Event.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        (h.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (h.Username?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (h.Comments?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    )
                    .ToList();
            }

            // 3) set up paging
            var totalCount = history.Count;
            var skip = (page - 1) * pageSize;
            var pagedItems = history
                .OrderByDescending(h => h.Timestamp)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            // 4) build PagedVm
            var vm = new PagedVm<HistoryItemVm>
            {
                Items = pagedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            // 5) keep the search term for the view
            ViewData["Search"] = search;

            return View("History", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(long id, string comments)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await _svc.RevokeAsync(id, adminId, comments);
                TempData["Success"] = $"🚫 Verification for request #{id} has been revoked.";
                return RedirectToAction(nameof(Pending));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Revoke failed for request {RequestId}", id);
                TempData["Error"] = "Something went wrong.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SubmitterLogs(string filter = "all", string search = null, int page = 1)
        {
            const int pageSize = 10;

            // 1) grab the full timeline
            var history = await _svc.GetHistoryAsync();

            // 2) keep only Approved/Rejected
            var filtered = history
                .Where(h => h.Event == "Approved" || h.Event == "Rejected")
                // optional: allow showing just one kind
                .Where(h =>
                    filter == "all" ||
                    (filter == "approved" && h.Event == "Approved") ||
                    (filter == "rejected" && h.Event == "Rejected")
                )
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            // 3) paging
            var total = filtered.Count;
            var items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4) build view-model
            var vm = new PagedVm<HistoryItemVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };

            ViewData["Filter"] = filter;
            return View(vm);
        }
    }
}
