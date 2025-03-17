using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class OfferController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<OfferController> _logger;
        private readonly IWebHostEnvironment _env;

        public OfferController(SkillSwapDbContext context, ILogger<OfferController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
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
                        var user = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
                        if (user != null)
                        {
                            ViewData["UserProfileImage"] = user.ProfileImageUrl;
                            user.LastActive = DateTime.UtcNow;
                            _context.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating LastActive for user {UserId}", userId);
                        context.Result = RedirectToAction("EP500", "EP");
                    }
                }
                else
                {
                    // If we cannot parse the user ID, redirect to EP500.
                    context.Result = RedirectToAction("EP500", "EP");
                }
            }

            //int userId = GetUserId();
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

        #region Create Offer

        // GET: /Offer/Create
        public async Task<IActionResult> Create()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId) || userId == 0)
            {
                return RedirectToAction("EP500", "EP");
            }

            try
            {
                // Retrieve the user's offered skills (from TblUserSkills joined with TblSkills).
                var userSkills = await _context.TblUserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == userId && us.IsOffering)
                .Select(us => new { us.Skill.SkillId, us.Skill.SkillName })
                .ToListAsync();

                // Retrieve the user's languages (from TblLanguages).
                var userLanguages = await _context.TblLanguages
                    .Where(l => l.UserId == userId)
                    .Select(l => new { l.LanguageId, l.Language })
                    .Distinct()
                    .ToListAsync();

                var model = new OfferCreateVM
                {
                    UserSkills = userSkills.Select(s => new SelectListItem
                    {
                        Value = s.SkillId.ToString(),
                        Text = s.SkillName
                    }).ToList(),

                    // Populate language dropdown.
                    UserLanguages = userLanguages.Select(l => new SelectListItem
                    {
                        Value = l.LanguageId.ToString(),
                        Text = l.Language
                    }).ToList(),

                    // Populate static options.
                    FreelanceTypeOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Full Time", Text = "Full Time" },
                        new SelectListItem { Value = "Part Time", Text = "Part Time" }
                    },

                    RequiredSkillLevelOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Entry", Text = "Entry" },
                        new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                        new SelectListItem { Value = "Advanced", Text = "Advanced" },
                    },

                    RequiredLanguageLevelOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Basic", Text = "Basic" },
                        new SelectListItem { Value = "Conversational", Text = "Conversational" },
                        new SelectListItem { Value = "Fluent", Text = "Fluent" },
                    },

                    CategoryOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Graphics & Design", Text = "Graphics & Design" },
                        new SelectListItem { Value = "Digital Marketing", Text = "Digital Marketing" },
                        new SelectListItem { Value = "Writing & Translation", Text = "Writing & Translation" },
                        new SelectListItem { Value = "Video & Animation", Text = "Video & Animation" },
                        new SelectListItem { Value = "Music & Audio", Text = "Music & Audio" },
                        new SelectListItem { Value = "Programming & Tech", Text = "Programming & Tech" },
                        new SelectListItem { Value = "Business", Text = "Business" },
                        new SelectListItem { Value = "Lifestyle", Text = "Lifestyle" },
                        new SelectListItem { Value = "Trending", Text = "Trending" }
                    }
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create offer page for user {UserId}", userId);
                return RedirectToAction("EP500", "EP");
            }
        }

        // POST: Offer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OfferCreateVM model)
        {
            ModelState.Remove("UserSkills");
            if (!ModelState.IsValid)
            {
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogError("ModelState error in {Key}: {Error}", state.Key, error.ErrorMessage);
                    }
                }
                // Re-populate the user skills list if validation fails.
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                PopulateDropdowns(model, userId); // Repopulate dropdowns here
                return View(model);
            }

            int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            // Check if the user already has 6 offers
            var userOfferCount = await _context.TblOffers.CountAsync(o => o.UserId == currentUserId);
            if (userOfferCount >= 6)
            {
                ViewBag.ErrorMessage = "You have reached the maximum number of offers allowed (6). You cannot create more than 6 offers.";
                PopulateDropdowns(model, currentUserId);
                return View(model);
            }
            try
            {
                var SkillIds = (model.SelectedSkillIds != null && model.SelectedSkillIds.Any())
                                ? string.Join(",", model.SelectedSkillIds)
                                : string.Empty;

                // Create a new offer entity.
                var offer = new TblOffer
                {
                    UserId = currentUserId,
                    Title = model.Title,
                    Description = model.Description,
                    TokenCost = model.TokenCost,
                    TimeCommitmentDays = model.TimeCommitmentDays,
                    Category = model.Category,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true,
                    FreelanceType = model.FreelanceType,
                    RequiredSkillLevel = model.RequiredSkillLevel,
                    RequiredLanguageId = model.RequiredLanguageId,
                    RequiredLanguageLevel = model.RequiredLanguageLevel,
                    SkillIdOfferOwner = SkillIds
                };

                // Convert selected skill IDs into a comma-separated string and store it.
                if (model.SelectedSkillIds != null && model.SelectedSkillIds.Any())
                {
                    offer.SkillIdOfferOwner = string.Join(",", model.SelectedSkillIds);
                }

                // Save the offer first to generate a valid OfferID
                _context.TblOffers.Add(offer);
                await _context.SaveChangesAsync();

                // If portfolio files are provided, process them.
                if (model.PortfolioFiles != null && model.PortfolioFiles.Any())
                {
                    // We store portfolio as a JSON array (or you could use a delimited string).
                    var portfolioUrls = new List<string>();
                    foreach (var file in model.PortfolioFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            if (!ValidateFile(file, new[] { ".jpg", ".jpeg", ".png", ".pdf" }, 5 * 1024 * 1024, out string errorMsg))
                            {
                                ModelState.AddModelError("PortfolioFiles", errorMsg);
                                return View(model);
                            }
                            string fileUrl = await UploadFileAsync(file, "portfolio");
                            portfolioUrls.Add(fileUrl);

                            // Optionally, also save each portfolio file in tblOfferPortfolio:
                            var portfolio = new TblOfferPortfolio
                            {
                                OfferId = offer.OfferId, // Note: OfferID is generated after saving; you might need to save offer first.
                                FileUrl = fileUrl,
                                CreatedDate = DateTime.UtcNow
                            };
                            _context.TblOfferPortfolios.Add(portfolio);
                        }
                    }
                    // Optionally, update the offer's Portfolio JSON field.
                    offer.Portfolio = Newtonsoft.Json.JsonConvert.SerializeObject(portfolioUrls);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Offer created successfully.";
                return RedirectToAction("Create", "Offer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer for user {UserId}", currentUserId);
                ModelState.AddModelError("", "An error occurred while creating your offer. Please try again.");
                return RedirectToAction("EP500", "EP");
            }
        }

        #endregion

        #region Offer List

        // GET: /Offer/List
        [HttpGet]
        public async Task<IActionResult> OfferList(int page = 1)
        {
            try
            {
                int pageSize = 5; // 5 offers per page
                var totalOffers = await _context.TblOffers.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalOffers / pageSize);

                var offers = await _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .OrderByDescending(o => o.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Build the view model.
                var viewModel = new OfferDetailsVM
                {
                    Offers = offers,
                    CurrentPage = page,
                    TotalPages = totalPages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offers for user.");
                TempData["ErrorMessage"] = "An error occurred while loading offers.";
                return RedirectToAction("EP500", "EP");
            }
        }

        #endregion

        #region Helper Methods

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
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", folderName);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/uploads/{folderName}/{uniqueFileName}";
        }

        private void PopulateDropdowns(OfferCreateVM model, int userId)
        {
            // Populate UserSkills
            var userSkills = _context.TblUserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == userId && us.IsOffering)
                .Select(us => new { us.Skill.SkillId, us.Skill.SkillName })
                .ToList();

            model.UserSkills = userSkills.Select(s => new SelectListItem
            {
                Value = s.SkillId.ToString(),
                Text = s.SkillName
            }).ToList();

            // Populate languages
            var userLanguages = _context.TblLanguages
                .Where(l => l.UserId == userId)
                .Select(l => new { l.LanguageId, l.Language })
                .Distinct()
                .ToList();

            model.UserLanguages = userLanguages.Select(l => new SelectListItem
            {
                Value = l.LanguageId.ToString(),
                Text = l.Language
            }).ToList();

            // Populate static options.
            model.FreelanceTypeOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Full Time", Text = "Full Time" },
                new SelectListItem { Value = "Part Time", Text = "Part Time" }
            };

            model.RequiredSkillLevelOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Entry", Text = "Entry" },
                new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                new SelectListItem { Value = "Advanced", Text = "Advanced" },
            };

            model.RequiredLanguageLevelOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Basic", Text = "Basic" },
                new SelectListItem { Value = "Conversational", Text = "Conversational" },
                new SelectListItem { Value = "Fluent", Text = "Fluent" },
                new SelectListItem { Value = "Native", Text = "Native" }
            };

            model.CategoryOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Graphics & Design", Text = "Graphics & Design" },
                new SelectListItem { Value = "Digital Marketing", Text = "Digital Marketing" },
                new SelectListItem { Value = "Writing & Translation", Text = "Writing & Translation" },
                new SelectListItem { Value = "Video & Animation", Text = "Video & Animation" },
                new SelectListItem { Value = "Music & Audio", Text = "Music & Audio" },
                new SelectListItem { Value = "Programming & Tech", Text = "Programming & Tech" },
                new SelectListItem { Value = "Business", Text = "Business" },
                new SelectListItem { Value = "Lifestyle", Text = "Lifestyle" },
                new SelectListItem { Value = "Trending", Text = "Trending" }
            };
        }

        [HttpPost]
        public IActionResult ToggleActive(int offerId, bool isActive)
        {
            try
            {
                var offer = _context.TblOffers.FirstOrDefault(o => o.OfferId == offerId);
                if (offer == null)
                {
                    return Json(new { success = false, message = "Offer not found" });
                }

                offer.IsActive = isActive;
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling active state for offer {OfferId}", offerId);
                return Json(new { success = false, message = "An error occurred." });
            }
        }

        #endregion
    }
}