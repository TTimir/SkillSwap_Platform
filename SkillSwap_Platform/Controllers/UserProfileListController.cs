using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.FreelancersVM;

namespace SkillSwap_Platform.Controllers
{
    [AllowAnonymous]
    public class UserProfileListController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfileListController> _logger;

        public UserProfileListController(SkillSwapDbContext context, ILogger<UserProfileListController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Public Profile List
        // GET: /UserProfileList/PublicProfileList
        [HttpGet]
        public async Task<IActionResult> PublicProfileList(string keyword, string location, int page = 1, int pageSize = 20)
        {
            try
            {
                // Fetch active and non-deleted users.
                var usersQuery = _context.TblUsers
                    .Where(u => u.IsActive);

                // Apply keyword filter on username, first or last name.
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    usersQuery = usersQuery.Where(u =>
                        u.UserName.Contains(keyword) ||
                        u.FirstName.Contains(keyword) ||
                        u.LastName.Contains(keyword));
                }

                // Apply location filter on city or country.
                if (!string.IsNullOrWhiteSpace(location))
                {
                    usersQuery = usersQuery.Where(u =>
                        u.City.Contains(location) ||
                        u.Country.Contains(location));
                }

                int totalUsers = await usersQuery.CountAsync();

                var users = await usersQuery
                    .Include(u => u.TblUserSkills).ThenInclude(us => us.Skill)
                    .OrderByDescending(u => u.LastActive) // You might sort by LastActive or CreatedDate
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to your profile view model
                var profileCards = users.Select(u => new ProfileCardVM
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    City = u.City,
                    Country = u.Country,
                    ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                        ? "/template_assets/images/No_Profile_img.png"
                        : u.ProfileImageUrl,
                    OfferedSkillAreas = u.TblUserSkills != null
                        ? u.TblUserSkills
                            .Where(us => us.IsOffering)
                            .OrderByDescending(us => us.ProficiencyLevel)
                            .Take(3)
                            .Select(us => us.Skill.SkillName)
                            .ToList()
                        : new List<string>(),
                    Recommendation = u.RecommendedPercentage,
                    JobSuccessRate = u.JobSuccessRate,
                }).ToList();

                var vm = new UserProfileListVM
                {
                    Profiles = profileCards,
                    Keyword = keyword,
                    Location = location,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize)
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public profile list");
                TempData["ErrorMessage"] = "An error occurred while loading profiles.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion
    }
}