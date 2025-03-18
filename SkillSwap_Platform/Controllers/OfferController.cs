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
                            if (!ValidateFile(file, new[] { ".jpg", ".jpeg", ".png" }, 1 * 1024 * 1024, out string errorMsg))
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

        #region Edit Offer

        // GET: /Offer/Edit/{offerId}
        [HttpGet]
        public async Task<IActionResult> Edit(int offerId)
        {
            int userId = GetUserId();
            try
            {
                var offer = await _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .FirstOrDefaultAsync(o => o.OfferId == offerId && o.UserId == userId);
                if (offer == null)
                {
                    return NotFound("Offer not found");
                }

                // Deserialize portfolio JSON (if any)
                List<string> portfolioUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(offer.Portfolio))
                {
                    try
                    {
                        portfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(offer.Portfolio) ?? new List<string>();
                    }
                    catch { /* If deserialization fails, leave portfolioUrls empty */ }
                }

                // Create an edit model and prepopulate fields.
                var model = new OfferEditVM
                {
                    OfferId = offer.OfferId,
                    UserId = offer.UserId,
                    Title = offer.Title,
                    Description = offer.Description,
                    TokenCost = offer.TokenCost,
                    TimeCommitmentDays = offer.TimeCommitmentDays,
                    Category = offer.Category,
                    Portfolio = offer.Portfolio, // Stored as JSON string
                    FreelanceType = offer.FreelanceType,
                    RequiredSkillLevel = offer.RequiredSkillLevel,
                    RequiredLanguageId = offer.RequiredLanguageId,
                    RequiredLanguageLevel = offer.RequiredLanguageLevel,
                    // Parse comma-separated skill IDs if available.
                    SelectedSkillIds = string.IsNullOrWhiteSpace(offer.SkillIdOfferOwner)
                                       ? new List<int>()
                                       : offer.SkillIdOfferOwner.Split(',').Select(int.Parse).ToList()
                };

                PopulateDropdownsForEdit(model, userId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading offer for editing for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while loading the offer.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // POST: /Offer/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OfferEditVM model)
        {
            ModelState.Remove("UserSkills");
            ModelState.Remove("UserLanguages");
            ModelState.Remove("CategoryOptions");
            ModelState.Remove("FreelanceTypeOptions");
            ModelState.Remove("RequiredSkillLevelOptions");
            ModelState.Remove("RequiredLanguageLevelOptions");
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
                PopulateDropdownsForEdit(model, GetUserId());
                return View(model);
            }

            int userId = GetUserId();
            try
            {
                var offer = await _context.TblOffers.FirstOrDefaultAsync(o => o.OfferId == model.OfferId && o.UserId == userId);
                if (offer == null)
                {
                    return NotFound("Offer not found");
                }

                // Update the offer entity with values from the model.
                offer.Title = model.Title;
                offer.Description = model.Description;
                offer.TokenCost = model.TokenCost;
                offer.TimeCommitmentDays = model.TimeCommitmentDays;
                offer.Category = model.Category;
                offer.FreelanceType = model.FreelanceType;
                offer.RequiredSkillLevel = model.RequiredSkillLevel;
                offer.RequiredLanguageId = model.RequiredLanguageId;
                offer.RequiredLanguageLevel = model.RequiredLanguageLevel;

                // Store selected skill IDs as comma-separated.
                if (model.SelectedSkillIds != null && model.SelectedSkillIds.Any())
                {
                    offer.SkillIdOfferOwner = string.Join(",", model.SelectedSkillIds);
                }
                else
                {
                    offer.SkillIdOfferOwner = string.Empty;
                }

                // --- Merge Existing and New Portfolio Images ---
                // Start by deserializing the existing portfolio from the hidden field.
                var existingPortfolioUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(model.Portfolio))
                {
                    try
                    {
                        existingPortfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(model.Portfolio)
                                                ?? new List<string>();
                    }
                    catch
                    {
                        // Log the error if needed and proceed with an empty list.
                    }
                }

                // Deserialize current offer portfolio as fallback.
                var portfolioUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(offer.Portfolio))
                {
                    try
                    {
                        portfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(offer.Portfolio)
                                        ?? new List<string>();
                    }
                    catch { }
                }
                var mergedUrls = existingPortfolioUrls.Union(portfolioUrls).ToList();

                // If new portfolio files are provided, process them.
                if (model.PortfolioFiles != null && model.PortfolioFiles.Any())
                {
                    foreach (var file in model.PortfolioFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            if (!ValidateFile(file, new[] { ".jpg", ".jpeg", ".png" }, 1 * 1024 * 1024, out string errorMsg))
                            {
                                ModelState.AddModelError("PortfolioFiles", errorMsg);
                                PopulateDropdownsForEdit(model, userId);
                                return View(model);
                            }
                            string fileUrl = await UploadFileAsync(file, "portfolio");
                            mergedUrls.Add(fileUrl);

                            // Optionally, add new portfolio entry in tblOfferPortfolio.
                            var portfolio = new TblOfferPortfolio
                            {
                                OfferId = offer.OfferId,
                                FileUrl = fileUrl,
                                CreatedDate = DateTime.UtcNow
                            };
                            _context.TblOfferPortfolios.Add(portfolio);
                        }
                    }
                    // Update the Portfolio JSON field.
                    offer.Portfolio = Newtonsoft.Json.JsonConvert.SerializeObject(mergedUrls);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Offer updated successfully.";
                return RedirectToAction("Edit", "Offer", new { offerId = offer.OfferId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offer {OfferId} for user {UserId}", model.OfferId, userId);
                ModelState.AddModelError("", "An error occurred while updating your offer. Please try again.");
                PopulateDropdownsForEdit(model, userId);
                return View(model);
            }
        }

        #endregion

        #region Delete & Restore Offer

        // GET: /Offer/Delete/{offerId}
        [HttpGet]
        public async Task<IActionResult> Delete(int offerId)
        {
            int userId = GetUserId();
            try
            {
                var offer = await _context.TblOffers
                    .Include(o => o.TblOfferPortfolios)
                    .FirstOrDefaultAsync(o => o.OfferId == offerId && o.UserId == userId && !o.IsDeleted);
                if (offer == null)
                {
                    return NotFound("Offer not found or already deleted.");
                }

                // Prepare a view model for confirmation (you might reuse your OfferEditVM or create a specific DeleteOfferVM)
                var model = new OfferDeleteVM
                {
                    OfferId = offer.OfferId,
                    Title = offer.Title,
                    TokenCost = offer.TokenCost,
                    TimeCommitmentDays = offer.TimeCommitmentDays,
                    Category = offer.Category,
                    FreelanceType = offer.FreelanceType,
                    CreatedDate = offer.CreatedDate,
                    // Optionally, include first portfolio image (if available)
                    ThumbnailUrl = !string.IsNullOrWhiteSpace(offer.Portfolio)
                                    ? (Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(offer.Portfolio) ?? new List<string>()).FirstOrDefault()
                                    : null
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading delete confirmation for offer {OfferId} for user {UserId}", offerId, userId);
                TempData["ErrorMessage"] = "An error occurred while loading the offer.";
                return RedirectToAction("OfferList", "Offer");
            }
        }

        // POST: /Offer/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int offerId)
        {
            int userId = GetUserId();
            try
            {
                var offer = await _context.TblOffers.FirstOrDefaultAsync(o => o.OfferId == offerId && o.UserId == userId && !o.IsDeleted);
                if (offer == null)
                {
                    return NotFound("Offer not found or already deleted.");
                }

                // Mark as deleted (soft delete) and record the deletion date.
                offer.IsDeleted = true;
                offer.DeletedDate = DateTime.UtcNow;

                // Optionally, also set IsActive = false
                offer.IsActive = false;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Offer has been moved to deleted status. You can restore it within 15 days.";
                return RedirectToAction("OfferList", "Offer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer {OfferId} for user {UserId}", offerId, userId);
                TempData["ErrorMessage"] = "An error occurred while deleting the offer.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Offer/DeletedOffers
        [HttpGet]
        public async Task<IActionResult> DeletedOffers(int page = 1)
        {
            int userId = GetUserId();
            try
            {
                int pageSize = 5; // Adjust page size as needed.
                                  // Calculate the cutoff date (15 days ago)
                var cutoffDate = DateTime.UtcNow.AddDays(-15);

                // Get all deleted offers that are within the grace period.
                var query = _context.TblOffers
                    .Where(o => o.UserId == userId && o.IsDeleted && o.DeletedDate >= cutoffDate)
                    .OrderByDescending(o => o.DeletedDate);

                var totalOffers = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalOffers / pageSize);

                var offers = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to view model objects.
                var deletedOfferVMs = offers.Select(o => new OfferDeleteVM
                {
                    OfferId = o.OfferId,
                    Title = o.Title,
                    TokenCost = o.TokenCost,
                    TimeCommitmentDays = o.TimeCommitmentDays,
                    Category = o.Category,
                    FreelanceType = o.FreelanceType,
                    CreatedDate = o.CreatedDate,
                    DeletedDate = o.DeletedDate,
                    Portfolio = o.Portfolio,
                    // For ThumbnailUrl, if Portfolio is not empty, try to deserialize and get the first image.
                    ThumbnailUrl = !string.IsNullOrWhiteSpace(o.Portfolio)
                        ? (Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(o.Portfolio) ?? new List<string>()).FirstOrDefault()
                        : null
                }).ToList();

                var viewModel = new OfferRestoreListVM
                {
                    DeletedOffers = deletedOfferVMs,
                    CurrentPage = page,
                    TotalPages = totalPages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching deleted offers for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while loading deleted offers.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // POST: /Offer/Restore/{offerId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int offerId)
        {
            int userId = GetUserId();
            try
            {
                var offer = await _context.TblOffers.FirstOrDefaultAsync(o => o.OfferId == offerId && o.UserId == userId && o.IsDeleted);
                if (offer == null)
                {
                    return NotFound("Offer not found or cannot be restored.");
                }

                // Check if within grace period (15 days)
                if (offer.DeletedDate.HasValue && offer.DeletedDate.Value.AddDays(15) < DateTime.UtcNow)
                {
                    TempData["ErrorMessage"] = "The offer can no longer be restored as the grace period has expired.";
                    return RedirectToAction("CancelledOffers", "Offer");
                }

                // Restore the offer.
                offer.IsDeleted = false;
                offer.DeletedDate = null;
                offer.IsActive = true; // Optionally, mark as active.

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Offer has been successfully restored.";
                return RedirectToAction("OfferList", "Offer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring offer {OfferId} for user {UserId}", offerId, userId);
                TempData["ErrorMessage"] = "An error occurred while restoring the offer.";
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

        #region Active & Inactive Offers

        // GET: /Offer/ActiveOffers
        [HttpGet]
        public async Task<IActionResult> ActiveOffers(int page = 1)
        {
            try
            {
                int pageSize = 5;
                int userId = GetUserId();

                // Active offers only (exclude deleted)
                var query = _context.TblOffers
                    .Where(o => o.UserId == userId && !o.IsDeleted && o.IsActive)
                    .OrderByDescending(o => o.CreatedDate);

                int totalOffers = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalOffers / pageSize);

                var offers = await query.Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .Include(o => o.TblOfferPortfolios)
                                        .ToListAsync();

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
                _logger.LogError(ex, "Error fetching active offers for user.");
                TempData["ErrorMessage"] = "An error occurred while loading active offers.";
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Offer/InactiveOffers
        [HttpGet]
        public async Task<IActionResult> InactiveOffers(int page = 1)
        {
            try
            {
                int pageSize = 5;
                int userId = GetUserId();

                // Inactive offers only (exclude deleted)
                var query = _context.TblOffers
                    .Where(o => o.UserId == userId && !o.IsDeleted && !o.IsActive)
                    .OrderByDescending(o => o.CreatedDate);

                int totalOffers = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalOffers / pageSize);

                var offers = await query.Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .Include(o => o.TblOfferPortfolios)
                                        .ToListAsync();

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
                _logger.LogError(ex, "Error fetching inactive offers for user.");
                TempData["ErrorMessage"] = "An error occurred while loading inactive offers.";
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

            // Fetch all languages (remove userId filtering)
            var userLanguages = _context.TblLanguages
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
                new SelectListItem { Value = "Beginner", Text = "Beginner" },
                new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                new SelectListItem { Value = "Advanced", Text = "Advanced" },
            };

            model.RequiredLanguageLevelOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Basic", Text = "Basic" },
                new SelectListItem { Value = "Conversational", Text = "Conversational" },
                new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                new SelectListItem { Value = "Proficient", Text = "Proficient" },};

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

        private void PopulateDropdownsForEdit(OfferEditVM model, int userId)
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
            var allLanguages = _context.TblLanguages
                .Select(l => new { l.LanguageId, l.Language })
                .Distinct()
                .ToList();

            model.UserLanguages = allLanguages.Select(l => new SelectListItem
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
                new SelectListItem { Value = "Beginner", Text = "Beginner" },
                new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                new SelectListItem { Value = "Proficient", Text = "Proficient" },
            };

            model.RequiredLanguageLevelOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Basic", Text = "Basic" },
                new SelectListItem { Value = "Conversational", Text = "Conversational" },
                new SelectListItem { Value = "Fluent", Text = "Fluent" },
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

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            else
                throw new Exception("Invalid user identifier claim.");
        }

        #endregion
    }
}