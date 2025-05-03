using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using SkillSwap_Platform.Services.AdminControls.Offer_and_Review;
using System.Drawing.Printing;
using System.Security.Claims;

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
            var vm = await _offre.GetActiveFlagsAsync(page, pageSize);
            return View("ActiveFlags", vm);
        }

        // reviews
        public async Task<IActionResult> Reviews(int page = 1, int pageSize = 20)
        {
            // returns PagedResult<ReviewFlagVm>
            var vm = await _offre.GetFlaggedReviewsAsync(page, pageSize);
            return View(vm);
        }

        public async Task<IActionResult> Replies(int page = 1, int pageSize = 20)
        {
            var vm = await _offre.GetFlaggedReplyFlagsAsync(page, pageSize);
            ViewData["Mode"] = "Replies";
            return View("Reviews", vm);
        }

        public async Task<IActionResult> Details(int id, string mode)
        {
            if (mode == "Replies")
            {
                var vm = await _offre.GetReplyDetailsAsync(id);
                ViewData["Mode"] = "Replies";
                return View("Details", vm);
            }   
            else
            {
                var vm = await _offre.GetFlaggedReviewDetailsAsync(id);
                ViewData["Mode"] = "Reviews";
                return View("Details", vm);
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
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _offre.DismissReviewFlagsAsync(reviewId, flagId, adminId, notes);
            TempData["Success"] = "Flag dismissed successfully.";
            return RedirectToAction(nameof(Reviews));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReply(int replyId, string notes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _offre.DismissReplyFlagsAsync(replyId, adminId, notes);
            TempData["Success"] = "Reply-flag dismissed.";
            return RedirectToAction(nameof(Reviews));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Moderate(int id, string mode, string note, string warning)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            bool isReply = mode == "Replies";

            await _offre.ModerateAndWarnAsync(
                contentId: id,
                isReply: isReply,
                adminId: adminId,
                moderationNote: note,
                warningMessage: warning
            );

            TempData["Success"] = "Content removed and user warned.";
            return RedirectToAction("Reviews");
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            var vm = await _offre.GetFlagHistoryAsync(page, pageSize);
            ViewData["Mode"] = "History";
            return View(vm);
        }
    }
}
