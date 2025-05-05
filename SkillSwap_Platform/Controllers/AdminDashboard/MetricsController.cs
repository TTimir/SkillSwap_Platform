using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.PlatformMetrics;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
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
            var model = await _performanceService.GetCurrentMetricsAsync();
            return View(model);
        }
    }
}
