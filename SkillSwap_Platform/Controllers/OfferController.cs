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
                    }
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

        // GET: /Offer/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Retrieve the user's offered skills (from TblUserSkills joined with TblSkills).
            var userSkills = await _context.TblUserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == userId && us.IsOffering)
                .Select(us => new { us.Skill.SkillId, us.Skill.SkillName })
                .ToListAsync();

            var model = new OfferCreateVM
            {
                UserSkills = userSkills.Select(s => new SelectListItem
                {
                    Value = s.SkillId.ToString(),
                    Text = s.SkillName
                }).ToList()
            };

            return View(model);
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
                var userSkills = await _context.TblUserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == userId && us.IsOffering)
                    .Select(us => new { us.Skill.SkillId, us.Skill.SkillName })
                    .ToListAsync();
                model.UserSkills = userSkills.Select(s => new SelectListItem
                {
                    Value = s.SkillId.ToString(),
                    Text = s.SkillName
                }).ToList();

                return View(model);
            }

            int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                // Create a new offer entity.
                var offer = new TblOffer
                {
                    UserId = currentUserId,
                    Title = model.Title,
                    Description = model.Description,
                    TokenCost = model.TokenCost,
                    TimeCommitmentDays = model.TimeCommitmentDays,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true,
                    Category = model.Category
                };

                // Save the offer first so that OfferID is generated.
                _context.TblOffers.Add(offer);
                await _context.SaveChangesAsync();

                // If a skill was selected from the dropdown, save it with the offer.
                if (model.SelectedSkillId.HasValue)
                {
                    // Here, we assume you want to store the chosen skill ID in one of the TblOffer columns.
                    // For example, if you have a column called SkillID_OfferOwner:
                    offer.SkillIdOfferOwner = model.SelectedSkillId.Value;
                }

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
                    // Store the JSON serialized version in the Offer record.
                    offer.Portfolio = Newtonsoft.Json.JsonConvert.SerializeObject(portfolioUrls);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Offer created successfully.";
                return RedirectToAction("Create", "Offer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer for user {UserId}", currentUserId);
                ModelState.AddModelError("", "An error occurred while creating your offer. Please try again.");
                return View(model);
            }
        }

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

        #endregion
    }
}