using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator, Support Agent")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminDashboardController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminDashboardController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.UtcNow.AddMonths(-1);
            var end = to ?? DateTime.UtcNow;
            AdminDashboardMetricsDto model;

            try
            {
                model = await _dashboardService.GetAdminDashboardMetricsAsync(start, end);
            }
            catch (Exception ex)
            {
                // Log or handle as needed
                // For now, redirect to error page
                return RedirectToAction("Error", "Home");
            }

            ViewData["From"] = start.ToString("dd MMMM, yyyy");
            ViewData["To"] = end.ToString("dd MMMM, yyyy");
            return View(model);
        }
    }
}
