using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public async Task<IActionResult> PublicProfileList(
            string keyword, string location, string designation, string freelanceType,
            string category, string interactionMode, string skill, string filterLanguage, 
            int page = 1, int pageSize = 20)
        {
            try
            {
                // Fetch active and non-deleted users.
                var usersQuery = _context.TblUsers
                    .Where(u => u.IsVerified);

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

                if (!string.IsNullOrWhiteSpace(designation))
                {
                    usersQuery = usersQuery.Where(u => u.Designation == designation);
                }
                if (!string.IsNullOrWhiteSpace(skill))
                {
                    // If you store skills in a related table, filter through that relationship.
                    usersQuery = usersQuery.Where(u => u.TblUserSkills.Any(us => us.Skill.SkillName == skill));
                }
                if (!string.IsNullOrWhiteSpace(filterLanguage))
                {
                    usersQuery = usersQuery.Where(u => u.Languages.Contains(filterLanguage));
                }

                // ----------------------------------------------------
                // Filter by Primary Category from TblOffers (derived filter)
                // ----------------------------------------------------
                if (!string.IsNullOrWhiteSpace(category))
                {
                    // Group offers by user and category, then count occurrences.
                    var userOfferCategoryQuery =
                        from o in _context.TblOffers
                        group o by new { o.UserId, o.Category } into g
                        select new
                        {
                            g.Key.UserId,
                            Category = g.Key.Category,
                            Count = g.Count()
                        };

                    // For each user, select the category with the maximum count.
                    var primaryCategories =
                        from u in usersQuery
                        join oc in userOfferCategoryQuery on u.UserId equals oc.UserId
                        group oc by oc.UserId into grp
                        let primary = grp.OrderByDescending(x => x.Count).FirstOrDefault()
                        select new
                        {
                            UserId = grp.Key,
                            PrimaryCategory = primary.Category
                        };

                    // Filter users based on the primary category.
                    usersQuery = from u in usersQuery
                                     join pc in primaryCategories on u.UserId equals pc.UserId
                                     where pc.PrimaryCategory == category
                                     select u;
                }

                // Filter by primary freelance type
                // ================================
                if (!string.IsNullOrWhiteSpace(freelanceType))
                {
                    // Group freelance types by user and count the occurrences
                    var userFreelanceTypeQuery =
                        from uft in _context.TblOffers
                        group uft by new { uft.UserId, uft.FreelanceType } into g
                        select new
                        {
                            g.Key.UserId,
                            FreelanceType = g.Key.FreelanceType,
                            Count = g.Count()
                        };

                    // For each user, select the freelance type with the maximum count.
                    var primaryFreelanceTypes =
                        from u in usersQuery
                        join uft in userFreelanceTypeQuery on u.UserId equals uft.UserId
                        group uft by u.UserId into grp
                        let primary = grp.OrderByDescending(x => x.Count).FirstOrDefault()
                        select new
                        {
                            UserId = grp.Key,
                            PrimaryFreelanceType = primary.FreelanceType
                        };

                    // Filter users based on the primary freelance type
                    usersQuery = from u in usersQuery
                                 join pft in primaryFreelanceTypes on u.UserId equals pft.UserId
                                     where pft.PrimaryFreelanceType == freelanceType
                                     select u;
                }

                // ================================
                // Filter by primary interaction mode
                // ================================
                if (!string.IsNullOrWhiteSpace(interactionMode))
                {
                    // Group interaction modes by user and count occurrences
                    var userInteractionModeQuery =
                        from uim in _context.TblOffers
                        group uim by new { uim.UserId, uim.CollaborationMethod } into g
                        select new
                        {
                            g.Key.UserId,
                            InteractionMode = g.Key.CollaborationMethod,
                            Count = g.Count()
                        };

                    // For each user, select the interaction mode with the maximum count.
                    var primaryInteractionModes =
                        from u in usersQuery
                        join uim in userInteractionModeQuery on u.UserId equals uim.UserId
                        group uim by u.UserId into grp
                        let primary = grp.OrderByDescending(x => x.Count).FirstOrDefault()
                        select new
                        {
                            UserId = grp.Key,
                            PrimaryInteractionMode = primary.InteractionMode
                        };

                    // Filter users based on the primary interaction mode
                    usersQuery = from u in usersQuery
                                     join pim in primaryInteractionModes on u.UserId equals pim.UserId
                                     where pim.PrimaryInteractionMode == interactionMode
                                     select u;
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
                    Designation = u.Designation,
                    City = u.City,
                    Country = u.Country,
                    ProfileImage = string.IsNullOrEmpty(u.ProfileImageUrl)
                        ? "/template_assets/images/No_Profile_img.png"
                        : u.ProfileImageUrl,
                    OfferedSkillAreas = u.TblUserSkills != null
                        ? u.TblUserSkills
                            .Where(us => us.IsOffering && us.Skill != null && !string.IsNullOrEmpty(us.Skill.SkillName))
                            .OrderByDescending(us => us.ProficiencyLevel)
                            .Take(3)
                            .Select(us => us.Skill.SkillName)
                            .ToList()
                        : new List<string>(),
                    Recommendation = u.RecommendedPercentage ?? 0.0,
                    JobSuccessRate = u.JobSuccessRate ?? 0.0,
                }).ToList();

                var vm = new UserProfileListVM
                {
                    Profiles = profileCards,
                    Keyword = keyword,
                    Location = location,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize),
                    DesignationOptions = await _context.TblUsers
                        .Select(u => u.Designation)
                        .Distinct()
                        .OrderBy(d => d)
                        .Select(d => new SelectListItem { Text = d, Value = d })
                        .ToListAsync(),
                    CategoryOptions = await _context.TblOffers
                        .Select(o => o.Category)
                        .Distinct()
                        .OrderBy(c => c)
                        .Select(c => new SelectListItem { Text = c, Value = c })
                        .ToListAsync(),
                    FreelanceTypeOptions = await _context.TblOffers
                        .Select(uft => uft.FreelanceType)
                        .Distinct()
                        .OrderBy(ft => ft)
                        .Select(ft => new SelectListItem { Text = ft, Value = ft })
                        .ToListAsync(),
                    InteractionModeOptions = await _context.TblOffers
                        .Select(uim => uim.CollaborationMethod)
                        .Distinct()
                        .OrderBy(im => im)
                        .Select(im => new SelectListItem { Text = im, Value = im })
                        .ToListAsync(),
                    SkillOptions = await _context.TblUserSkills
                        .Where(us => us.Skill != null)
                        .Select(us => new SelectListItem
                        {
                            Text = us.Skill.SkillName,
                            Value = us.Skill.SkillId.ToString()
                        })
                        .Distinct()
                        .ToListAsync(),
                    LanguageOptions = await _context.TblLanguages
                        .Select(l => l.Language)
                        .Distinct()
                        .OrderBy(lang => lang)
                        .Select(lang => new SelectListItem { Text = lang, Value = lang })
                        .ToListAsync()
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