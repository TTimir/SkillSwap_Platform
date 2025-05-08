using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.ExchangeVM;
using SkillSwap_Platform.Services;
using SkillSwap_Platform.Services.NotificationTrack;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserOfferManageController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<UserOfferManageController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IFileService _fileService;
        private readonly INotificationService _notif;
        public UserOfferManageController(SkillSwapDbContext context, ILogger<UserOfferManageController> logger, IWebHostEnvironment env, IFileService fileService, INotificationService notif)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _fileService = fileService;
            _notif = notif;
        }

        #region Create Offer

        // GET: /Offer/Create
        public async Task<IActionResult> Create()
        {
            int userId = GetUserId();
            try
            {
                // Retrieve the user to fetch their willing skills
                var currentUser = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);

                // Get willing skills from the user (assumes a comma-separated string is stored in currentUser.WillingSkills)
                var willingSkillOptions = new List<SelectListItem>();
                if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.DesiredSkillAreas))
                {
                    var skills = currentUser.DesiredSkillAreas.Split(',')
                                    .Select(s => s.Trim())
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToList();

                    willingSkillOptions = skills.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
                }

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

                // Create device options (you can add more as needed)
                var deviceOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Desktop", Text = "Desktop" },
                    new SelectListItem { Value = "Mobile", Text = "Mobile" },
                    new SelectListItem { Value = "iOS", Text = "iOS" },
                    new SelectListItem { Value = "Linux", Text = "Linux" }
                };

                var collaborationMethod = new List<SelectListItem>
                {
                    new SelectListItem { Value = "In-Person", Text = "In-Person" },
                    new SelectListItem { Value = "Online", Text = "Online" }
                };

                var model = new OfferCreateVM
                {
                    UserSkills = userSkills.Select(s => new SelectListItem
                    {
                        Value = s.SkillId.ToString(),
                        Text = s.SkillName
                    }).ToList(),

                    // Populate static options.
                    FreelanceTypeOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Full Time", Text = "Full Time" },
                        new SelectListItem { Value = "Part Time", Text = "Part Time" }
                    },

                    RequiredSkillLevelOptions = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Beginner", Text = "Beginner" },
                        new SelectListItem { Value = "Intermediate", Text = "Intermediate" },
                        new SelectListItem { Value = "Advanced", Text = "Advanced" },
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
                    },
                    DeviceOptions = deviceOptions,
                    CollaborationOptions = collaborationMethod,
                    WillingSkillOptions = willingSkillOptions
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
            if (string.IsNullOrWhiteSpace(model.Address)
                 && (!model.Latitude.HasValue || !model.Longitude.HasValue))
            {
                ModelState.AddModelError(
                    nameof(model.Address),
                    "Please either type in your address or click the location button to fetch your GPS coordinates."
                );
            }

            ModelState.Remove("UserSkills");
            ModelState.Remove("DeviceOptions");
            ModelState.Remove("CollaborationOptions");
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
                int userId = GetUserId();
                PopulateDropdowns(model, userId); // Repopulate dropdowns here
                return View(model);
            }

            int currentUserId = GetUserId();
            // Check if the user already has 6 offers
            var userOfferCount = await _context.TblOffers.CountAsync(o => o.UserId == currentUserId && !o.IsDeleted);
            if (userOfferCount >= 5)
            {
                ViewBag.ErrorMessage = "You have reached the maximum number of offers allowed (5). You cannot create more than 5 offers.";
                PopulateDropdowns(model, currentUserId);
                return View(model);
            }
            // Wrap the multi-step process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var SkillIds = (model.SelectedSkillIds != null && model.SelectedSkillIds.Any())
                                    ? string.Join(",", model.SelectedSkillIds)
                                    : string.Empty;

                    // Join the selected device values into a comma-separated string.
                    string devices = (model.SelectedDevices != null && model.SelectedDevices.Any())
                        ? string.Join(",", model.SelectedDevices)
                        : string.Empty;

                    // Create a new offer entity.
                    var offer = new TblOffer
                    {
                        UserId = currentUserId,
                        Title = model.Title,
                        TokenCost = model.TokenCost,
                        TimeCommitmentDays = model.TimeCommitmentDays,
                        Category = model.Category,
                        CreatedDate = DateTime.UtcNow,
                        IsActive = true,
                        FreelanceType = model.FreelanceType,
                        ScopeOfWork = model.ScopeOfWork,
                        AssistanceRounds = model.AssistanceRounds,
                        CollaborationMethod = model.CollaborationMethod,
                        RequiredSkillLevel = model.RequiredSkillLevel,
                        SkillIdOfferOwner = SkillIds,
                        Device = devices,
                        Tools = model.Tools,
                        WillingSkill = model.SelectedWillingSkill,
                        Address = model.Address,
                        Latitude = model.Latitude,
                        Longitude = model.Longitude,
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
                                if (!_fileService.ValidateFile(file, new[] { ".jpg", ".jpeg", ".png" }, 1 * 1024 * 1024, out string errorMsg))
                                {
                                    ModelState.AddModelError("PortfolioFiles", errorMsg);
                                    return View(model);
                                }
                                string fileUrl = await _fileService.UploadFileAsync(file, "portfolio");
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

                        if (model.Faqs == null || !model.Faqs.Any())
                        {
                            // tell the user they forgot to add at least one FAQ
                            ViewBag.ErrorMessage = "Please add at least one question & answer pair in the FAQ section.";

                            // re-populate all your dropdowns, etc.
                            var userId = GetUserId();
                            PopulateDropdowns(model, userId);

                            // short-circuit: render the same view with the error message
                            return View(model);
                        }

                        //  — save FAQs —
                        if (model.Faqs != null && model.Faqs.Any())
                        {
                            foreach (var fq in model.Faqs)
                            {
                                // skip any empty entries
                                if (string.IsNullOrWhiteSpace(fq.Question) || string.IsNullOrWhiteSpace(fq.Answer))
                                    continue;

                                _context.TblOfferFaqs.Add(new TblOfferFaq
                                {
                                    OfferId = offer.OfferId,
                                    Question = fq.Question,
                                    Answer = fq.Answer,
                                    CreatedDate = DateTime.UtcNow
                                });
                            }
                            await _context.SaveChangesAsync();
                        }
                    }

                    // log notification:
                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = GetUserId(),
                        Title = "Swap Created",
                        Message = "You successfully created and published your offer.",
                        Url = Url.Action("OfferList", "UserOfferManage"),
                    });

                    // Commit the transaction when all steps succeed.
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Offer created successfully.";
                    return RedirectToAction("Create", "UserOfferManage");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating offer for user {UserId}", currentUserId);
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "An error occurred while creating your offer. Please try again.");
                    return RedirectToAction("EP500", "EP");
                }
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
                    return RedirectToAction("EP404", "EP");
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

                // Create a list of selected devices by splitting the stored comma-separated string.
                var selectedDevices = string.IsNullOrWhiteSpace(offer.Device)
                    ? new List<string>()
                    : offer.Device.Split(',').Select(s => s.Trim()).ToList();

                // Prepare device options.
                var deviceOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Desktop", Text = "Desktop" },
                    new SelectListItem { Value = "Mobile", Text = "Mobile" },
                    new SelectListItem { Value = "iOS", Text = "iOS" },
                    new SelectListItem { Value = "Linux", Text = "Linux" }
                };

                var collaborationMethod = new List<SelectListItem>
                 {
                     new SelectListItem { Value = "In=Person", Text = "InPerson" },
                     new SelectListItem { Value = "Online", Text = "Online" }
                 };

                // Create an edit model and prepopulate fields.
                var model = new OfferEditVM
                {
                    OfferId = offer.OfferId,
                    UserId = offer.UserId,
                    Title = offer.Title,
                    TokenCost = offer.TokenCost,
                    TimeCommitmentDays = offer.TimeCommitmentDays,
                    Category = offer.Category,
                    Portfolio = offer.Portfolio, // Stored as JSON string
                    FreelanceType = offer.FreelanceType,
                    ScopeOfWork = offer.ScopeOfWork,
                    AssistanceRounds = offer.AssistanceRounds,
                    CollaborationMethod = offer.CollaborationMethod,
                    RequiredSkillLevel = offer.RequiredSkillLevel,
                    // Parse comma-separated skill IDs if available.
                    SelectedSkillIds = string.IsNullOrWhiteSpace(offer.SkillIdOfferOwner)
                                       ? new List<int>()
                                       : offer.SkillIdOfferOwner.Split(',').Select(int.Parse).ToList(),
                    SelectedDevices = selectedDevices,
                    Tools = offer.Tools,
                    DeviceOptions = deviceOptions,
                    CollaborationOptions = collaborationMethod,
                    SelectedWillingSkill = offer.WillingSkill,
                    Address = offer.Address,
                    Longitude = offer.Longitude,
                    Latitude = offer.Latitude
                };

                var existingFaqs = await _context.TblOfferFaqs
                    .Where(f => f.OfferId == offerId && !f.IsDeleted)
                    .OrderBy(f => f.CreatedDate)
                    .ToListAsync();

                model.Faqs = existingFaqs
                    .Select(f => new OfferFaqVM
                    {
                        FaqId = f.FaqId,
                        OfferId = f.OfferId,
                        Question = f.Question,
                        Answer = f.Answer
                    })
                    .ToList();

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
            if (string.IsNullOrWhiteSpace(model.Address)
                && (!model.Latitude.HasValue || !model.Longitude.HasValue))
            {
                ModelState.AddModelError(
                    nameof(model.Address),
                    "Please either type in your address or click the location button to fetch your GPS coordinates."
                );
            }
            ModelState.Remove("UserSkills");
            ModelState.Remove("UserLanguages");
            ModelState.Remove("CategoryOptions");
            ModelState.Remove("FreelanceTypeOptions");
            ModelState.Remove("RequiredSkillLevelOptions");
            ModelState.Remove("RequiredLanguageLevelOptions");
            ModelState.Remove("FinalPortfolioOrder");
            ModelState.Remove("DeviceOptions");
            ModelState.Remove("Tools");
            ModelState.Remove("DeviceOptions");
            ModelState.Remove("CollaborationOptions");

            // If there are no new files provided, remove ModelState errors for PortfolioFiles
            if (model.PortfolioFiles == null || !model.PortfolioFiles.Any())
            {
                ModelState.Remove("PortfolioFiles");
            }
            else if (!string.IsNullOrWhiteSpace(model.Portfolio) && ModelState.ContainsKey("PortfolioFiles"))
            {
                // Clear errors if existing portfolio is present
                ModelState["PortfolioFiles"].Errors.Clear();
            }

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
            // Wrap the multi-step process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var offer = await _context.TblOffers.FirstOrDefaultAsync(o => o.OfferId == model.OfferId && o.UserId == userId);
                    if (offer == null)
                    {
                        return RedirectToAction("EP500", "EP");
                    }

                    // Update the offer entity with values from the model.
                    offer.Title = model.Title;
                    offer.TokenCost = model.TokenCost;
                    offer.TimeCommitmentDays = model.TimeCommitmentDays;
                    offer.Category = model.Category;
                    offer.FreelanceType = model.FreelanceType;
                    offer.RequiredSkillLevel = model.RequiredSkillLevel;
                    offer.ScopeOfWork = model.ScopeOfWork;
                    offer.AssistanceRounds = model.AssistanceRounds;
                    offer.Device = model.SelectedDevices != null && model.SelectedDevices.Any()
                           ? string.Join(",", model.SelectedDevices)
                           : string.Empty;
                    offer.Tools = model.Tools;
                    offer.CollaborationMethod = model.CollaborationMethod;
                    offer.WillingSkill = model.SelectedWillingSkill;
                    offer.Address = model.Address;
                    offer.Latitude = model.Latitude;
                    offer.Longitude = model.Longitude;

                    // Store selected skill IDs as comma-separated.
                    if (model.SelectedSkillIds != null && model.SelectedSkillIds.Any())
                    {
                        offer.SkillIdOfferOwner = string.Join(",", model.SelectedSkillIds);
                    }
                    else
                    {
                        offer.SkillIdOfferOwner = string.Empty;
                    }

                    // Deserialize the existing portfolio from the hidden field (updated by the client)
                    var updatedPortfolioUrls = new List<string>();
                    if (!string.IsNullOrWhiteSpace(model.Portfolio))
                    {
                        try
                        {
                            updatedPortfolioUrls = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(model.Portfolio)
                                                    ?? new List<string>();
                        }
                        catch
                        {
                            // Log error if needed; continue with empty list.
                        }
                    }

                    // If new portfolio files are provided, process them.
                    if (model.PortfolioFiles != null && model.PortfolioFiles.Any())
                    {
                        // (Optional) Remove old portfolio entries from tblOfferPortfolio if you wish to replace them.
                        var existingPortfolios = _context.TblOfferPortfolios.Where(p => p.OfferId == offer.OfferId).ToList();
                        foreach (var entry in existingPortfolios)
                        {
                            _context.TblOfferPortfolios.Remove(entry);
                        }
                        updatedPortfolioUrls.Clear(); // Clear old URLs if replacing completely.

                        foreach (var file in model.PortfolioFiles)
                        {
                            if (file != null && file.Length > 0)
                            {
                                if (!_fileService.ValidateFile(file, new[] { ".jpg", ".jpeg", ".png" }, 1 * 1024 * 1024, out string errorMsg))
                                {
                                    ModelState.AddModelError("PortfolioFiles", errorMsg);
                                    PopulateDropdownsForEdit(model, userId);
                                    return View(model);
                                }
                                string fileUrl = await _fileService.UploadFileAsync(file, "portfolio");
                                updatedPortfolioUrls.Add(fileUrl);

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
                        offer.Portfolio = Newtonsoft.Json.JsonConvert.SerializeObject(updatedPortfolioUrls);
                    }
                    else
                    {
                        // No new files provided.
                        // Use the updated portfolio URLs from the hidden field (which reflects removals).
                        offer.Portfolio = Newtonsoft.Json.JsonConvert.SerializeObject(updatedPortfolioUrls);

                        // Optionally, update tblOfferPortfolios to reflect removals.
                        var currentPortfolios = _context.TblOfferPortfolios.Where(p => p.OfferId == offer.OfferId).ToList();
                        foreach (var entry in currentPortfolios)
                        {
                            if (!updatedPortfolioUrls.Contains(entry.FileUrl))
                            {
                                _context.TblOfferPortfolios.Remove(entry);
                            }
                        }
                    }

                    // 1) load current FAQs from DB
                    var currentFaqs = await _context.TblOfferFaqs
                        .Where(f => f.OfferId == model.OfferId && !f.IsDeleted)
                        .ToListAsync();

                    // 2) delete any removed in the form
                    var removed = currentFaqs
                        .Where(f => !model.Faqs.Any(vm => vm.FaqId == f.FaqId))
                        .ToList();
                    removed.ForEach(f => f.IsDeleted = true);

                    if (model.Faqs == null || !model.Faqs.Any())
                    {
                        ViewBag.ErrorMessage = "Please add at least one question & answer pair in the FAQ section.";
                        PopulateDropdownsForEdit(model, GetUserId());
                        return View(model);
                    }

                    // 3) add or update each posted FAQ
                    foreach (var vm in model.Faqs)
                    {
                        if (vm.FaqId > 0)
                        {
                            // update
                            var ent = currentFaqs.First(f => f.FaqId == vm.FaqId);
                            ent.Question = vm.Question;
                            ent.Answer = vm.Answer;
                            ent.UpdatedDate = DateTime.UtcNow;
                        }
                        else
                        {
                            // new
                            _context.TblOfferFaqs.Add(new TblOfferFaq
                            {
                                OfferId = model.OfferId,
                                Question = vm.Question,
                                Answer = vm.Answer,
                                CreatedDate = DateTime.UtcNow
                            });
                        }
                    }

                    await _context.SaveChangesAsync();

                    // log notification:
                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = GetUserId(),
                        Title = "Swap Updated",
                        Message = "You successfully updated your offer.",
                        Url = Url.Action("OfferList", "UserOfferManage"),
                    });

                    // Commit the transaction when all steps succeed.
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Offer updated successfully.";
                    return RedirectToAction("Edit", "UserOfferManage", new { offerId = offer.OfferId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating offer {OfferId} for user {UserId}", model.OfferId, userId);
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "An error occurred while updating your offer. Please try again.");
                    PopulateDropdownsForEdit(model, userId);
                    return RedirectToAction("EP500", "EP");
                }
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
                return RedirectToAction("EP500", "EP");
            }
        }

        // POST: /Offer/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int offerId)
        {
            int userId = GetUserId();
            // Wrap the multi-step process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
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

                    // log notification:
                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = GetUserId(),
                        Title = "Swap Deleted",
                        Message = "You successfully deleted and removed your offer.",
                        Url = Url.Action("DeletedOffers", "UserOfferManage"),
                    });

                    // Commit the transaction when all steps succeed.
                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = "Offer has been moved to deleted status. You can restore it within 15 days.";
                    return RedirectToAction("OfferList", "UserOfferManage");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting offer {OfferId} for user {UserId}", offerId, userId);
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "An error occurred while deleting the offer.";
                    return RedirectToAction("EP500", "EP");
                }
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
            // Wrap the multi-step process in a transaction.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
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
                        return RedirectToAction("CancelledOffers", "UserOfferManage");
                    }

                    // Restore the offer.
                    offer.IsDeleted = false;
                    offer.DeletedDate = null;
                    offer.IsActive = true; // Optionally, mark as active.

                    await _context.SaveChangesAsync();

                    // log notification:
                    await _notif.AddAsync(new TblNotification
                    {
                        UserId = GetUserId(),
                        Title = "Swap Restored",
                        Message = "You successfully restored your offer.",
                        Url = Url.Action("OfferList", "UserOfferManage"),
                    });

                    // Commit the transaction when all steps succeed.
                    await transaction.CommitAsync(); 
                    TempData["SuccessMessage"] = "Offer has been successfully restored.";
                    return RedirectToAction("OfferList", "UserOfferManage");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring offer {OfferId} for user {UserId}", offerId, userId);
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "An error occurred while restoring the offer.";
                    return RedirectToAction("EP500", "EP");
                }
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
                int userId = GetUserId();
                int pageSize = 5; // 5 offers per page

                var totalOffers = await _context.TblOffers.Where(o => o.UserId == userId && !o.IsDeleted).CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalOffers / pageSize);

                var offers = await _context.TblOffers
                    .Where(o => o.UserId == userId && !o.IsDeleted)
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

        #region FAQ Management

        // GET: /Offer/FAQ/List/{offerId}
        [HttpGet]
        public async Task<IActionResult> FaqList(int offerId)
        {
            try
            {
                var faqs = await _context.TblOfferFaqs
                    .Where(f => f.OfferId == offerId && !f.IsDeleted)
                    .OrderBy(f => f.CreatedDate)
                    .Select(f => new OfferFaqVM
                    {
                        FaqId = f.FaqId,
                        OfferId = f.OfferId,
                        Question = f.Question,
                        Answer = f.Answer
                    })
                    .ToListAsync();

                return PartialView("_FaqList", faqs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading FAQs for offer {OfferId}", offerId);
                return RedirectToAction("EP500", "EP");
            }
        }

        // GET: /Offer/FAQ/Create/{offerId}
        [HttpGet]
        public IActionResult CreateFaq(int offerId)
        {
            return PartialView("_FaqForm", new OfferFaqVM { OfferId = offerId });
        }

        // POST: /Offer/FAQ/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFaq(OfferFaqVM model)
        {
            if (!ModelState.IsValid)
                return PartialView("_FaqForm", model);

            try
            {
                var faq = new TblOfferFaq
                {
                    OfferId = model.OfferId,
                    Question = model.Question,
                    Answer = model.Answer,
                    CreatedDate = DateTime.UtcNow
                };
                _context.TblOfferFaqs.Add(faq);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating FAQ for offer {OfferId}", model.OfferId);
                return StatusCode(500, "Error creating FAQ.");
            }
        }

        // GET: /Offer/FAQ/Edit/{faqId}
        [HttpGet]
        public async Task<IActionResult> EditFaq(int faqId)
        {
            try
            {
                var faq = await _context.TblOfferFaqs.FindAsync(faqId);
                if (faq == null || faq.IsDeleted) return NotFound();

                var vm = new OfferFaqVM
                {
                    FaqId = faq.FaqId,
                    OfferId = faq.OfferId,
                    Question = faq.Question,
                    Answer = faq.Answer
                };
                return PartialView("_FaqForm", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading FAQ {FaqId} for edit", faqId);
                return StatusCode(500, "Unable to load FAQ.");
            }
        }

        // POST: /Offer/FAQ/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFaq(OfferFaqVM model)
        {
            if (!ModelState.IsValid)
                return PartialView("_FaqForm", model);

            try
            {
                var faq = await _context.TblOfferFaqs.FindAsync(model.FaqId);
                if (faq == null || faq.IsDeleted) return NotFound();

                faq.Question = model.Question;
                faq.Answer = model.Answer;
                faq.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FAQ {FaqId}", model.FaqId);
                return StatusCode(500, "Error updating FAQ.");
            }
        }

        // POST: /Offer/FAQ/Delete/{faqId}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFaq(int faqId)
        {
            try
            {
                var faq = await _context.TblOfferFaqs.FindAsync(faqId);
                if (faq == null || faq.IsDeleted) return NotFound();

                faq.IsDeleted = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting FAQ {FaqId}", faqId);
                return StatusCode(500, "Error deleting FAQ.");
            }
        }

        #endregion

        #region Helper Methods
        private void PopulateDropdowns(OfferCreateVM model, int userId)
        {
            // Retrieve the current user to get their willing skills.
            var currentUser = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.DesiredSkillAreas))
            {
                var willingSkills = currentUser.DesiredSkillAreas.Split(',')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                model.WillingSkillOptions = willingSkills.Select(ws => new SelectListItem
                {
                    Value = ws,
                    Text = ws
                }).ToList();
            }
            else
            {
                model.WillingSkillOptions = new List<SelectListItem>();
            }

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

            model.DeviceOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Desktop", Text = "Desktop" },
                new SelectListItem { Value = "Mobile", Text = "Mobile" },
                new SelectListItem { Value = "iOS", Text = "iOS" },
                new SelectListItem { Value = "Linux", Text = "Linux" }
            };

            model.CollaborationOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "In-Person", Text = "In-Person" },
                new SelectListItem { Value = "Online", Text = "Online" }
            };
        }

        private void PopulateDropdownsForEdit(OfferEditVM model, int userId)
        {
            var currentUser = _context.TblUsers.FirstOrDefault(u => u.UserId == userId);
            if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.DesiredSkillAreas))
            {
                var willingSkills = currentUser.DesiredSkillAreas.Split(',')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                model.WillingSkillOptions = willingSkills.Select(ws => new SelectListItem
                {
                    Value = ws,
                    Text = ws
                }).ToList();
            }
            else
            {
                model.WillingSkillOptions = new List<SelectListItem>();
            }

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

            model.DeviceOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Desktop", Text = "Desktop" },
                new SelectListItem { Value = "Mobile", Text = "Mobile" },
                new SelectListItem { Value = "iOS", Text = "iOS" },
                new SelectListItem { Value = "Linux", Text = "Linux" }
            };

            model.CollaborationOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "In-Person", Text = "In-Person" },
                new SelectListItem { Value = "Online", Text = "Online" }
            };
        }


        [HttpPost]
        public IActionResult ToggleActive(int offerId, bool isActive)
        {
            try
            {
                var userId = GetUserId();
                var offer = _context.TblOffers.FirstOrDefault(o => o.OfferId == offerId && o.UserId == userId);
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