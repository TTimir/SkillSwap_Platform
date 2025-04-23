using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using SkillSwap_Platform.Services.NotificationTrack;
using System.Linq;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserProfileController> _logger;
        private readonly INotificationService _notif;

        public UserProfileController(SkillSwapDbContext context, ILogger<UserProfileController> logger, INotificationService notif)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
            _notif = notif;
        }

        #region Public Profile
        // GET: /UserProfile/PublicProfileByUsername
        [AllowAnonymous]
        public async Task<IActionResult> PublicProfileByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("EP404", "EP");
            }

            try
            {
                // Look up the user by username (case-insensitive using ToLower).
                var user = await _context.TblUsers
                    .Include(u => u.TblEducations)
                    .Include(u => u.TblExperiences)
                    .Include(u => u.TblLanguages)
                    .Include(u => u.TblUserCertificateUsers)
                    .Include(u => u.TblUserSkills)
                        .ThenInclude(us => us.Skill)
                    .Include(u => u.TblReviewReviewees)
                        .ThenInclude(r => r.TblReviewReplies)
                        .ThenInclude(rep => rep.ReplierUser)
                    .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

                if (user == null)
                {
                    return RedirectToAction("EP404", "EP");
                }

                // Fetch all available skills from tblSkills.
                var allSkills = await _context.TblSkills.Select(s => s.SkillName).ToListAsync();

                // Extract offered skills stored as comma-separated values.
                var offeredSkills = user.OfferedSkillAreas?
                        .Split(',')
                        .Select(s => s.Trim())
                        .ToList() ?? new List<string>();

                // Map global skills to SkillVM objects.
                var skillList = user.TblUserSkills
                    .Where(us => us.Skill != null)
                    .Select(us => new SkillVM
                    {
                        Name = us.Skill.SkillName,
                        // Mark as offered based on the IsOffering flag from the relationship.
                        IsOffered = us.IsOffering
                    })
                    .ToList();

                // Calculate the recommended percentage from reviews.
                double recommendedPercentage = 0;
                int totalReviews = user.TblReviewReviewees.Count();
                if (totalReviews > 0)
                {
                    int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);
                    recommendedPercentage = positiveReviews * 100.0 / totalReviews;
                }

                // Fetch the most recent completed exchange (job) for the user.
                var lastCompletedExchange = await _context.TblExchanges
                    .Where(e => e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId)
                    .Where(e => e.Status == "Completed")
                    .OrderByDescending(e => e.LastStatusChangeDate)
                    .FirstOrDefaultAsync();

                int lastDeliveryDays = lastCompletedExchange != null
                    ? (DateTime.UtcNow - lastCompletedExchange.LastStatusChangeDate).Days
                    : -1;

                // Load offers created by the user.
                var offers = await _context.TblOffers
                    .Where(o => o.UserId == user.UserId)
                    .Include(o => o.TblOfferPortfolios)
                    .Where(o => o.IsActive)
                    .ToListAsync();

                // Get the distinct skill IDs from the offers.
                var skillIds = offers
                    .Select(o => o.SkillIdOfferOwner)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct()
                    .ToList();

                // Retrieve the matching skills from tblSkills using in-memory filtering.
                var skills = _context.TblSkills
                    .AsEnumerable()
                    .Where(s => skillIds.Contains(s.SkillId.ToString()))
                    .ToList();

                var reviewAggregates = await _context.TblReviews
                    .GroupBy(r => r.OfferId)
                    .Select(g => new {
                        OfferId = g.Key,
                        ReviewCount = g.Count(),
                        AverageRating = g.Average(r => r.Rating)
                    })
                    .ToDictionaryAsync(x => x.OfferId);

                // Map each offer to its view model.
                var offerVMs = offers.Select(o =>
                {
                    // Default values if no reviews are available.
                    int reviewCount = 0;
                    double avgRating = 0;

                    // Look for aggregated review data.
                    if (reviewAggregates.TryGetValue(o.OfferId, out var agg))
                    {
                        reviewCount = agg.ReviewCount;
                        avgRating = agg.AverageRating;
                    }

                    return new OfferDetailsVM
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        Designation = user.Designation,
                        Category = o.Category,
                        Description = o.Description,
                        TimeCommitmentDays = o.TimeCommitmentDays,
                        PortfolioImages = o.TblOfferPortfolios.Select(p => p.FileUrl).ToList(),
                        SkillName = string.Join(", ", o.SkillIdOfferOwner
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => skills.FirstOrDefault(s => s.SkillId.ToString() == id)?.SkillName)
                            .Where(name => !string.IsNullOrEmpty(name))),
                        AverageRating = avgRating,
                        ReviewCount = reviewCount
                    };
                }).ToList();

                // Retrieve the total exchanges for the user (as offer owner or as the other party)
                var totalExchanges = await _context.TblExchanges
                    .CountAsync(e => e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId);

                var exchangesWithResponse = await _context.TblExchanges
                    .Where(e => e.ResponseDate.HasValue)
                    .ToListAsync();

                var averageResponseTime = exchangesWithResponse.Any()
                    ? exchangesWithResponse.Average(e =>
                        (e.ResponseDate.HasValue && e.RequestDate.HasValue)
                            ? (e.ResponseDate.Value - e.RequestDate.Value).TotalHours
                            : 0)
                    : 0;

                int activeExchangeCount = await _context.TblExchanges
                    .CountAsync(e => (e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId) &&
                     e.Status != null &&
                     e.Status.Trim().ToLower() != "completed");

                // For reviews, use the reviews associated with the user.
                int totalReviewsUser = user.TblReviewReviewees.Count();
                double avgRatingUser = totalReviewsUser > 0 ? user.TblReviewReviewees.Average(r => r.Rating) : 0;

                // Build the public user profile view model.
                var model = new UserProfileVM
                {
                    User = user,
                    Educations = user.TblEducations.OrderByDescending(e => e.StartDate).ToList(),
                    Experiences = user.TblExperiences.OrderByDescending(e => e.StartDate).ToList(),
                    Languages = user.TblLanguages.ToList(),
                    Certificates = user.TblUserCertificateUsers.ToList(),
                    LastExchangeDays = lastDeliveryDays,
                    RecommendedPercentage = recommendedPercentage,
                    Skills = skillList,
                    Offers = offerVMs,
                    TotalExchanges = totalExchanges,
                    AverageResponseTime = averageResponseTime.ToString("F2"),
                    ActiveExchangeCount = activeExchangeCount,
                    Reviews = user.TblReviewReviewees,
                    ReviewCount = totalReviewsUser,
                    AverageRating = avgRatingUser
                };

                return View("PublicProfile", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public profile for username {username}", username);
                TempData["ErrorMessage"] = "An error occurred while loading the profile.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Authenticated Profile (Index & Edit)
        // GET: /UserProfile/Index
        public async Task<IActionResult> Index()
        {
            int userId;
            try
            {
                userId = GetUserId();
            }
            catch (Exception)
            {
                // No valid user id found; redirect to login.
                return RedirectToAction("Login", "Home");
            }

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
                        .ThenInclude(r => r.TblReviewReplies)
                        .ThenInclude(rep => rep.ReplierUser)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return RedirectToAction("EP404", "EP");
                }

                // Fetch all available skills from tblSkills
                var allSkills = await _context.TblSkills.Select(s => s.SkillName).ToListAsync();
                // Extract offered skills stored in "OfferedSkillAreas" column (comma-separated)
                var offeredSkills = user.OfferedSkillAreas?
                        .Split(',')
                        .Select(s => s.Trim())
                        .ToList() ?? new List<string>();

                // Map skills to SkillVM objects
                var skillList = user.TblUserSkills
                        .Where(us => us.Skill != null)
                        .Select(us => new SkillVM
                        {
                            Name = us.Skill.SkillName,
                            IsOffered = us.IsOffering
                        })
                        .ToList();


                // Calculate the recommended percentage from reviews.
                double recommendedPercentage = 0;
                int totalReviews = user.TblReviewReviewees.Count();
                if (totalReviews > 0)
                {
                    int positiveReviews = user.TblReviewReviewees.Count(r => r.Rating >= 4);
                    recommendedPercentage = positiveReviews * 100.0 / totalReviews;
                }

                // Fetch the most recent completed exchange (job)
                var lastCompletedExchange = await _context.TblExchanges
                    //.Where(e => e.RequesterId == userId || e.LastStatusChangedBy == userId) // Job done by the user
                    //.Where(e => e.Status == "Completed") // Only completed transactions
                    //.OrderByDescending(e => e.LastStatusChangeDate) // Get the most recent one
                    .FirstOrDefaultAsync();

                // Calculate days since last completed job
                int lastDeliveryDays = lastCompletedExchange != null
                    ? (DateTime.UtcNow - lastCompletedExchange.LastStatusChangeDate).Days
                    : -1; // Default if no completed jobs

                // Load offers created by the user
                var offers = await _context.TblOffers
                    .Where(o => o.UserId == userId)
                    .Include(o => o.TblOfferPortfolios)
                    .Where(o => o.IsActive)
                    .ToListAsync();

                // Get the distinct skill IDs from the offers.
                var skillIds = offers
                    .Select(o => o.SkillIdOfferOwner)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct()
                    .ToList();

                // Retrieve the matching skills from tblskill.
                var skills = _context.TblSkills
                    .AsEnumerable() // Switches to in-memory filtering
                    .Where(s => skillIds.Contains(s.SkillId.ToString()))
                    .ToList();

                var reviewAggregates = await _context.TblReviews
                   .GroupBy(r => r.OfferId)
                   .Select(g => new {
                       OfferId = g.Key,
                       ReviewCount = g.Count(),
                       AverageRating = g.Average(r => r.Rating)
                   })
                   .ToDictionaryAsync(x => x.OfferId);

                // Map each offer to its view model.
                var offerVMs = offers.Select(o =>
                {
                    // Default values if no reviews are available.
                    int reviewCount = 0;
                    double avgRating = 0;

                    // Look for aggregated review data.
                    if (reviewAggregates.TryGetValue(o.OfferId, out var agg))
                    {
                        reviewCount = agg.ReviewCount;
                        avgRating = agg.AverageRating;
                    }

                    return new OfferDetailsVM
                    {
                        OfferId = o.OfferId,
                        Title = o.Title,
                        Designation = user.Designation,
                        Category = o.Category,
                        Description = o.Description,
                        TimeCommitmentDays = o.TimeCommitmentDays,
                        PortfolioImages = o.TblOfferPortfolios.Select(p => p.FileUrl).ToList(),
                        SkillName = string.Join(", ", o.SkillIdOfferOwner
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => skills.FirstOrDefault(s => s.SkillId.ToString() == id)?.SkillName)
                            .Where(name => !string.IsNullOrEmpty(name))),
                        AverageRating = avgRating,
                        ReviewCount = reviewCount
                    };
                }).ToList();

                // Retrieve the total exchanges for the user (as offer owner or as the other party)
                var totalExchanges = await _context.TblExchanges
                    .CountAsync(e => e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId);

                var exchangesWithResponse = await _context.TblExchanges
                    .Where(e => e.ResponseDate.HasValue)
                    .ToListAsync();

                var averageResponseTime = exchangesWithResponse.Any()
                    ? exchangesWithResponse.Average(e =>
                        (e.ResponseDate.HasValue && e.RequestDate.HasValue)
                            ? (e.ResponseDate.Value - e.RequestDate.Value).TotalHours
                            : 0)
                    : 0;

                int activeExchangeCount = await _context.TblExchanges
                    .CountAsync(e => (e.OfferOwnerId == user.UserId || e.OtherUserId == user.UserId) &&
                     e.Status != null &&
                     e.Status.Trim().ToLower() != "completed");

                // For reviews, use the reviews associated with the user.
                int totalReviewsUser = user.TblReviewReviewees.Count();
                double avgRatingUser = totalReviewsUser > 0 ? user.TblReviewReviewees.Average(r => r.Rating) : 0;

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
                    Skills = skillList,
                    Offers = offerVMs,
                    TotalExchanges = totalExchanges,
                    AverageResponseTime = averageResponseTime.ToString("F2"),
                    ActiveExchangeCount = activeExchangeCount,
                    Reviews = user.TblReviewReviewees,
                    ReviewCount = totalReviewsUser,
                    AverageRating = avgRatingUser
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /UserProfile/EditProfile
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            int userId;
            try
            {
                userId = GetUserId();
            }
            catch (Exception)
            {
                return RedirectToAction("Login", "Home");
            }

            var user = await _context.TblUsers
                .Include(u => u.TblUserSkills).ThenInclude(us => us.Skill)
                .Include(u => u.TblEducations)
                .Include(u => u.TblExperiences)
                .Include(u => u.TblUserCertificateUsers)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return RedirectToAction("EP404", "EP");

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
            if (!userSkillEditList.Any())
            {
                userSkillEditList.Add(new SkillEditVM());
            }


            // Map existing education entries.
            var educationEntries = user.TblEducations
                .OrderByDescending(e => e.StartDate)
                .Select(e => new EducationVM
                {
                    EducationId = e.EducationId,
                    Degree = e.Degree,
                    DegreeName = e.DegreeName,
                    Institution = e.InstitutionName,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Description = e.Description,
                    IsDeleted = false
                })
                .ToList();
            if (!educationEntries.Any())
            {
                educationEntries.Add(new EducationVM());
            }

            var experienceEntries = user.TblExperiences
               .OrderByDescending(e => e.StartDate)
               .Select(e => new ExperienceVM
               {
                   ExperienceId = e.ExperienceId,
                   CompanyName = e.CompanyName,
                   Position = e.Position,
                   StartDate = e.StartDate,
                   EndDate = e.EndDate,
                   Description = e.Description,
                   IsexpDeleted = false
               }).ToList();
            if (!experienceEntries.Any())
            {
                experienceEntries.Add(new ExperienceVM());
            }

            var certificateEntries = user.TblUserCertificateUsers
                .Select(c => new CertificateVM
                {
                    CertificateId = c.CertificateId,
                    CertificateName = c.CertificateName,
                    CertificateFrom = c.CertificateFrom,
                    VerificationId = c.VerificationId,
                    CertificateFilePath = c.CertificateFilePath,
                    CertificateDate = c.SubmittedDate,
                    IsApproved = c.IsApproved,
                    IscertDeleted = false
                }).ToList();
            if (!certificateEntries.Any())
            {
                certificateEntries.Add(new CertificateVM());
            }

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
                    Location = user.CurrentLocation,
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
                EducationEntries = educationEntries,
                ExperienceEntries = experienceEntries,
                CertificateEntries = certificateEntries
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

        #endregion

        #region Update Profile (Transactional)
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
            for (int i = 0; i < model.CertificateEntries.Count; i++)
            {
                var cert = model.CertificateEntries[i];

                // If no new file is uploaded and no file exists, add a model error.
                if (cert.CertificateFile == null && string.IsNullOrEmpty(cert.CertificateFilePath))
                {
                    ModelState.Remove($"CertificateEntries[{i}].CertificateFile");
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

            int userId;
            try
            {
                userId = GetUserId();
            }
            catch (Exception)
            {
                return RedirectToAction("Login", "Home");
            }

            // Wrap the entire update process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await UpdatePersonalDetailsAsync(model.PersonalDetails, userId);
                    await UpdateSkillsAsync(model.Skills, userId);
                    await UpdateEducationAsync(model.EducationEntries, userId);
                    await UpdateExperienceAsync(model.ExperienceEntries, userId);
                    await UpdateCertificatesAsync(model.CertificateEntries, userId);

                    var changes = await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Now update the user’s OfferedSkillAreas in TblUsers and sync the TblUserSkills flags.
                    var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.OfferedSkillAreas = model.Skills.OfferedSkillSummary;
                        user.DesiredSkillAreas = model.Skills.WillingSkillSummary;
                        await _context.SaveChangesAsync();

                        // If nothing changed, let the user know; otherwise a success message.
                        if (changes == 0)
                        {
                            TempData["SuccessMessage"] = "Looks like nothing changed, your profile is already up to date.";
                        }
                        else
                        {
                            // log notification only if there was something to save
                            await _notif.AddAsync(new TblNotification
                            {
                                UserId = userId,
                                Title = "Profile updated",
                                Message = "You successfully updated your profile.",
                                Url = Url.Action("Index", "UserProfile")
                            });
                            TempData["SuccessMessage"] = "Profile updated successfully.";
                        }

                        // Synchronize the IsOffering flag on each TblUserSkill.
                        await UpdateUserOfferSkillFlagsAsync(userId, user.OfferedSkillAreas);
                    }

                    TempData["SuccessMessage"] = "Profile updated successfully.";
                    return RedirectToAction("EditProfile", "UserProfile");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                    ModelState.AddModelError("", "An error occurred while updating your profile. Please try again.");
                    return RedirectToAction("EP500", "EP");
                }
            }
        }
        #endregion

        #region Helper Methods

        #region Update Personal Details
        private async Task UpdatePersonalDetailsAsync(EditPersonalDetailsVM personal, int userId)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.FirstName = personal.FirstName;
                user.LastName = personal.LastName;
                user.UserName = personal.UserName;
                user.Email = personal.Email;
                user.PersonalWebsite = personal.PersonalWebsite;
                user.CurrentLocation = personal.Location;
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

                    // Prepare folders & filename
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profile");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid() + Path.GetExtension(personal.ProfileImageFile.FileName);
                    string savePath = Path.Combine(uploadsFolder, uniqueFileName);


                    using (var inStream = personal.ProfileImageFile.OpenReadStream())
                    using (var image = Image.Load(inStream))
                    {
                        image.Mutate(x => x.Resize(330, 300));
                        // You can tweak quality here if you like:
                        var encoder = new JpegEncoder { Quality = 85 };
                        await image.SaveAsync(savePath, encoder);
                    }
                    user.ProfileImageUrl = $"/uploads/profile/{uniqueFileName}";
                }
            }
        }
        #endregion

        #region Update Skills
        private async Task UpdateSkillsAsync(EditSkillVM skills, int userId)
        {
            try
            {
                if (skills == null)
                    throw new ArgumentNullException(nameof(skills));

                // Update the user's offered and desired skill summary fields.
                var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user != null)
                {
                    user.OfferedSkillAreas = skills.OfferedSkillSummary;
                    user.DesiredSkillAreas = skills.WillingSkillSummary;
                }

                // Remove all existing user-skill assignments.
                var existingUserSkills = await _context.TblUserSkills.Where(us => us.UserId == userId).ToListAsync();
                _context.TblUserSkills.RemoveRange(existingUserSkills);

                // Create a cache for global skills (keyed by skill name in lowercase).
                var globalSkillsCache = new Dictionary<string, TblSkill>(StringComparer.OrdinalIgnoreCase);
                var newGlobalSkills = new List<TblSkill>();

                // Build the offered skills list from the offered summary (comma-separated)
                var offeredSkillList = (skills.OfferedSkillSummary ?? "")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList();

                // Process each skill provided from the view model.
                foreach (var skill in skills.AllSkills)
                {
                    // Skip if the skill row is empty.
                    if (string.IsNullOrWhiteSpace(skill.SkillName))
                        continue;

                    if (!skill.ProficiencyLevel.HasValue)
                    {
                        throw new Exception($"Please select a proficiency level for {skill.SkillName}.");
                    }

                    // Determine the category to use (if "Other" and a custom category is provided, then use that).
                    string categoryToUse = (string.Equals(skill.Category, "Other", StringComparison.OrdinalIgnoreCase) &&
                                             !string.IsNullOrWhiteSpace(skill.CustomCategory))
                                            ? skill.CustomCategory
                                            : skill.Category;

                    TblSkill globalSkill = null;
                    string skillKey = skill.SkillName.ToLowerInvariant();

                    // Check the cache first.
                    if (globalSkillsCache.TryGetValue(skillKey, out globalSkill))
                    {
                        // Optionally update the category.
                        globalSkill.SkillCategory = categoryToUse;
                    }
                    else
                    {
                        // Look up the global skill in the database (case-insensitive).
                        globalSkill = await _context.TblSkills
                                                    .FirstOrDefaultAsync(s => s.SkillName.ToLower() == skillKey);

                        if (globalSkill == null)
                        {
                            // If not found, create a new global skill.
                            globalSkill = new TblSkill
                            {
                                SkillName = skill.SkillName,
                                SkillCategory = categoryToUse
                            };
                            _context.TblSkills.Add(globalSkill);
                            newGlobalSkills.Add(globalSkill);
                        }
                        else
                        {
                            // Update the category if necessary.
                            globalSkill.SkillCategory = categoryToUse;
                        }
                        // Cache the looked-up or created global skill.
                        globalSkillsCache[skillKey] = globalSkill;
                    }
                }

                // Save changes for any new global skills so that their SkillId is generated.
                if (newGlobalSkills.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Second phase: Create user-skill relationships using the now-populated SkillIds.
                foreach (var skill in skills.AllSkills)
                {
                    if (string.IsNullOrWhiteSpace(skill.SkillName))
                        continue;

                    string skillKey = skill.SkillName.ToLowerInvariant();
                    if (globalSkillsCache.TryGetValue(skillKey, out var globalSkill))
                    {
                        // Mark IsOffering true if the offered skills list contains this skill.
                        bool isOffering = offeredSkillList.Contains(skillKey);
                        _context.TblUserSkills.Add(new TblUserSkill
                        {
                            UserId = userId,
                            SkillId = globalSkill.SkillId,  // Now non-zero because we've saved new skills.
                            ProficiencyLevel = skill.ProficiencyLevel,
                            IsOffering = isOffering
                        });
                    }
                }


                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating skills for user {UserId}", userId);
                throw; // Rethrow exception so the outer transaction can handle rollback.
            }
        }
        #endregion

        #region Update Education
        private async Task UpdateEducationAsync(IEnumerable<EducationVM> educationEntries, int userId)
        {
            // Remove all existing education entries for the user.
            var existingExp = await _context.TblEducations.Where(e => e.UserId == userId).ToListAsync();
            // Process each entry coming from the view.
            foreach (var edu in educationEntries)
            {
                // If the entry is marked for deletion...
                if (edu.IsDeleted)
                {
                    // If it exists in DB (has an EducationId), remove it.
                    if (edu.EducationId > 0)
                    {
                        var recordToRemove = existingExp.FirstOrDefault(e => e.EducationId == edu.EducationId);
                        if (recordToRemove != null)
                        {
                            _context.TblEducations.Remove(recordToRemove);
                        }
                    }
                    // Skip further processing for this row.
                    continue;
                }

                // Skip entries that are incomplete.
                if (string.IsNullOrWhiteSpace(edu.Degree) ||
                    string.IsNullOrWhiteSpace(edu.DegreeName) ||
                    string.IsNullOrWhiteSpace(edu.Institution))
                {
                    continue;
                }

                // Check if this entry already exists.
                var existingRecord = existingExp.FirstOrDefault(e => e.EducationId == edu.EducationId);

                if (existingRecord != null)
                {
                    // Update existing record.
                    existingRecord.Degree = edu.Degree;
                    existingRecord.DegreeName = edu.DegreeName;
                    existingRecord.InstitutionName = edu.Institution;
                    existingRecord.StartDate = edu.StartDate;
                    existingRecord.EndDate = edu.EndDate;
                    existingRecord.Description = edu.Description;
                }
                else
                {
                    // Create new education record.
                    var newRecord = new TblEducation
                    {
                        UserId = userId,
                        Degree = edu.Degree,
                        DegreeName = edu.DegreeName,
                        InstitutionName = edu.Institution,
                        StartDate = edu.StartDate,
                        EndDate = edu.EndDate,
                        Description = edu.Description
                    };
                    _context.TblEducations.Add(newRecord);
                }
            }

            // Optionally update a summary field in the TblUsers table.
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                var summaries = educationEntries
                    .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.DegreeName) && !string.IsNullOrWhiteSpace(e.Institution))
                    .Select(e => $"{e.DegreeName} from {e.Institution}");
                user.Education = string.Join("; ", summaries);
            }
        }
        #endregion

        #region Update Experience
        private async Task UpdateExperienceAsync(IEnumerable<ExperienceVM> experienceEntries, int userId)
        {
            var existingExp = await _context.TblExperiences.Where(e => e.UserId == userId).ToListAsync();

            double totalYears = 0;
            foreach (var exp in experienceEntries)
            {
                if (exp.IsexpDeleted)
                {
                    if (exp.ExperienceId > 0)
                    {
                        var recordToRemove = existingExp.FirstOrDefault(e => e.ExperienceId == exp.ExperienceId);
                        if (recordToRemove != null)
                        {
                            _context.TblExperiences.Remove(recordToRemove);
                        }
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(exp.CompanyName) || string.IsNullOrWhiteSpace(exp.Position))
                    continue;

                // Use the provided start date; if EndDate is null, use DateTime.Now.
                DateTime startDate = (DateTime)exp.StartDate;
                DateTime expEnd = exp.EndDate ?? DateTime.Now;

                // Calculate the duration (in years) for this experience.
                double duration = (expEnd - startDate).TotalDays / 365;
                totalYears += duration;
                var existingRecord = existingExp.FirstOrDefault(e => e.ExperienceId == exp.ExperienceId);
                if (existingRecord != null)
                {
                    existingRecord.CompanyName = exp.CompanyName;
                    existingRecord.Position = exp.Position;
                    existingRecord.StartDate = exp.StartDate;
                    existingRecord.EndDate = exp.EndDate;
                    existingRecord.Description = exp.Description;
                    existingRecord.Years = (decimal?)Math.Round(duration, 2);
                    // You can recalculate total years if needed.
                }
                else
                {
                    var newRecord = new TblExperience
                    {
                        UserId = userId,
                        CompanyName = exp.CompanyName,
                        Position = exp.Position,
                        StartDate = exp.StartDate,
                        EndDate = exp.EndDate,
                        Description = exp.Description,
                        Years = (decimal?)Math.Round(duration, 2)
                    };
                    _context.TblExperiences.Add(newRecord);
                }
            }
            // Optionally, update a summary field in TblUsers if you store total experience.
            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                // For example, recalculate total years:
                user.Experience = totalYears.ToString("0.0") + " years";
            }
        }
        #endregion

        #region Update Certificates
        private async Task UpdateCertificatesAsync(IEnumerable<CertificateVM> certificateEntries, int userId)
        {
            // Remove certificates marked as deleted.
            var existingCerts = await _context.TblUserCertificates.Where(c => c.UserId == userId).ToListAsync();
            foreach (var cert in certificateEntries)
            {
                if (cert.IscertDeleted)
                {
                    if (cert.CertificateId > 0)
                    {
                        var recordToRemove = existingCerts.FirstOrDefault(c => c.CertificateId == cert.CertificateId);
                        if (recordToRemove != null)
                        {
                            _context.TblUserCertificates.Remove(recordToRemove);
                        }
                    }
                    continue;
                }

                // If certificate exists, update its details; otherwise, create a new record.
                var existingRecord = existingCerts.FirstOrDefault(c => c.CertificateId == cert.CertificateId);
                if (existingRecord != null)
                {
                    existingRecord.CertificateName = cert.CertificateName;
                    existingRecord.VerificationId = cert.VerificationId;
                    existingRecord.CertificateFrom = cert.CertificateFrom;
                    existingRecord.CompleteDate = cert.CertificateDate;
                    // If a new certificate file is uploaded, update the file path.
                    if (cert.CertificateFile != null && cert.CertificateFile.Length > 0)
                    {
                        if (!ValidateFile(cert.CertificateFile, new[] { ".pdf", ".jpg", ".jpeg", ".png" }, 5 * 1024 * 1024, out string certError))
                        {
                            throw new Exception(certError);
                        }
                        string fileUrl = await UploadFileAsync(cert.CertificateFile, "certificates");
                        existingRecord.CertificateFilePath = fileUrl;
                        existingRecord.SubmittedDate = DateTime.Now;
                        existingRecord.IsApproved = false;
                    }
                }
                else
                {
                    // Only add new certificate if a file is provided.
                    if (cert.CertificateFile != null && cert.CertificateFile.Length > 0)
                    {
                        if (!ValidateFile(cert.CertificateFile, new[] { ".pdf", ".jpg", ".jpeg", ".png" }, 5 * 1024 * 1024, out string certError))
                        {
                            throw new Exception(certError);
                        }
                        string fileUrl = await UploadFileAsync(cert.CertificateFile, "certificates");
                        var newCert = new TblUserCertificate
                        {
                            UserId = userId,
                            CertificateName = cert.CertificateName,
                            CertificateFrom = cert.CertificateFrom,
                            CompleteDate = cert.CertificateDate,
                            VerificationId = cert.VerificationId,
                            CertificateFilePath = fileUrl,
                            SubmittedDate = DateTime.Now,
                            IsApproved = false
                        };
                        _context.TblUserCertificates.Add(newCert);
                    }
                }
            }
        }
        #endregion

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

        private async Task UpdateUserOfferSkillFlagsAsync(int userId, string updatedOfferedSkillAreas)
        {
            var offeredSkillsList = !string.IsNullOrWhiteSpace(updatedOfferedSkillAreas)
                ? updatedOfferedSkillAreas
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList()
                : new List<string>();

            var userSkills = await _context.TblUserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == userId)
                .ToListAsync();

            foreach (var userSkill in userSkills)
            {
                // Set IsOffering to true if the global skill name exists in the offered skills list.
                userSkill.IsOffering = offeredSkillsList.Contains(userSkill.Skill.SkillName.ToLowerInvariant());
            }

            await _context.SaveChangesAsync();
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            else
                throw new Exception("Invalid user identifier claim.");
        }

    }
}