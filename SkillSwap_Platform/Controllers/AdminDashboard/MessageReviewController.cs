using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.Message;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class MessageReviewController : Controller
    {
        private readonly IMessageService _mod;

        public MessageReviewController(IMessageService mod)
            => _mod = mod;

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            try
            {
                var held = await _mod.GetHeldMessagesAsync(page, pageSize);
                return View(held);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load held messages.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // 1) Summary of flagged users
        [HttpGet]
        public async Task<IActionResult> Summary(int page = 1, int pageSize = 20)
        {
            try
            {
                var vm = await _mod.GetFlaggedUserSummariesAsync(page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load held messages Summary.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // 2) Logs for a specific sender
        [HttpGet]
        public async Task<IActionResult> Logs(int senderUserId, int page = 1, int pageSize = 20)
        {
            try
            {
                ViewBag.SenderUserId = senderUserId;
                ViewBag.SenderUserName = (await _mod
                    .GetFlaggedUserSummariesAsync(1, int.MaxValue))   // quick lookup
                    .Items
                    .FirstOrDefault(u => u.SenderUserId == senderUserId)?
                    .SenderUserName
                    ?? "Unknown";

                var vm = await _mod.GetHeldMessagesBySenderAsync(senderUserId, page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load logs";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                await _mod.ApproveMessageAsync(id, adminId);
                TempData["Success"] = "Message approved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not approve message.";
                return RedirectToAction("EP500", "EP");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id)
        {
            try
            {
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                await _mod.DismissMessageAsync(id, adminId);
                TempData["Success"] = "Message dismissed and warned.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not dismiss message.";
                return RedirectToAction("EP500", "EP");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}