using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    public class UserDashboardController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfileController> _logger;

        public UserDashboardController(SkillSwapDbContext context, ILogger<UserProfileController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
