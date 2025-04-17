using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.ResourceVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.HelperClass;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ResourceSharingController : Controller
    {
        private readonly SkillSwapDbContext _dbContext;
        private readonly ILogger<ResourceSharingController> _logger;
        private readonly string _uploadFolder;

        public ResourceSharingController(SkillSwapDbContext dbContext, ILogger<ResourceSharingController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "resources");
        }

        /// <summary>
        /// Displays a selection page where the user can choose the offer (exchange context) to share the resource with.
        /// </summary>
        public async Task<IActionResult> SelectOffer(int page = 1)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                int pageSize = 10;

                // Query for the total count of offers
                var totalOffers = await _dbContext.TblExchanges
                    .Where(e => (e.OfferOwnerId ?? 0) == currentUserId || (e.OtherUserId ?? 0) == currentUserId)
                    .CountAsync();

                // Retrieve a paged list of offers/exchanges
                var offers = await _dbContext.TblExchanges
                    .Where(e => (e.OfferOwnerId ?? 0) == currentUserId || (e.OtherUserId ?? 0) == currentUserId)
                    .OrderBy(e => e.ExchangeDate) // or other criteria
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new OfferOption
                    {
                        OfferId = e.OfferId,
                        ExchangeId = e.ExchangeId,
                        OfferTitle = e.Offer.Title,
                        OfferImageUrl = !string.IsNullOrEmpty(e.Offer.Portfolio)
                            ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(e.Offer.Portfolio).FirstOrDefault()
                            : "/template_assets/images/default-offer.png",
                        TokenCost = (from c in _dbContext.TblContracts
                                     where c.OfferId == e.OfferId
                                     orderby c.ContractId descending
                                     select c.TokenOffer).FirstOrDefault(),
                        Status = e.Status,
                        InitiatedDate = e.ExchangeDate,
                        ModeOfLearning = e.Offer.CollaborationMethod,
                        Category = e.Offer.Category,
                        ReceivedCount = _dbContext.TblResources.Count(r => r.ExchangeId == e.ExchangeId
                            && r.OwnerUserId == (currentUserId == (e.OfferOwnerId ?? 0) ? e.OtherUserId.Value : e.OfferOwnerId.Value))
                    })
                    .ToListAsync();

                var viewModel = new ResourceSelectionVM
                {
                    CurrentUserId = currentUserId,
                    OfferOptions = offers,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalOffers = totalOffers,
                    TotalPages = (int)Math.Ceiling(totalOffers / (double)pageSize)
                };

                return View("SelectOffer", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SelectOffer.");
                TempData["ErrorMessage"] = "An error occurred while selecting an offer.";
                return RedirectToAction("Index", "ExchangeDashboard");
            }
        }

        /// <summary>
        /// Displays the resource input form for a selected offer.
        /// </summary>
        [HttpGet]
        public IActionResult Create(int exchangeId, int offerId)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var viewModel = new ResourceInputVM
                {
                    OwnerUserId = currentUserId,
                    ExchangeId = exchangeId,
                    OfferId = offerId
                };

                ViewData["ExchangeId"] = viewModel.ExchangeId;
                ViewData["OfferId"] = viewModel.OfferId;

                return View("Create", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error displaying resource input form.");
                TempData["ErrorMessage"] = "An error occurred while loading the resource sharing form.";
                return RedirectToAction("SelectOffer");
            }
        }

        /// <summary>
        /// Processes the resource sharing form submission.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ResourceInputVM model)
        {
            try
            {
                ModelState.Remove("ExchangeId");
                ModelState.Remove("OfferId"); 
                ModelState.Remove("File");
                ModelState.Remove("InputUrl");
                if (!ModelState.IsValid)
                {
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogError("ModelState error in field '{Field}': {ErrorMessage}", state.Key, error.ErrorMessage);
                        }
                    }
                }

                // If the resource is not a Link, ensure a file is provided.
                if (model.ResourceType != "Link")
                {
                    if (model.File == null || model.File.Length <= 0)
                    {
                        TempData["ErrorMessage"] =  "No file selected.";
                        return View("Create", model);
                    }
                }

                string resourceUrl = string.Empty;

                // For "Link" resource type, use the InputUrl value.
                if (model.ResourceType.Equals("Link", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(model.InputUrl))
                    {
                        TempData["ErrorMessage"] = "Please provide a valid URL.";
                        return View("Create", model);
                    }

                    // Validate the provided URL.
                    if (!Uri.TryCreate(model.InputUrl.Trim(), UriKind.Absolute, out Uri? validatedUri) ||
                        (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
                    {
                        TempData["ErrorMessage"] = "The provided URL is invalid. Please enter a valid HTTP or HTTPS URL.";
                        return View("Create", model);
                    }

                    resourceUrl = model.InputUrl.Trim();
                }
                else
                {
                    // For "Image" or "File", ensure a file is provided.
                    if (model.File == null || model.File.Length <= 0)
                    {
                        TempData["ErrorMessage"] = "No file selected.";
                        return View("Create", model);
                    }

                    // Perform additional validation for file size based on file extension.
                    long maxFileSize;
                    string[] codeExtensions = new[] { ".cs", ".js", ".html", ".css", ".py", ".java", ".cpp", ".c", ".php", ".rb", ".go" };
                    string extension = Path.GetExtension(model.File.FileName)?.ToLowerInvariant() ?? string.Empty;
                    string[] videoExtensions = new[] { ".mp4", ".mov", ".avi", ".wmv", ".mkv" };
                    if (codeExtensions.Contains(extension))
                    {
                        maxFileSize = 100 * 1024 * 1024;    // 100 MB for code
                    }
                    else if (videoExtensions.Contains(extension))
                    {
                        maxFileSize = 500 * 1024 * 1024;    // 500 MB for videos
                    }
                    else
                    {
                        maxFileSize = 5 * 1024 * 1024;      // 5 MB for images & other files
                    }
                    if (model.File.Length > maxFileSize)
                    {
                        TempData["ErrorMessage"] =
                            $"File size exceeds the allowed limit of {maxFileSize / (1024 * 1024)} MB.";
                        return View("Create", model);
                    }

                    try
                    {
                        // After file is uploaded, get the physical file path.
                        string savedFilePath = await ResourceFileHelper.UploadFileAsync(model.File, _uploadFolder);
                        // Extract the file name.
                        string fileName = Path.GetFileName(savedFilePath);
                        // Build the relative URL that does NOT include 'wwwroot'
                        resourceUrl = $"/uploads/resources/{fileName}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during file upload.");
                        TempData["ErrorMessage"] = ex.Message;
                        return RedirectToAction("Create", new { exchangeId = model.ExchangeId, offerId = model.OfferId });
                    }
                }

                // Create the TblResource record.
                var resource = new TblResource
                {
                    OwnerUserId = model.OwnerUserId,
                    ExchangeId = model.ExchangeId,
                    OfferId = model.OfferId,
                    Title = model.Title,
                    Description = model.Description,
                    FilePath = resourceUrl,
                    ResourceType = model.ResourceType,
                    CreatedDate = DateTime.UtcNow
                };

                _dbContext.TblResources.Add(resource);
                await _dbContext.SaveChangesAsync();

                // Retrieve the related offer details.
                var offer = await _dbContext.TblOffers.FirstOrDefaultAsync(o => o.OfferId == model.OfferId);
                string offerTitle = offer != null ? offer.Title : "the offer";

                int currentUserId = model.OwnerUserId;
                int otherUserId = 0;
                var exchange = await _dbContext.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == model.ExchangeId);
                if (exchange != null)
                {
                    // If the current user is the offer owner, then the partner is the other user.
                    otherUserId = (currentUserId == (exchange.OfferOwnerId ?? 0))
                                   ? exchange.OtherUserId ?? 0
                                   : exchange.OfferOwnerId ?? 0;
                }

                // Build the notification message content.
                string notificationContent = $"Shared a new resource for \"{offerTitle}\". Click the view button below to see details.";

                // Create a new notification message for resource sharing.
                var notificationMessage = new TblMessage
                {
                    SenderUserId = model.OwnerUserId, // user sharing the resource
                    ReceiverUserId = otherUserId,
                    Content = notificationContent,
                    MessageType = "ResourceNotification",  // A type you will check in the view
                    ResourceId = resource.ResourceId,  // Make sure your TblResource has an identifier property
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                // Add and save the new message.
                _dbContext.TblMessages.Add(notificationMessage);
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = "Resource shared successfully.";

                // Redirect back to the resource list for the current exchange.
                return RedirectToAction("List", new { exchangeId = model.ExchangeId, offerId = model.OfferId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing resource sharing form.");
                TempData["ErrorMessage"] = "An error occurred while sharing the resource.";
                return RedirectToAction("SelectOffer");
            }
        }

        /// <summary>
        /// Displays a list of shared resources for a specific exchange and offer.
        /// </summary>
        public async Task<IActionResult> List(int exchangeId, int offerId, int page = 1)
        {
            try
            {
                _logger.LogInformation("List action called with exchangeId: {exchangeId} and offerId: {offerId}", exchangeId, offerId);
                if (exchangeId <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid exchange identifier.";
                    return RedirectToAction("Index", "ExchangeDashboard");
                }
                // Verify that the current user is a participant in the exchange.
                var exchange = await _dbContext.TblExchanges
                    .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                if (exchange == null)
                {
                    TempData["ErrorMessage"] = "Exchange not found.";
                    return RedirectToAction("Index", "ExchangeDashboard");
                }

                int currentUserId = GetCurrentUserId();
                // Determine the opposite user ID:
                // Assuming that Exchange record has OfferOwnerId and OtherUserId properties.
                int oppositeUserId = currentUserId == (exchange.OfferOwnerId ?? 0)
                    ? exchange.OtherUserId ?? 0
                    : exchange.OfferOwnerId ?? 0;

                int pageSize = 10;

                // Load resources shared by the current user.
                var myResourcesQuery = _dbContext.TblResources
                    .Where(r => r.ExchangeId == exchangeId && r.OfferId == offerId && r.OwnerUserId == currentUserId);

                int totalMyResources = await myResourcesQuery.CountAsync();

                var myResources = await myResourcesQuery
                    .OrderByDescending(r => r.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Load resources shared by the opposite user.
                var receivedResourcesQuery = _dbContext.TblResources
                    .Where(r => r.ExchangeId == exchangeId && r.OfferId == offerId && r.OwnerUserId == oppositeUserId);

                int totalReceivedResources = await receivedResourcesQuery.CountAsync();

                var receivedResources = await receivedResourcesQuery
                    .OrderByDescending(r => r.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new ResourceListVM
                {
                    ExchangeId = exchangeId,
                    OfferId = offerId,
                    CurrentUserId = currentUserId,
                    MyResources = myResources,
                    ReceivedResources = receivedResources,
                    CurrentPage = page,
                    PageSize = pageSize,
                    MyResourcesTotalPages = (int)Math.Ceiling(totalMyResources / (double)pageSize),
                    ReceivedResourcesTotalPages = (int)Math.Ceiling(totalReceivedResources / (double)pageSize)
                };

                ViewData["ExchangeId"] = viewModel.ExchangeId;
                ViewData["OfferId"] = viewModel.OfferId;

                return View("List", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading resource list for exchange {ExchangeId}", exchangeId);
                TempData["ErrorMessage"] = "An error occurred while loading resources.";
                return RedirectToAction("Index", "ExchangeDashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(int resourceId)
        {
            // Retrieve the resource record from the database
            var resource = await _dbContext.TblResources.FindAsync(resourceId);
            if (resource == null)
            {
                TempData["ErrorMessage"] = "Resource not found.";
                return RedirectToAction("List", new { exchangeId = 0, offerId = 0 });
            }

            // Build the physical file path; assuming resource.FilePath is the public relative URL like "/uploads/resources/filename.ext"
            string fileRelativePath = resource.FilePath.TrimStart('/');
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileRelativePath);

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "File not found on the server.";
                return RedirectToAction("List", new { exchangeId = resource.ExchangeId, offerId = resource.OfferId });
            }

            // Determine MIME type. You can use a library such as Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider,
            // or for a generic code file, you might want to force download using "application/octet-stream".
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out string contentType))
            {
                contentType = "application/octet-stream";
            }

            // Read the file bytes.
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // The third parameter (the fileDownloadName) forces download in the browser.
            return File(fileBytes, contentType, Path.GetFileName(filePath));
        }

        /// <summary>
        /// Retrieves the current logged-in user’s ID from claims.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }
    }
}