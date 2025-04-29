using Microsoft.AspNetCore.Mvc;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    public class AdminDashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
