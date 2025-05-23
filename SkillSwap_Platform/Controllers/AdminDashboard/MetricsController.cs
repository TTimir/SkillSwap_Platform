﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.PlatformMetrics;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class MetricsController : Controller
    {
        private readonly IPerformanceService _performanceService;

        public MetricsController(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var model = await _performanceService.GetCurrentMetricsAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to load metrics right now.";
                return RedirectToAction("EP500", "EP");
            }
        }
    }
}
