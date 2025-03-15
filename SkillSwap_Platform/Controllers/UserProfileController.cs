using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfileController> _logger;

        public UserProfileController(SkillSwapDbContext context, ILogger<UserProfileController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (User.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
                {
                    try
                    {
                        // Set the user profile image for the layout.
                        var user = _context.TblUsers.AsNoTracking().FirstOrDefault(u => u.UserId == userId);
                        ViewData["UserProfileImage"] = user?.ProfileImageUrl;

                        // Update the user's LastActive field.
                        var userToUpdate = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                        if (userToUpdate != null)
                        {
                            userToUpdate.LastActive = DateTime.UtcNow;
                            _context.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating LastActive for user {UserId}", userId);
                    }
                }
            }

            //int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            //if (userId != null)
            //{
            //    var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            //    ViewData["UserProfileImage"] = user?.ProfileImageUrl;
            //}
            //if (userId > 0)
            //{
            //    var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            //    if (user != null)
            //    {
            //        user.LastActive = DateTime.UtcNow; // ✅ Update LastActive
            //        _context.SaveChanges();
            //    }
            //}
        }
        public async Task<IActionResult> Index()
        {
            // Retrieve current user's ID from claims.
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            try
            {
                // Load the user and related data.
                var user = await _context.TblUsers
                .Include(u => u.TblEducations)
                .Include(u => u.TblExperiences)
                .Include(u => u.TblLanguages)
                .Include(u => u.TblUserCertificateUsers)
                .Include(u => u.TblUserSkills) // ✅ Include user skills
                    .ThenInclude(us => us.Skill) // ✅ Include related skills from TblSkills
                .Include(u => u.TblReviewReviewees) // ✅ Fetch reviews received by the user
                .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Fetch all available skills from tblSkills
                var allSkills = await _context.TblSkills.Select(s => s.SkillName).ToListAsync();

                // Extract offered skills stored in "OfferedSkillAreas" column (comma-separated)
                var offeredSkills = user.OfferedSkillAreas?
                        .Split(',')
                        .Select(s => s.Trim())
                        .ToList() ?? new List<string>();

                // Map skills to SkillVM objects
                var skillList = allSkills.Select(skill => new SkillVM
                {
                    Name = skill,
                    IsOffered = offeredSkills.Contains(skill, StringComparer.OrdinalIgnoreCase) // Case-insensitive match
                }).ToList();

                // Calculate the recommended percentage from reviews.
                double recommendedPercentage = 0;
                if (user.TblReviewReviewees.Any())
                {
                    int totalReviews = user.TblReviewReviewees.Count();
                    int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);
                    decimal? percentage = (decimal?)positiveReviews / totalReviews * 100;
                    recommendedPercentage = percentage.HasValue ? (double)percentage.Value : 0;
                }

                // Fetch the most recent completed exchange (job)
                var lastCompletedExchange = await _context.TblExchanges
                    .Where(e => e.RequesterId == userId || e.LastStatusChangedBy == userId) // Job done by the user
                    .Where(e => e.Status == "Completed") // Only completed transactions
                    .OrderByDescending(e => e.LastStatusChangeDate) // Get the most recent one
                    .FirstOrDefaultAsync();

                // Calculate days since last completed job
                int lastDeliveryDays = lastCompletedExchange != null
                    ? (DateTime.UtcNow - lastCompletedExchange.LastStatusChangeDate).Days
                    : -1; // Default if no completed jobs

                // Build the UserProfile view model.
                var model = new UserProfileVM
                {
                    User = user,
                    Educations = user.TblEducations.OrderByDescending(e => e.StartDate).ToList(),
                    Experiences = user.TblExperiences.OrderByDescending(e => e.StartDate).ToList(),
                    Languages = user.TblLanguages.ToList(),
                    Certificates = user.TblUserCertificateUsers.ToList(),
                    LastExchangeDays = lastDeliveryDays,
                    RecommendedPercentage = recommendedPercentage,
                    Skills = skillList
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /UserProfile/EditProfile
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.TblUsers
                .Include(u => u.TblUserSkills)
                    .ThenInclude(us => us.Skill)
                .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return NotFound("User not found.");

            // Map the user's existing skills only.
            var userSkillEditList = user.TblUserSkills
                .Select(us => new SkillEditVM
                {
                    SkillId = us.Skill.SkillId,
                    SkillName = us.Skill.SkillName,
                    Category = us.Skill.SkillCategory,
                    ProficiencyLevel = us.ProficiencyLevel
                })
                .ToList();

            // Prepare composite view model.
            var model = new EditProfileCompositeVM
            {
                PersonalDetails = new EditPersonalDetailsVM
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    PersonalWebsite = user.PersonalWebsite,
                    Location = user.Location,
                    Address = user.Address,
                    City = user.City,
                    Country = user.Country,
                    AboutMe = user.AboutMe,
                    ProfileImageUrl = user.ProfileImageUrl,
                },
                Skills = new EditSkillVM
                {
                    OfferedSkillSummary = user.OfferedSkillAreas,
                    WillingSkillSummary = user.DesiredSkillAreas,
                    AllSkills = userSkillEditList,
                    ProficiencyOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "1", Text = "Basic" },
                        new SelectListItem { Value = "2", Text = "Intermediate" },
                        new SelectListItem { Value = "3", Text = "Proficient" }
                    }
                },
                // Optionally load education, experience, and certificate entries if needed.
                EducationEntries = new List<EducationVM>(),
                ExperienceEntries = new List<ExperienceVM>(),
                CertificateEntries = new List<CertificateVM>()
            };

            // Pre-populate up to 5 rows.
            int maxSkills = 5;
            var skillsList = model.Skills.AllSkills.ToList();
            for (int i = skillsList.Count; i < maxSkills; i++)
            {
                skillsList.Add(new SkillEditVM());
            }
            model.Skills.AllSkills = skillsList;
            return View(model);
        }

        // POST: /UserProfile/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(EditProfileCompositeVM model)
        {
            
    // Remove ModelState errors for cleared skill rows.
    for (int i = 0; i < model.Skills.AllSkills.Count; i++)
    {
        if (string.IsNullOrWhiteSpace(model.Skills.AllSkills[i].SkillName))
        {
            ModelState.Remove($"Skills.AllSkills[{i}].SkillName");
            ModelState.Remove($"Skills.AllSkills[{i}].Category");
            ModelState.Remove($"Skills.AllSkills[{i}].ProficiencyLevel");
        }
    }
            if (!ModelState.IsValid)
            {
                return View("EditProfile", model);
            }

            // Validate that each summary contains at most 5 skills.
            var offeredSkillsList = (model.Skills.OfferedSkillSummary ?? "")
                                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s))
                                        .ToList();

            var willingSkillsList = (model.Skills.WillingSkillSummary ?? "")
                                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s))
                                        .ToList();

            if (offeredSkillsList.Count > 5)
            {
                ViewBag.ErrorMessage = "You can only add up to 5 offered skills.";
                ModelState.AddModelError("Skills.OfferedSkillSummary", "You can only add up to 5 offered skills.");
                return View("EditProfile", model);
            }
            if (willingSkillsList.Count > 5)
            {
                ViewBag.ErrorMessage = "You can only add up to 5 willing skills.";
                ModelState.AddModelError("Skills.WillingSkillSummary", "You can only add up to 5 willing skills.");
                return View("EditProfile", model);
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                UpdatePersonalDetails(model.PersonalDetails, userId);
                await UpdateSkillsAsync(model.Skills, userId);
                // Optionally, call UpdateEducationAsync, UpdateExperienceAsync, UpdateCertificatesAsync if you support editing those.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("EditProfile", "UserProfile");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                ModelState.AddModelError("", "An error occurred while updating your profile. Please try again.");
                return View("EditProfile", model);
            }
        }

        #region Helper Methods

        private void UpdatePersonalDetails(EditPersonalDetailsVM personal, int userId)
        {
            var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            if (user != null)
            {
                user.FirstName = personal.FirstName;
                user.LastName = personal.LastName;
                user.UserName = personal.UserName;
                user.Email = personal.Email;
                user.PersonalWebsite = personal.PersonalWebsite;
                user.Location = personal.Location;
                user.Address = personal.Address;
                user.City = personal.City;
                user.Country = personal.Country;
                user.AboutMe = personal.AboutMe;
                if (personal.ProfileImageFile != null && personal.ProfileImageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    if (!ValidateFile(personal.ProfileImageFile, allowedExtensions, 2 * 1024 * 1024, out string errorMsg))
                    {
                        throw new Exception(errorMsg);
                    }
                    string fileUrl = UploadFileAsync(personal.ProfileImageFile, "profile").Result;
                    user.ProfileImageUrl = fileUrl;
                }
            }
        }

        private async Task UpdateSkillsAsync(EditSkillVM skills, int userId)
        {
            // Update summary fields.
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.OfferedSkillAreas = skills.OfferedSkillSummary;
                user.DesiredSkillAreas = skills.WillingSkillSummary;
            }

            // Remove all existing user skill assignments.
            var existingSkills = await _context.TblUserSkills.Where(us => us.UserId == userId).ToListAsync();
            _context.TblUserSkills.RemoveRange(existingSkills);

            // Process each global skill that is selected.
            foreach (var skill in skills.AllSkills)
            {
                // Skip if the row is empty (no skill name entered).
                if (string.IsNullOrWhiteSpace(skill.SkillName))
                    continue;

                if (!skill.ProficiencyLevel.HasValue)
                {
                    ViewBag.ErrorMessage = $"Please select a proficiency level for {skill.SkillName}.";
                }

                // If the selected category is "Other" and a custom category is provided, use it.
                var categoryToUse = skill.Category;
                if (categoryToUse == "Other" && !string.IsNullOrWhiteSpace(skill.CustomCategory))
                {
                    categoryToUse = skill.CustomCategory;
                }

                // Look up or create the global skill. Use CategoryId as needed.
                TblSkill? globalSkill = null;
                if (skill.SkillId != 0)
                {
                    globalSkill = await _context.TblSkills.FirstOrDefaultAsync(s => s.SkillId == skill.SkillId);
                    // If the skill name has been updated, assign the new value.
                    if (globalSkill != null && !string.Equals(globalSkill.SkillName, skill.SkillName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if another skill already has this new name
                        var existingSkill = await _context.TblSkills
                            .FirstOrDefaultAsync(s => s.SkillName.ToLower() == skill.SkillName.ToLower());

                        if (existingSkill != null)
                        {
                            globalSkill = existingSkill;
                        }
                        else
                        {
                            // New skill entirely
                            globalSkill = new TblSkill
                            {
                                SkillName = skill.SkillName,
                                SkillCategory = skill.Category
                            };
                            _context.TblSkills.Add(globalSkill);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    globalSkill = await _context.TblSkills
                        .FirstOrDefaultAsync(s => s.SkillName.ToLower() == skill.SkillName.ToLower());

                    if (globalSkill == null)
                    {
                        globalSkill = new TblSkill
                        {
                            SkillName = skill.SkillName,
                            SkillCategory = skill.Category
                        };
                        _context.TblSkills.Add(globalSkill);
                        await _context.SaveChangesAsync();
                    }
                }

                if (globalSkill == null)
                {
                    globalSkill = new TblSkill
                    {
                        SkillName = skill.SkillName,
                        SkillCategory = skill.Category
                    };
                    _context.TblSkills.Add(globalSkill);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Optionally update the category if needed.
                    globalSkill.SkillCategory = categoryToUse;
                }

                _context.TblUserSkills.Add(new TblUserSkill
                {
                    UserId = userId,
                    SkillId = globalSkill.SkillId,
                    ProficiencyLevel = skill.ProficiencyLevel,
                });

            }
        }

        private bool ValidateFile(IFormFile file, string[] allowedExtensions, long maxSizeBytes, out string errorMessage)
        {
            errorMessage = string.Empty;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                errorMessage = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}.";
                return false;
            }
            if (file.Length > maxSizeBytes)
            {
                errorMessage = $"File size exceeds the maximum allowed size of {maxSizeBytes / (1024 * 1024)} MB.";
                return false;
            }
            return true;
        }

        private async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderName);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/uploads/{folderName}/{uniqueFileName}";
        }

        #endregion
    }
}