﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.Escrow;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class EscrowController : Controller
    {
        private readonly IEscrowService _escrowService;

        public EscrowController(IEscrowService escrowService)
        {
            _escrowService = escrowService;
        }

        // GET: /Admin/Escrow?page=1&pageSize=20
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var model = await _escrowService.GetAllAsync(page, pageSize);

            // flag escrows created in the last 15 minutes
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var newIds = model.Items
                              .Where(e => e.CreatedAt >= cutoff)
                              .Select(e => e.EscrowId)
                              .ToList();
            ViewBag.NewEscrowIds = newIds;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            var model = await _escrowService.GetHistoryAsync(page, pageSize);
            return View(model);
        }

        // GET: /Admin/Escrow/Details/123
        public async Task<IActionResult> Details(int id)
        {
            var escrow = await _escrowService.GetByIdAsync(id);
            if (escrow == null) return NotFound();
            return View(escrow);
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(int page = 1, int pageSize = 20)
        {
            var vm = await _escrowService.GetDashboardAsync(page, pageSize);
            return View(vm);
        }

        [HttpPost("Release/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int id, string notes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _escrowService.ReleaseAsync(id, adminId, notes);
            TempData["Success"] = "Escrow released.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("Refund/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refund(int id, string notes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _escrowService.RefundAsync(id, adminId, notes);
            TempData["Success"] = "Escrow refunded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("Dispute/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispute(int id, string notes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _escrowService.DisputeAsync(id, adminId, notes);
            TempData["Success"] = "Escrow disputed.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}