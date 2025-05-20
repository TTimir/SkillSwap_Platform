using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Swap.Models;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.OnBoardVM;
using SkillSwap_Platform.Services.BadgeTire;
using SkillSwap_Platform.Services.Email;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.Controllers
{
    public class OnboardingController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<OnboardingController> _logger;
        private readonly IEmailService _emailService;
        private readonly BadgeService _badgeService;

        // Per-step rewards (sum = 2.0m)
        private const decimal ROLE_STEP_REWARD = 0.25m;
        private const decimal PROFILE_STEP_REWARD = 0.25m;
        private const decimal SKILLEXP_STEP_REWARD = 0.50m;
        private const decimal SKILLPREF_STEP_REWARD = 0.50m;
        private const decimal CERT_STEP_REWARD = 0.25m;
        private const decimal SOCKYC_STEP_REWARD = 0.25m;

        public OnboardingController(SkillSwapDbContext context, ILogger<OnboardingController> logger, IEmailService emailService, BadgeService badgeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
        }

        #region STEP 1: Select Role

        //[HttpGet]
        //public IActionResult SelectRole()
        //{
        //    // Get user ID from session
        //    int? userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        //    if (userId == null)
        //        return RedirectToAction("Login", "Home"); // 🔥 Redirect if session expired

        //    return View(new SelectRoleVM());
        //}

        [HttpGet]
        public IActionResult SelectRole()
        {
            // CHANGE: Check for user id using GetUserId()
            if (GetUserId() == null)
                return RedirectToAction("Login", "Home");

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = ROLE_STEP_REWARD;
            return View(new SelectRoleVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectRole(SelectRoleVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Please correct the errors in your role.";
                ViewBag.StepReward = ROLE_STEP_REWARD;
                return View(model);
            }

            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE

            // Use transaction for consistency when updating multiple tables.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Remove existing roles.
                    var existingRoles = await _context.TblUserRoles.Where(ur => ur.UserId == userId).ToListAsync();
                    _context.TblUserRoles.RemoveRange(existingRoles);

                    // Map SelectedRole to role IDs.
                    if (model.SelectedRole == "Teacher")
                        _context.TblUserRoles.Add(new TblUserRole { UserId = userId, RoleId = 2 });
                    else if (model.SelectedRole == "Student")
                        _context.TblUserRoles.Add(new TblUserRole { UserId = userId, RoleId = 3 });
                    else if (model.SelectedRole == "Both")
                    {
                        _context.TblUserRoles.Add(new TblUserRole { UserId = userId, RoleId = 2 });
                        _context.TblUserRoles.Add(new TblUserRole { UserId = userId, RoleId = 3 });
                    }

                    // Save referral info in the user record.
                    var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                        user.Description = "Referral: " + model.ReferralSource;

                    // mark progress flag (no token credit yet)
                    var prog = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId)
                               ?? new TblOnboardingProgress { UserId = userId };
                    prog.RoleSelected = true;
                    if (_context.Entry(prog).State == EntityState.Detached)
                        _context.TblOnboardingProgresses.Add(prog);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["ShowCreditSuccess"] = true;
                    ViewBag.StepReward = ROLE_STEP_REWARD;
                    return View(model);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error saving role selection for user {UserId}", userId);
                    ViewBag.ErrorMessage = "An error occurred while saving your role selection.";
                    return View(model);
                }
            }
        }

        #endregion

        #region STEP 2: Profile Completion

        [HttpGet]
        public async Task<IActionResult> ProfileCompletion()
        {
            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                ViewBag.StepReward = PROFILE_STEP_REWARD;
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE

            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            var model = new ProfileCompletionVM();
            if (user != null)
            {
                model.Location = user.CurrentLocation;
                model.Address = user.Address;
                model.City = user.City;
                model.Country = user.Country;
                model.AboutMe = user.AboutMe;
                model.Designation = user.Designation;
                model.Zip = user.Zip;
            }

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = PROFILE_STEP_REWARD;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfileCompletion(ProfileCompletionVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Please correct the errors in your profile.";
                return View(model);
            }

            ViewBag.StepReward = PROFILE_STEP_REWARD;

            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);

            // Handle profile image upload if provided.
            if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
            {
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                if (!ValidateFile(model.ProfileImageFile, allowedImageExtensions, 2 * 1024 * 1024, out string errorMsg))
                {
                    ViewBag.ErrorMessage = errorMsg;
                    return View(model);
                }
                string fileUrl = await UploadFileAsync(model.ProfileImageFile, "profile");
                user.ProfileImageUrl = fileUrl;
            }

            user.CurrentLocation = model.Location;
            user.Address = model.Address;
            user.City = model.City;
            user.Country = model.Country;
            user.AboutMe = model.AboutMe;
            user.Designation = model.Designation;
            user.Zip = model.Zip;

            try
            {
                // mark progress
                var prog = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId)
                           ?? new TblOnboardingProgress { UserId = userId };
                prog.ProfileCompleted = true;
                if (_context.Entry(prog).State == EntityState.Detached)
                    _context.TblOnboardingProgresses.Add(prog);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profile for user {UserId}", userId);
                ViewBag.ErrorMessage = "An error occurred while saving your profile."; return View(model);
            }

            TempData["ShowCreditSuccess"] = true;
            return View(model);
        }

        #endregion

        #region STEP 3: Skills & Experience

        [HttpGet]
        public async Task<IActionResult> SkillsExperience()
        {
            //int? userId = HttpContext.Session.GetInt32("TempUserId");
            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers
                .Include(u => u.TblEducations)
                .Include(u => u.TblExperiences)
                .Include(u => u.TblLanguages)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var model = new SkillsExperienceVM();
            if (user != null)
            {
                model.Education = user.Education;
                model.Experience = user.Experience;
                model.Languages = user.Languages;
                model.DesiredSkillAreas = user.DesiredSkillAreas;
                model.OfferedSkillAreas = user.OfferedSkillAreas;
            }

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = SKILLEXP_STEP_REWARD;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SkillsExperience(SkillsExperienceVM model, IFormCollection form)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Please correct the errors in your skills and experience.";
                ViewBag.StepReward = SKILLEXP_STEP_REWARD;
                return View(model);
            }

            ViewBag.StepReward = SKILLEXP_STEP_REWARD;

            int? userId = GetUserId();
            if (userId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please log in again.";
                return RedirectToAction("Login", "Home");
            }
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);

            // Process Education, Language, and Experience entries.
            var eduSummaries = ProcessEducationEntries(form, userId.Value, out bool eduValid, out string eduError);
            if (!eduValid)
            {
                ViewBag.ErrorMessage = eduError;
                return View(model);
            }
            user.Education = string.Join("; ", eduSummaries);

            var langSummaries = ProcessLanguageEntries(form, userId.Value, out bool langValid, out string langError);
            if (!langValid)
            {
                ViewBag.ErrorMessage = langError;
                return View(model);
            }
            user.Languages = string.Join(", ", langSummaries);

            double totalExperienceYears = ProcessExperienceEntries(form, userId.Value, out bool expValid, out string expError);
            if (!expValid)
            {
                ViewBag.ErrorMessage = expError;
                return View(model);
            }
            user.Experience = totalExperienceYears.ToString("0.0") + " years";

            try
            {
                // mark progress
                var prog = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId)
                           ?? new TblOnboardingProgress { UserId = userId.Value };
                prog.SkillsFilled = true;
                if (_context.Entry(prog).State == EntityState.Detached)
                    _context.TblOnboardingProgresses.Add(prog);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving skills and experience for user {UserId}", userId);
                ViewBag.ErrorMessage = "An error occurred while saving your skills and experience.";
                return View(model);
            }
            TempData["ShowCreditSuccess"] = true;
            return View(model);

            //    #region Process Education Entries (Required)

            //    // Retrieve education arrays from the form.
            //    var degreeTypes = form["education[degree][]"].ToArray();
            //    var degreeNames = form["education[degree_name][]"].ToArray();
            //    var institutions = form["education[institution_name][]"].ToArray();
            //    var eduStartDates = form["education[start_date][]"].ToArray();
            //    var eduEndDates = form["education[end_date][]"].ToArray();
            //    var eduDescription = form["education[description][]"].ToArray();

            //    // Determine the maximum number of rows posted.
            //    int eduCount = new int[] { degreeTypes.Length, degreeNames.Length, institutions.Length, eduStartDates.Length, eduEndDates.Length, eduDescription.Length }.Max();

            //    var educationSummaries = new List<string>();

            //    for (int i = 0; i < eduCount; i++)
            //    {
            //        // Safely retrieve each field using the current index (or default to empty string).
            //        string degreeType = i < degreeTypes.Length ? degreeTypes[i]?.Trim() : "";
            //        string degreeName = i < degreeNames.Length ? degreeNames[i]?.Trim() : "";
            //        string institution = i < institutions.Length ? institutions[i]?.Trim() : "";
            //        string startDateString = i < eduStartDates.Length ? eduStartDates[i]?.Trim() : "";
            //        string endDateString = i < eduEndDates.Length ? eduEndDates[i]?.Trim() : "";
            //        string eduDesc = i < eduDescription.Length ? eduDescription[i]?.Trim() : "";

            //        // Skip rows that are completely empty.
            //        if (string.IsNullOrWhiteSpace(degreeType) &&
            //            string.IsNullOrWhiteSpace(degreeName) &&
            //            string.IsNullOrWhiteSpace(institution) &&
            //            string.IsNullOrWhiteSpace(startDateString) &&
            //            string.IsNullOrWhiteSpace(endDateString) &&
            //                string.IsNullOrWhiteSpace(eduDesc))
            //        {
            //            continue;
            //        }

            //        // Validate required fields.
            //        if (string.IsNullOrWhiteSpace(degreeType) || string.IsNullOrWhiteSpace(degreeName) ||
            //            string.IsNullOrWhiteSpace(institution))
            //        {
            //            ViewBag.ErrorMessage = $"Degree, Degree Name, and Institution are required for education entry {i + 1}.";
            //            return View(model);
            //        }

            //        // Validate Start Date.
            //        if (!DateTime.TryParse(startDateString, out DateTime startDate))
            //        {
            //            ViewBag.ErrorMessage = $"Invalid Start Date for education entry {i + 1}.";
            //            return View(model);
            //        }
            //        DateTime? endDate = null;
            //        if (!string.IsNullOrWhiteSpace(endDateString))
            //        {
            //            if (DateTime.TryParse(endDateString, out DateTime parsedEnd))
            //                endDate = parsedEnd;
            //            else
            //            {
            //                ViewBag.ErrorMessage = $"Invalid End Date for education entry {i + 1}.";
            //                return View(model);
            //            }
            //        }

            //        // Build a summary for this education entry.
            //        educationSummaries.Add($"{degreeName} from {institution}");

            //        // Create a new education record.
            //        var education = new TblEducation
            //        {
            //            UserId = userId,
            //            Degree = degreeType,         // e.g., "School's", "Bachelor's", etc.
            //            DegreeName = degreeName,       // e.g., "High School", "BMU"
            //            UniversityName = institution,
            //            InstitutionName = institution,
            //            StartDate = startDate,
            //            EndDate = endDate,
            //            Description = eduDesc
            //        };
            //        _context.TblEducations.Add(education);
            //    }

            //    if (!educationSummaries.Any())
            //    {
            //        ViewBag.ErrorMessage = "At least one valid education entry must be provided.";
            //        return View(model);
            //    }

            //    // Update the summary column in TblUsers.
            //    user.Education = string.Join("; ", educationSummaries);

            //    #endregion


            //    #region Process Language Entries

            //    var languageNames = form["language[name][]"].ToArray();
            //    var languageLevels = form["language[level][]"].ToArray();

            //    int langCount = languageNames.Length;
            //    if (langCount == 0)
            //    {
            //        ViewBag.ErrorMessage = "At least one language entry is required.";
            //        return View(model);
            //    }

            //    var languageSummaries = new List<string>();
            //    for (int i = 0; i < langCount; i++)
            //    {
            //        string langName = languageNames[i]?.Trim();
            //        string proficiency = languageLevels.ElementAtOrDefault(i)?.Trim();

            //        // Skip completely empty rows.
            //        if (string.IsNullOrWhiteSpace(langName) && string.IsNullOrWhiteSpace(proficiency))
            //            continue;

            //        if (string.IsNullOrWhiteSpace(langName))
            //        {
            //            ViewBag.ErrorMessage = $"Language name is required for language entry {i + 1}.";
            //            return View(model);
            //        }

            //        languageSummaries.Add(!string.IsNullOrWhiteSpace(proficiency)
            //            ? $"{langName} ({proficiency})"
            //            : langName);

            //        var language = new TblLanguage
            //        {
            //            UserId = userId,
            //            Language = langName,
            //            Proficiency = proficiency
            //        };
            //        _context.TblLanguages.Add(language);
            //    }

            //    if (!languageSummaries.Any())
            //    {
            //        ViewBag.ErrorMessage = "At least one valid language entry must be provided.";
            //        return View(model);
            //    }
            //    user.Languages = string.Join(", ", languageSummaries);

            //    #endregion

            //    #region Process Experience Entries

            //    var compNames = form["experience[company_name][]"].ToArray();
            //    var positions = form["experience[position][]"].ToArray();
            //    var expStartDates = form["experience[start_date][]"].ToArray();
            //    var expEndDates = form["experience[end_date][]"].ToArray();
            //    var expDescription = form["experience[description][]"].ToArray();

            //    int expCount = compNames.Length;
            //    if (expCount == 0)
            //    {
            //        ViewBag.ErrorMessage = "At least one experience entry is required.";
            //        return View(model);
            //    }

            //    double totalExperienceYears = 0; // Calculate total duration in years.
            //    int validExperienceCount = 0;
            //    for (int i = 0; i < expCount; i++)
            //    {
            //        string companyName = compNames[i]?.Trim();
            //        string position = positions[i]?.Trim();
            //        string expStartDateStr = expStartDates[i]?.Trim();
            //        string expEndDateStr = expEndDates[i]?.Trim();
            //        string expDesc = expDescription[i]?.Trim();

            //        // Skip completely empty rows.
            //        if (string.IsNullOrWhiteSpace(companyName) && string.IsNullOrWhiteSpace(position) &&
            //            string.IsNullOrWhiteSpace(expStartDateStr) && string.IsNullOrWhiteSpace(expEndDateStr) && string.IsNullOrWhiteSpace(expDesc))
            //        {
            //            continue;
            //        }

            //        if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(position))
            //        {
            //            ViewBag.ErrorMessage = $"Both Company and Position are required for experience entry {i + 1}.";
            //            return View(model);
            //        }

            //        if (!DateTime.TryParse(expStartDateStr, out DateTime expStart))
            //        {
            //            ViewBag.ErrorMessage = $"Invalid Start Date for experience entry {i + 1}.";
            //            return View(model);
            //        }

            //        // If End Date is missing, assume ongoing; use DateTime.Now for calculation.
            //        DateTime expEnd;
            //        if (!string.IsNullOrWhiteSpace(expEndDateStr))
            //        {
            //            if (!DateTime.TryParse(expEndDateStr, out expEnd))
            //            {
            //                ViewBag.ErrorMessage = $"Invalid End Date for experience entry {i + 1}.";
            //                return View(model);
            //            }
            //        }
            //        else
            //        {
            //            expEnd = DateTime.Now;
            //        }

            //        validExperienceCount++;
            //        double durationYears = (expEnd - expStart).TotalDays / 365;
            //        totalExperienceYears += durationYears;

            //        var experience = new TblExperience
            //        {
            //            UserId = userId,
            //            CompanyName = companyName,
            //            Position = position,
            //            Years = totalExperienceYears > 0 ? (decimal?)Math.Round(totalExperienceYears, 2) : null,
            //            Description = expDesc,
            //            StartDate = expStart,
            //            EndDate = string.IsNullOrWhiteSpace(expEndDateStr) ? (DateTime?)null : expEnd
            //        };
            //        _context.TblExperiences.Add(experience);
            //    }

            //    if (validExperienceCount == 0)
            //    {
            //        ViewBag.ErrorMessage = "At least one valid experience entry must be provided.";
            //        return View(model);
            //    }
            //    user.Experience = totalExperienceYears.ToString("0.0") + " years";

            //    #endregion

            //    try
            //    {
            //        await _context.SaveChangesAsync();
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogError(ex, "Error saving skills and experience for user {UserId}", userId);
            //        ViewBag.ErrorMessage = "An error occurred while saving your skills and experience.";
            //        return View(model);
            //    }
            //    return RedirectToAction(nameof(SkillPreference));
        }

        #endregion

        #region STEP 4: Skill Preference

        [HttpGet]
        public async Task<IActionResult> SkillPreference()
        {
            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            var model = new SkillPreferenceVM();
            if (user != null)
            {
                model.DesiredSkillAreas = user.DesiredSkillAreas;
                model.OfferedSkillAreas = user.OfferedSkillAreas;
            }

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = SKILLPREF_STEP_REWARD;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SkillPreference(SkillPreferenceVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Please correct the errors in your skill preferences.";
                ViewBag.StepReward = SKILLPREF_STEP_REWARD;
                return View(model);
            }

            ViewBag.StepReward = SKILLPREF_STEP_REWARD;

            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return View(model);
            }

            // Basic SkillPreferences fields
            var offeredRaw = Request.Form["offeredSkills"].ToString();
            var willingRaw = Request.Form["willingSkills"].ToString();
            if (string.IsNullOrWhiteSpace(offeredRaw) || string.IsNullOrWhiteSpace(willingRaw))
            {
                ViewBag.ErrorMessage = "Please enter at least one skill in each field (Your Offering and Willing).";
                return View(model);
            }
            user.OfferedSkillAreas = offeredRaw;
            user.DesiredSkillAreas = willingRaw;

            // Extract offered skills list from the hidden input.
            var offeredSkillsFromBadges = Request.Form["offeredSkills"]
                .ToString()
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToList();

            // Process dynamic offered skill rows.
            var offeredSkillNames = Request.Form["skill[name][]"].ToArray();
            var offeredSkillCategories = Request.Form["skill[category][]"].ToArray();
            var offeredSkillCustomCategories = Request.Form["skill[customCategory][]"].ToArray();
            var offeredSkillLevels = Request.Form["skill[level][]"].ToArray();

            // Get existing skills for duplication check.
            var existingUserSkills = await _context.TblUserSkills
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Skill.SkillName, s.Skill.SkillCategory }) // Get only relevant fields
                .ToListAsync();

            var toInsertSkills = new List<TblSkill>();
            var toInsertUserSkills = new List<TblUserSkill>();
            var duplicateErrors = new List<string>();

            // Preload all existing global skills into a dictionary for quick lookup
            var allGlobalSkills = await _context.TblSkills
                .ToListAsync();
            var globalSkillLookup = allGlobalSkills
                .ToDictionary(s => (s.SkillName.ToLower(), s.SkillCategory.ToLower()), s => s);

            for (int i = 0; i < offeredSkillNames.Length; i++)
            {
                string skillName = offeredSkillNames[i]?.Trim();
                if (string.IsNullOrWhiteSpace(skillName))
                    continue;

                // Determine category (use custom value if "Other" was selected).
                string category = offeredSkillCategories.ElementAtOrDefault(i)?.Trim();
                if (category == "Other")
                {
                    category = offeredSkillCustomCategories.ElementAtOrDefault(i)?.Trim();
                }

                if (existingUserSkills.Any(x =>
                    x.SkillName.Equals(skillName, StringComparison.OrdinalIgnoreCase) &&
                    x.SkillCategory.Equals(category, StringComparison.OrdinalIgnoreCase)))
                {
                    duplicateErrors.Add($"{skillName} ({category})");
                    continue;
                }

                // Map proficiency
                int? prof = offeredSkillLevels.ElementAtOrDefault(i) switch
                {
                    "Basic" => 1,
                    "Intermediate" => 2,
                    "Proficient" => 3,
                    _ => (int?)null
                };

                // Lookup or stage new global skill
                TblSkill skillEntity;
                var key = (skillName.ToLower(), category.ToLower());
                if (!globalSkillLookup.TryGetValue(key, out skillEntity))
                {
                    skillEntity = new TblSkill { SkillName = skillName, SkillCategory = category };
                    globalSkillLookup[key] = skillEntity;
                    toInsertSkills.Add(skillEntity);
                }

                // Create the user-skill link
                toInsertUserSkills.Add(new TblUserSkill
                {
                    UserId = userId,
                    Skill = skillEntity,           // EF will wire up the FK
                    ProficiencyLevel = prof,
                    IsOffering = offeredRaw
                                      .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(s => s.Trim().ToLower())
                                      .Contains(skillName.ToLower())
                });
            }

            if (duplicateErrors.Any())
            {
                ViewBag.ErrorMessage = "These skills already exist in your profile: "
                    + string.Join(", ", duplicateErrors);
                return View(model);
            }

            // Add all new entities in one go
            if (toInsertSkills.Any())
                _context.TblSkills.AddRange(toInsertSkills);
            _context.TblUserSkills.AddRange(toInsertUserSkills);

            try
            {
                // mark progress
                var prog = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId)
                           ?? new TblOnboardingProgress { UserId = userId };
                prog.SkillPreferencesSet = true;
                if (_context.Entry(prog).State == EntityState.Detached)
                    _context.TblOnboardingProgresses.Add(prog);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving skill preferences for user {UserId}", userId);
                ViewBag.ErrorMessage = "Error saving skill preferences: " + ex.Message;
                return View(model);
            }
            TempData["ShowCreditSuccess"] = true;
            return View(model);
        }

        #endregion

        #region STEP 5: Certificate Details

        [HttpGet]
        public IActionResult CertificateDetails()
        {
            // CHANGE: Check for user id using GetUserId()
            if (GetUserId() == null)
                return RedirectToAction("Login", "Home");

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = CERT_STEP_REWARD;
            return View(new CertificateDetailsVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CertificateDetails(IFormCollection form)
        {
            var model = new CertificateDetailsVM();

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "There was an error in your submission. Please correct and try again.";
                ViewBag.StepReward = CERT_STEP_REWARD;
                return View(model);
            }

            ViewBag.StepReward = CERT_STEP_REWARD;

            var certificateNames = form["certification[certificate_name][]"];
            var certifiedFrom = form["certification[certified_from][]"];
            var completionDate = form["certification[completion_date][]"];
            var verificationIds = form["certification[verification_id][]"];
            var files = form.Files.GetFiles("certification[certificate_file][]");

            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            bool hasValidCertificate = false;

            for (int i = 0; i < certificateNames.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(certificateNames[i]))
                {
                    ViewBag.ErrorMessage = $"Certificate name is required for entry {i + 1}.";
                    return View(model);
                }
                if (files.Count <= i || files[i] == null || files[i].Length == 0)
                {
                    ViewBag.ErrorMessage = $"File is required for certificate '{certificateNames[i]}' at entry {i + 1}.";
                    return View(model);
                }

                // Validate certificate file: allow pdf and images, max 5 MB.
                var allowedCertExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                if (!ValidateFile(files[i], allowedCertExtensions, 5 * 1024 * 1024, out string certError))
                {
                    ViewBag.ErrorMessage = certError;
                    return View(model);
                }

                hasValidCertificate = true;
                string fileUrl = await UploadFileAsync(files[i], "certificates");
                DateTime parsedDate;
                var certificate = new TblUserCertificate
                {
                    UserId = userId,
                    CertificateName = certificateNames[i],
                    CertificateFrom = certifiedFrom[i],
                    CompleteDate = DateTime.TryParse(completionDate[i], out parsedDate) ? parsedDate : (DateTime?)null,
                    VerificationId = verificationIds[i],
                    CertificateFilePath = fileUrl,
                    SubmittedDate = DateTime.Now,
                    IsApproved = false
                };
                _context.TblUserCertificates.Add(certificate);
            }

            if (!hasValidCertificate)
            {
                ViewBag.ErrorMessage = "At least one valid certificate must be provided.";
                return View(model);
            }

            try
            {
                // mark progress
                var prog = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId)
                           ?? new TblOnboardingProgress { UserId = userId };
                prog.CertificateUploaded = true;
                if (_context.Entry(prog).State == EntityState.Detached)
                    _context.TblOnboardingProgresses.Add(prog);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error saving certificate details: " + ex.Message;
                return View(model);
            }

            TempData["ShowCreditSuccess"] = true;
            return View(model);
        }

        #endregion

        #region STEP 6: Additional Information

        [HttpGet]
        public async Task<IActionResult> AdditionalInfo()
        {
            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            var model = new AdditionalInfoVM();
            if (user != null)
                model.SocialMediaLinks = user.SocialMediaLinks;

            if (TempData.ContainsKey("ShowCreditSuccess"))
                TempData.Remove("ShowCreditSuccess");

            ViewBag.StepReward = SOCKYC_STEP_REWARD;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdditionalInfo(AdditionalInfoVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Please correct the errors in your additional information.";
                ViewBag.StepReward = SOCKYC_STEP_REWARD;
                return View(model);
            }

            ViewBag.StepReward = SOCKYC_STEP_REWARD;

            int? tempUserId = GetUserId(); // CHANGE
            if (tempUserId == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return RedirectToAction("Login", "Home");
            }
            int userId = tempUserId.Value; // CHANGE
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = "User not found. Please try to login again.";
                return View(model);
            }

            // Validate social media URLs (for selected platforms)
            bool IsValidUrl(string url)
            {
                if (string.IsNullOrWhiteSpace(url))
                    return false; // Optional field
                string pattern = @"^(https?:\/\/)?(www\.)?(facebook|instagram|linkedin|behance|pinterest|twitter|github)\.com\/[A-Za-z0-9_\-\/]+$";
                return System.Text.RegularExpressions.Regex.IsMatch(url, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Retrieve input values and convert StringValues to string
            string personalWebsite = Request.Form["personalWebsite"].ToString().Trim();
            string facebook = Request.Form["facebook"].ToString().Trim();
            string instagram = Request.Form["instagram"].ToString().Trim();
            string linkedIn = Request.Form["linkedin"].ToString().Trim();
            string behance = Request.Form["behance"].ToString().Trim();
            string pinterest = Request.Form["pinterest"].ToString().Trim();
            string twitter = Request.Form["twitter"].ToString().Trim();
            string github = Request.Form["github"].ToString().Trim();

            bool hasAtLeastOneSocial = !string.IsNullOrEmpty(facebook) || !string.IsNullOrEmpty(instagram) ||
                           !string.IsNullOrEmpty(linkedIn) || !string.IsNullOrEmpty(behance) ||
                           !string.IsNullOrEmpty(pinterest) || !string.IsNullOrEmpty(twitter) ||
                           !string.IsNullOrEmpty(github);

            if (!hasAtLeastOneSocial)
            {
                ViewBag.ErrorMessage = "❌ Please enter at least one social media link.";
                return View(model);
            }

            // Store social media links in JSON format
            var socialMedia = new
            {
                Facebook = facebook,
                Instagram = instagram,
                LinkedIn = linkedIn,
                Behance = behance,
                Pinterest = pinterest,
                Twitter = twitter,
                GitHub = github
            };

            user.SocialMediaLinks = Newtonsoft.Json.JsonConvert.SerializeObject(socialMedia);
            user.PersonalWebsite = personalWebsite;

            // 🚀 Mark onboarding as completed
            user.IsOnboardingCompleted = true;
            var progress = await _context.TblOnboardingProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
            progress.SocialAndKycCompleted = true;

            decimal totalEarned = 0;
            if (progress.RoleSelected) totalEarned += ROLE_STEP_REWARD;
            if (progress.ProfileCompleted) totalEarned += PROFILE_STEP_REWARD;
            if (progress.SkillsFilled) totalEarned += SKILLEXP_STEP_REWARD;
            if (progress.SkillPreferencesSet) totalEarned += SKILLPREF_STEP_REWARD;
            if (progress.CertificateUploaded) totalEarned += CERT_STEP_REWARD;
            if (progress.SocialAndKycCompleted) totalEarned += SOCKYC_STEP_REWARD;

            HttpContext.Session.Remove("TempUserId");
            try
            {
                await _context.SaveChangesAsync();

                if (progress.RoleSelected
                && progress.ProfileCompleted
                && progress.SkillsFilled
                && progress.SkillPreferencesSet
                && progress.CertificateUploaded
                && progress.SocialAndKycCompleted)
                {
                    // Award Onboard Complete for  participants:
                    _badgeService.EvaluateAndAward(userId);
                }

                // 🎉 Fire off the "onboarding complete" email
                var htmlBody = $@"
                      <p>Hi {user.FirstName},</p>
                    
                      <p>Great news—you’ve successfully completed your onboarding on <strong>SkillSwap</strong>! Here’s a quick game plan to get rolling:</p>
                    
                      <ul>
                        <li>Give your profile a once‑over. A friendly bio and clear photo help other swappers get to know you.</li>
                        <li>Post your first offer or request. It takes just a minute, and it puts your skills on the map.</li>
                        <li>Browse the marketplace. Spot something cool? Say hello and kick‑off your first swap.</li>
                        <li>And more…</li>
                      </ul>
                    
                      <p>Before you jump in, take a moment to review your profile so it shines. Once you’re happy, dive right into the community and start creating meaningful connections.</p>
                    
                      <p>🔗 <a href="">Jump back in here</a></p>
                    
                      <p> We can’t wait to see the connections you create.</ p >
                    
                      <p> Happy swapping! < br /> The SkillSwap Team</ p >
                    ".Trim();

                await _emailService.SendEmailAsync(
                    user.Email,
                    $"You’re all set! Ready to Start Swapping, {user.FirstName}? 🎉",
                    htmlBody,
                    isBodyHtml: true
                    );
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error saving additional information: " + ex.Message;
                return View(model);
            }

            // Process KYC file upload (PDF only, max 5 MB).
            var kycFile = Request.Form.Files["kycFile"];
            if (kycFile != null && kycFile.Length > 0)
            {
                var allowedKycExtensions = new[] { ".pdf" };
                if (!ValidateFile(kycFile, allowedKycExtensions, 5 * 1024 * 1024, out string kycError))
                {
                    ViewBag.ErrorMessage = kycError;
                    return View(model);
                }
                string fileUrl = await UploadFileAsync(kycFile, "kyc");
                var kycRecord = new TblKycUpload
                {
                    UserId = userId,
                    DocumentName = Request.Form["documentType"],
                    DocumentNumber = Request.Form["documentNumber"],
                    DocumentImageUrl = fileUrl,
                    UploadedDate = DateTime.Now,
                    IsVerified = false
                };
                _context.TblKycUploads.Add(kycRecord);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = "Error saving KYC details: " + ex.Message;
                    return View(model);
                }
            }
            await CreditAllOnboardingRewards(userId, progress);
            TempData["ShowCreditSuccess"] = true;
            TempData["TotalEarned"] = totalEarned;
            return View(model);
        }

        #endregion

        #region STEP 7: Approval Pending

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult ApprovalPending()
        {
            //int? tempUserId = GetUserId(); // CHANGE
            //if (tempUserId == null)
            //{
            //    ViewBag.ErrorMessage = "User not found. Please try to login again.";
            //    return RedirectToAction("Login", "Home");
            //}
            //int userId = tempUserId.Value; // CHANGE
            //var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            //if (user != null && user.IsVerified)
            //{
            //    TempData["SuccessMessage"] = "Your account is sent for review! be kind and have patience.";
            //    // If approved, redirect to the dashboard.
            //    return RedirectToAction("Index", "Home");
            //}
            //return View();
            return RedirectToAction("Index", "Home");
        }

        #endregion

        //— CREDIT ALL REWARDS AT ONBOARD COMPLETE ——————————————————————————————————
        private async Task CreditAllOnboardingRewards(int userId, TblOnboardingProgress progress)
        {
            decimal total = 0;
            if (progress.RoleSelected) total += ROLE_STEP_REWARD;
            if (progress.ProfileCompleted) total += PROFILE_STEP_REWARD;
            if (progress.SkillsFilled) total += SKILLEXP_STEP_REWARD;
            if (progress.SkillPreferencesSet) total += SKILLPREF_STEP_REWARD;
            if (progress.CertificateUploaded) total += CERT_STEP_REWARD;
            if (progress.SocialAndKycCompleted) total += SOCKYC_STEP_REWARD;

            if (total <= 0) return;  // nothing to credit

            var user = await _context.TblUsers.FindAsync(userId);
            user.DigitalTokenBalance += total;

            _context.TblTokenTransactions.Add(new TblTokenTransaction
            {
                FromUserId = null,
                ToUserId = userId,
                Amount = total,
                TxType = "Reward-Onboarding",
                CreatedAt = DateTime.UtcNow,
                Description = "Onboarding completed: all steps rewarded"
            });

            // record it so it won’t run again
            progress.TotalTokensGiven = total;
            await _context.SaveChangesAsync();

            var subject = $"🎉 You’ve just earned {total:0.##} SkillSwap Token{(total > 1 ? "s" : "")}!";
            var htmlBody = $@"
                <div style=""font-family:sans-serif;line-height:1.4;color:#333;"">
                  <h2>Congrats, {user.FirstName}! 🥳</h2>
                  <p>Because you completed every onboarding step, we’ve just deposited <strong>{total:0.##} SkillSwap Token{(total > 1 ? "s" : "")}</strong> into your account.</p>
                  <img src=""/template_assets/images/SSDToken.png"" 
                       alt=""SkillSwap Token"" 
                       style=""width:100px;height:100px;object-fit:contain;"" />
                  <p>
                    Your new balance is now <strong>{user.DigitalTokenBalance:0.##}</strong> tokens.
                    Use them to post offers, request help, or explore the community—your next big swap is just a click away!
                  </p>
                  <blockquote style=""border-left:4px solid #5BBB7B;padding-left:1em;color:#444;margin:1em 0;"">
                    “Tokens aren’t just currency—they’re the fuel for your next achievement!”  
                  </blockquote>
                  <p>
                    <a href=""{Url.Action("Profile", "User")}"">View your profile</a> to see them in action.
                  </p>
                  <p style=""margin-top:2em;"">— Happy swapping,<br/>The SkillSwap Team</p>
                </div>";

            await _emailService.SendEmailAsync(
                user.Email,
                subject,
                htmlBody,
                isBodyHtml: true
            );
        }

        #region Helper Methods
        private int? GetUserId()
        {
            // First, try to get from session (set during registration/OTP)
            int? tempUserId = HttpContext.Session.GetInt32("TempUserId");
            if (tempUserId != null)
                return tempUserId;
            // Fallback: if user is signed in, try claims.
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
                return userId;
            return null;
        }

        // Validate file based on allowed extensions and maximum size.
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

        // Upload file to specified folder and return relative URL.
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

        // Helper method to process education entries.
        private List<string> ProcessEducationEntries(IFormCollection form, int userId, out bool isValid, out string error)
        {
            isValid = true;
            error = string.Empty;
            var summaries = new List<string>();

            var degreeTypes = form["education[degree][]"].ToArray();
            var degreeNames = form["education[degree_name][]"].ToArray();
            var institutions = form["education[institution_name][]"].ToArray();
            var eduStartDates = form["education[start_date][]"].ToArray();
            var eduEndDates = form["education[end_date][]"].ToArray();
            var eduDescriptions = form["education[description][]"].ToArray();

            int eduCount = new int[] { degreeTypes.Length, degreeNames.Length, institutions.Length, eduStartDates.Length, eduEndDates.Length, eduDescriptions.Length }.Max();

            for (int i = 0; i < eduCount; i++)
            {
                string degreeType = i < degreeTypes.Length ? degreeTypes[i]?.Trim() : "";
                string degreeName = i < degreeNames.Length ? degreeNames[i]?.Trim() : "";
                string institution = i < institutions.Length ? institutions[i]?.Trim() : "";
                string startDateStr = i < eduStartDates.Length ? eduStartDates[i]?.Trim() : "";
                string endDateStr = i < eduEndDates.Length ? eduEndDates[i]?.Trim() : "";
                string eduDesc = i < eduDescriptions.Length ? eduDescriptions[i]?.Trim() : "";

                // Skip completely empty rows.
                if (string.IsNullOrWhiteSpace(degreeType) &&
                    string.IsNullOrWhiteSpace(degreeName) &&
                    string.IsNullOrWhiteSpace(institution) &&
                    string.IsNullOrWhiteSpace(startDateStr) &&
                    string.IsNullOrWhiteSpace(endDateStr) &&
                    string.IsNullOrWhiteSpace(eduDesc))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(degreeType) || string.IsNullOrWhiteSpace(degreeName) ||
                    string.IsNullOrWhiteSpace(institution))
                {
                    isValid = false;
                    error = $"Degree, Degree Name, and Institution are required for education entry {i + 1}.";
                    break;
                }

                if (!DateTime.TryParse(startDateStr, out DateTime startDate))
                {
                    isValid = false;
                    error = $"Invalid Start Date for education entry {i + 1}.";
                    break;
                }

                DateTime? endDate = null;
                if (!string.IsNullOrWhiteSpace(endDateStr))
                {
                    if (DateTime.TryParse(endDateStr, out DateTime parsedEnd))
                        endDate = parsedEnd;
                    else
                    {
                        isValid = false;
                        error = $"Invalid End Date for education entry {i + 1}.";
                        break;
                    }
                }

                summaries.Add($"{degreeName} from {institution}");

                var education = new TblEducation
                {
                    UserId = userId,
                    Degree = degreeType,
                    DegreeName = degreeName,
                    UniversityName = institution, // Or use a different property if needed
                    InstitutionName = institution,
                    StartDate = startDate,
                    EndDate = endDate,
                    Description = eduDesc
                };
                _context.TblEducations.Add(education);
            }

            if (!summaries.Any())
            {
                isValid = false;
                error = "At least one valid education entry must be provided.";
            }
            return summaries;
        }

        // Helper method to process language entries.
        private List<string> ProcessLanguageEntries(IFormCollection form, int userId, out bool isValid, out string error)
        {
            isValid = true;
            error = string.Empty;
            var summaries = new List<string>();

            var languageNames = form["language[name][]"].ToArray();
            var languageLevels = form["language[level][]"].ToArray();
            int langCount = languageNames.Length;

            if (langCount == 0)
            {
                isValid = false;
                error = "At least one language entry is required.";
                return summaries;
            }

            for (int i = 0; i < langCount; i++)
            {
                string langName = languageNames[i]?.Trim();
                string proficiency = languageLevels.ElementAtOrDefault(i)?.Trim();

                if (string.IsNullOrWhiteSpace(langName) && string.IsNullOrWhiteSpace(proficiency))
                    continue;

                if (string.IsNullOrWhiteSpace(langName))
                {
                    isValid = false;
                    error = $"Language name is required for language entry {i + 1}.";
                    break;
                }

                summaries.Add(!string.IsNullOrWhiteSpace(proficiency)
                    ? $"{langName} ({proficiency})"
                    : langName);

                var language = new TblLanguage
                {
                    UserId = userId,
                    Language = langName,
                    Proficiency = proficiency
                };
                _context.TblLanguages.Add(language);
            }

            if (!summaries.Any())
            {
                isValid = false;
                error = "At least one valid language entry must be provided.";
            }
            return summaries;
        }

        // Helper method to process experience entries and calculate total years.
        private double ProcessExperienceEntries(IFormCollection form, int userId, out bool isValid, out string error)
        {
            isValid = true;
            error = string.Empty;
            double totalYears = 0;
            int validCount = 0;

            var compNames = form["experience[company_name][]"].ToArray();
            var positions = form["experience[position][]"].ToArray();
            var expStartDates = form["experience[start_date][]"].ToArray();
            var expEndDates = form["experience[end_date][]"].ToArray();
            var isCurrentArr = form["experience[is_current][]"].ToArray();
            var expDescriptions = form["experience[description][]"].ToArray();

            int expCount = new int[] { compNames.Length, positions.Length, expStartDates.Length, expEndDates.Length, isCurrentArr.Length, expDescriptions.Length }.Max();

            if (expCount == 0)
            {
                isValid = false;
                error = "At least one experience entry is required.";
                return totalYears;
            }

            for (int i = 0; i < expCount; i++)
            {
                string companyName = i < compNames.Length ? compNames[i]?.Trim() : "";
                string position = i < positions.Length ? positions[i]?.Trim() : "";
                string startDateStr = i < expStartDates.Length ? expStartDates[i]?.Trim() : "";
                string endDateStr = i < expEndDates.Length ? expEndDates[i]?.Trim() : "";
                bool isCurr = isCurrentArr.ElementAtOrDefault(i) == "true";
                string expDesc = i < expDescriptions.Length ? expDescriptions[i]?.Trim() : "";

                if (string.IsNullOrWhiteSpace(companyName) &&
                    string.IsNullOrWhiteSpace(position) &&
                    string.IsNullOrWhiteSpace(startDateStr) &&
                    string.IsNullOrWhiteSpace(endDateStr) &&
                    string.IsNullOrWhiteSpace(expDesc))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(position) || !DateTime.TryParse(startDateStr, out var startDt))
                {
                    isValid = false;
                    error = $"Both Company and Position are required for experience entry {i + 1}.";
                    break;
                }

                DateTime endDt;
                if (isCurr)
                {
                    endDt = DateTime.UtcNow;
                }
                else
                {
                    if (!DateTime.TryParse(endDateStr, out endDt))
                    {
                        isValid = false;
                        error = $"Invalid end date for experience row {i + 1}.";
                        break;
                    }
                }

                validCount++;
                double years = (endDt - startDt).TotalDays / 365;
                totalYears += years;

                var experience = new TblExperience
                {
                    UserId = userId,
                    CompanyName = companyName,
                    Position = position,
                    StartDate = startDt,
                    EndDate = isCurr ? (DateTime?)null : endDt,
                    IsCurrent = isCurr,
                    Years = validCount > 0 ? (decimal?)Math.Round(totalYears, 2) : null,
                    Description = expDesc,
                };
                _context.TblExperiences.Add(experience);
            }

            if (validCount == 0)
            {
                isValid = false;
                error = "At least one valid experience entry must be provided.";
            }

            return totalYears;
        }

        #endregion

    }
}
