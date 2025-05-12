using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using SkillSwap_Platform.Models.ViewModels.ReviewReplyVm;
using SkillSwap_Platform.Services.AdminControls.Offer_and_Review;
using System.Drawing.Printing;
using System.Security.Claims;
using System.Transactions;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class OfferReviewController : Controller
    {
        private readonly IOfferReviewService _offre;
        public OfferReviewController(IOfferReviewService offre) => _offre = offre;

        [HttpGet]
        public async Task<IActionResult> ActiveFlags(int page = 1, int pageSize = 20)
        {
            ViewData["Mode"] = "Active";
            ViewData["Title"] = "Currently Flagged Content";
            ViewData["SubTitle"] = "All reviews and replies still awaiting moderation";
            try
            {
                var vm = await _offre.GetActiveFlagsAsync(page, pageSize);
                return View(nameof(ActiveFlags), vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load active flags right now.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // reviews
        public async Task<IActionResult> Reviews(int page = 1, int pageSize = 20)
        {
            try
            {
                // returns PagedResult<ReviewFlagVm>
                var vm = await _offre.GetFlaggedReviewsAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load flag reviews right now.";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> Replies(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _offre.GetFlaggedReplyFlagsAsync(page, pageSize);
                ViewData["Mode"] = "Replies";
                return View("Reviews", vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load flag reply right now.";
                return RedirectToAction("EP500", "EP");
            }
        }

        public async Task<IActionResult> Details(int id, string mode)
        {
            if (mode == "Replies")
            {
                try
                {
                    var vm = await _offre.GetReplyDetailsAsync(id);
                    ViewData["Mode"] = "Replies";
                    return View("Details", vm);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Unable to load flag reviews details right now.";
                    return RedirectToAction("EP500", "EP");

                }
            }
            else
            {
                try
                {
                    var vm = await _offre.GetFlaggedReviewDetailsAsync(id);
                    ViewData["Mode"] = "Reviews";
                    return View("Details", vm);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Unable to load flag reply details right now.";
                    return RedirectToAction("EP500", "EP");
                }
            }
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DismissOffer(int offerId, string notes)
        //{
        //    var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        //    await _offre.DismissOfferFlagsAsync(offerId, adminId, notes);
        //    TempData["Success"] = "All flags on that offer have been cleared.";
        //    return RedirectToAction(nameof(Details), new { offerId });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReview(int reviewId, int flagId, string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                ModelState.AddModelError(nameof(notes), "Reason is required.");
                return RedirectToAction(nameof(Details), new { id = reviewId, mode = "Reviews" });
            }

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdStr, out var adminId))
            {
                return Forbid();
            }

            using var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            try
            {
                await _offre.DismissReviewFlagsAsync(reviewId, flagId, adminId, notes);
                TempData["Success"] = "Flag dismissed successfully.";
                return RedirectToAction(nameof(Reviews));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to dismiss flag.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReply(int replyId, string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                ModelState.AddModelError(nameof(notes), "Reason is required.");
                return RedirectToAction(nameof(Details), new { id = replyId, mode = "Replies" });
            }

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdStr, out var adminId))
            {
                return Forbid();
            }

            using var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            try
            {
                await _offre.DismissReplyFlagsAsync(replyId, adminId, notes);
                TempData["Success"] = "Reply-flag dismissed.";
                return RedirectToAction(nameof(Reviews));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to dismiss flag.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Moderate(int id, string mode, string note, string warning)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                ModelState.AddModelError(nameof(note), "Reason is required.");
                return RedirectToAction(nameof(Moderate));
            }

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdStr, out var adminId))
            {
                return Forbid();
            }
            bool isReply = mode == "Replies";

            using var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            try
            {
                await _offre.ModerateAndWarnAsync(
                contentId: id,
                isReply: isReply,
                adminId: adminId,
                moderationNote: note,
                warningMessage: warning
            );

                TempData["Success"] = "Content removed and user warned.";
                return RedirectToAction(nameof(Reviews));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to perform action.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _offre.GetFlagHistoryAsync(page, pageSize);
                ViewData["Mode"] = "History";
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load history.";
                return RedirectToAction("EP500", "EP");
            }
        }
    }
}
