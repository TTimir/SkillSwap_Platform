using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ContractController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ContractController> _logger;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public ContractController(SkillSwapDbContext context, ILogger<ContractController> logger, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _context = context;
            _logger = logger;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        // GET: /Contract/Create?messageId=...
        [HttpGet]
        public async Task<IActionResult> Create(int messageId)
        {
            try
            {
                // Retrieve the original message and associated offer details
                var message = await _context.TblMessages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null || !message.OfferId.HasValue)
                {
                    TempData["ErrorMessage"] = "Invalid message or no offer attached.";
                    return RedirectToAction("Conversation", "Messaging");
                }

                int currentUserId = GetUserId();
                int receiverUserId = (message.SenderUserId == currentUserId)
                                       ? message.ReceiverUserId
                                       : message.SenderUserId;

                // Retrieve sender details from the DB
                var senderUser = await _context.TblUsers.FindAsync(currentUserId);
                string registeredSenderName = $"{senderUser.FirstName} {senderUser.LastName}".Trim();
                string senderUserName = senderUser.UserName;
                if (senderUser == null)
                {
                    TempData["ErrorMessage"] = "Sender not found.";
                    return RedirectToAction("Conversation", "Messaging");
                }

                // Retrieve sender's offered skills (if needed elsewhere).
                var senderOfferedSkills = await _context.TblUserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == currentUserId && us.IsOffering)
                    .Select(us => new SelectListItem
                    {
                        Text = us.Skill.SkillName,
                        Value = us.SkillId.ToString()
                    })
                    .ToListAsync();

                // Retrieve receiver details.
                var receiverUser = await _context.TblUsers.FindAsync(receiverUserId);
                string fullSenderAddress = $"{senderUser.Address}, {senderUser.City}, {senderUser.Country}";
                string receiverUserName = receiverUser.UserName;
                // If receiverUser is not null, use its details; otherwise, use a placeholder.
                string receiverName = receiverUser != null
                    ? $"{receiverUser.FirstName} {receiverUser.LastName}"
                    : "Counterparty Name";

                // Retrieve receiver's offered skills.
                var receiverOfferedSkills = await _context.TblUserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == receiverUserId && us.IsOffering)
                    .Select(us => new SelectListItem
                    {
                        Text = us.Skill.SkillName,
                        Value = us.SkillId.ToString()
                    })
                    .ToListAsync();

                // Fetch the offer record.
                var offer = await _context.TblOffers.FindAsync(message.OfferId.Value);
                int learningDays = offer != null ? offer.DeliveryTimeDays.GetValueOrDefault() : 0;
                string offerOwnerSkill = "N/A";
                string offerOwnerSkillId = "";
                if (offer != null)
                {
                    // Use the receiver's offered skill id.
                    offerOwnerSkillId = offer.SkillIdOfferOwner.ToString();
                    // Lookup the receiver's skill name from the receiverOfferedSkills list.
                    var foundSkill = receiverOfferedSkills.FirstOrDefault(x => x.Value == offerOwnerSkillId);
                    if (foundSkill != null)
                    {
                        offerOwnerSkill = foundSkill.Text;
                    }
                }

                // Check if a contract already exists between these users for this message
                bool contractExists = await _context.TblContracts.AnyAsync(c =>
                    c.MessageId == messageId &&
                    ((c.SenderUserId == currentUserId && c.ReceiverUserId == receiverUserId) ||
                     (c.SenderUserId == receiverUserId && c.ReceiverUserId == currentUserId)));

                if (contractExists)
                {
                    TempData["ErrorMessage"] = "A contract has already been sent for this conversation.";
                    return RedirectToAction("Conversation", "Messaging", new { otherUserId = receiverUserId });
                }

                // Build and prepopulate the contract creation view model
                var viewModel = new ContractCreationVM
                {
                    MessageId = messageId,
                    OfferId = message.OfferId.Value,
                    SenderUserId = currentUserId,
                    ReceiverUserId = receiverUserId,
                    SenderName = registeredSenderName,
                    SenderUserName = senderUserName,
                    ReceiverName = "Counterparty Name",
                    ReceiverUserName = receiverUserName,
                    SenderAddress = fullSenderAddress,
                    SenderEmail = senderUser.Email,
                    ReceiverAddress = "Counterparty Address",
                    ReceiverEmail = "Counterparty Email",
                    ContractDate = DateTime.Now,
                    TokenOffer = 0,
                    AdditionalTerms = "",
                    LearningDays = learningDays,
                    SenderOfferedSkills = senderOfferedSkills,
                    OfferedSkill = offerOwnerSkillId,
                    OfferOwnerSkill = offerOwnerSkill,
                    SenderAgreementAccepted = false,
                    ReceiverAgreementAccepted = false
                };

                // Set the account sender name for client-side validation.
                viewModel.AccountSenderName = registeredSenderName;
                ModelState.Remove("OfferedSkill");
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contract creation page for message {MessageId}", messageId);
                TempData["ErrorMessage"] = "An error occurred while loading the contract creation page.";
                return RedirectToAction("Conversation", "Messaging");
            }
        }

        // POST: /Contract/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContractCreationVM model)
        {
            try
            {

                // Ensure pre-populated fields are set.
                if (!model.ContractDate.HasValue)
                {
                    model.ContractDate = DateTime.Now;
                    ModelState.Remove(nameof(model.ContractDate));
                }
                if (string.IsNullOrWhiteSpace(model.SenderUserName))
                {
                    var sender = await _context.TblUsers.FindAsync(model.SenderUserId);
                    if (sender != null)
                        model.SenderUserName = sender.UserName;
                    ModelState.Remove(nameof(model.SenderUserName)); // ← Clear error
                }

                if (string.IsNullOrWhiteSpace(model.ReceiverUserName))
                {
                    var receiver = await _context.TblUsers.FindAsync(model.ReceiverUserId);
                    if (receiver != null)
                        model.ReceiverUserName = receiver.UserName;
                    ModelState.Remove(nameof(model.ReceiverUserName)); // ← Clear error
                }

                // Check if token offer is negative.
                if (model.TokenOffer < 0)
                {
                    ModelState.AddModelError("TokenOffer", "Your token amount isn’t valid. your offer can even start with 0 tokens.");
                }

                // Validate that the pre-populated fields are not null.
                if (!model.ContractDate.HasValue)
                    ModelState.AddModelError("ContractDate", "Contract Date is required.");
                if (string.IsNullOrWhiteSpace(model.SenderUserName))
                    ModelState.AddModelError("SenderUserName", "Sender Username is required.");
                if (string.IsNullOrWhiteSpace(model.ReceiverUserName))
                    ModelState.AddModelError("ReceiverUserName", "Receiver Username is required.");

                // Validate that both the sender and receiver have agreed.
                if (!model.SenderAgreementAccepted)
                    ModelState.AddModelError("SenderAgreementAccepted", "Sender must agree to the terms to proceed.");

                ModelState.Remove(nameof(model.SenderOfferedSkills));

                // Populate dropdown before returning view
                model.SenderOfferedSkills = await _context.TblUserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == model.SenderUserId && us.IsOffering)
                    .Select(us => new SelectListItem
                    {
                        Text = us.Skill.SkillName,
                        Value = us.SkillId.ToString()
                    })
                    .ToListAsync();

                if (!ModelState.IsValid)
                {
                    // Log model state errors for debugging
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogError("Model error in '{Key}': {ErrorMessage}", state.Key, error.ErrorMessage);
                        }
                    }
                    return BadRequest("Invalid contract data.");
                }


                // Create a new contract record
                var contract = new TblContract
                {
                    MessageId = model.MessageId,
                    OfferId = model.OfferId,
                    SenderUserId = model.SenderUserId,
                    ReceiverUserId = model.ReceiverUserId,
                    TokenOffer = model.TokenOffer,
                    OfferedSkill = model.OfferedSkill,
                    AdditionalTerms = model.AdditionalTerms,
                    FlowDescription = model.FlowDescription,
                    Status = "Pending",
                    CreatedDate = model.ContractDate.Value,
                    SenderAgreementAccepted = model.SenderAgreementAccepted,
                    SenderAcceptanceDate = model.SenderAcceptanceDate,
                    SenderSignature = model.SenderSignature,
                    SignedBySender = model.SenderAgreementAccepted,
                    SignedByReceiver = model.ReceiverAgreementAccepted
                };

                _context.TblContracts.Add(contract);
                await _context.SaveChangesAsync();

                // Generate PDF HTML from view
                string htmlContent = await RenderViewToStringAsync("PreviewContract", new ContractCreationVM
                {
                    // populate with necessary info from contract
                    // or pass the model directly if everything is available
                    MessageId = contract.MessageId,
                    OfferId = contract.OfferId,
                    SenderUserId = contract.SenderUserId,
                    ReceiverUserId = contract.ReceiverUserId,
                    TokenOffer = contract.TokenOffer,
                    OfferedSkill = contract.OfferedSkill,
                    AdditionalTerms = contract.AdditionalTerms,
                    FlowDescription = contract.FlowDescription,
                    ContractDate = contract.CreatedDate,
                    SenderName = model.SenderName,
                    SenderUserName = model.SenderUserName,
                    ReceiverName = model.ReceiverName,
                    ReceiverUserName = model.ReceiverUserName,
                    SenderEmail = model.SenderEmail,
                    SenderAddress = model.SenderAddress,
                    ReceiverEmail = model.ReceiverEmail,
                    ReceiverAddress = model.ReceiverAddress,
                    LearningDays = model.LearningDays,
                    SenderAgreementAccepted = model.SenderAgreementAccepted,
                    SenderAcceptanceDate = model.SenderAcceptanceDate,
                    SenderSignature = model.SenderSignature,
                    OfferOwnerSkill = model.OfferOwnerSkill,
                    IsPreview = true
                });

                await new BrowserFetcher().DownloadAsync();

                var launchOptions = new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox" }
                };


                // Generate filename from offered skill + sender + timestamp
                string skillPart = Sanitize(model.OfferOwnerSkill ?? "Skill");
                string senderPart = Sanitize(model.SenderName ?? "Sender");
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                string fileName = $"{skillPart}-{senderPart}-{timestamp}.pdf";

                string pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "contracts");
                if (!Directory.Exists(pdfDirectory))
                    Directory.CreateDirectory(pdfDirectory);

                string filePath = Path.Combine(pdfDirectory, fileName);

                using (var browser = await Puppeteer.LaunchAsync(launchOptions))
                {
                    using (var page = await browser.NewPageAsync())
                    {
                        await page.SetContentAsync(htmlContent);

                        var pdfOptions = new PdfOptions
                        {
                            Format = PaperFormat.A4,
                            PrintBackground = true,
                        };

                        await page.PdfAsync(filePath, pdfOptions);
                        await browser.CloseAsync();
                    }
                }

                // Sanitize function for filenames (remove invalid characters)
                string Sanitize(string input)
                {
                    var invalid = Path.GetInvalidFileNameChars();
                    return string.Join("-", input
                        .Split(invalid, StringSplitOptions.RemoveEmptyEntries))
                        .Replace(" ", "-");
                }

                contract.ContractDocument = $"/contracts/{fileName}";
                await _context.SaveChangesAsync(); // Update the contract with path

                TempData["SuccessMessage"] = "Contract invitation created successfully.";
                return RedirectToAction("Conversation", "Messaging", new { otherUserId = model.ReceiverUserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contract for message {MessageId}", model.MessageId);
                TempData["ErrorMessage"] = "An error occurred while creating the contract.";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewFromForm(ContractCreationVM model)
        {
            try
            {
                ModelState.Remove("SenderOfferedSkills");
                ModelState.Remove("FlowDescription");
                ModelState.Remove("SenderUserName");
                ModelState.Remove("ReceiverUserName");
                ModelState.Remove("ContractDate");

                if (!model.ContractDate.HasValue)
                    model.ContractDate = DateTime.Now;
                if (string.IsNullOrWhiteSpace(model.SenderUserName))
                {
                    var sender = await _context.TblUsers.FindAsync(model.SenderUserId);
                    if (sender != null)
                        model.SenderUserName = sender.UserName;
                }
                if (string.IsNullOrWhiteSpace(model.ReceiverUserName))
                {
                    var receiver = await _context.TblUsers.FindAsync(model.ReceiverUserId);
                    if (receiver != null)
                        model.ReceiverUserName = receiver.UserName;
                }

                if (!ModelState.IsValid)
                {
                    // Log model state errors for debugging
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogError("Model error in '{Key}': {ErrorMessage}", state.Key, error.ErrorMessage);
                        }
                    }
                    return BadRequest("Invalid contract data.");
                }

                // Validate FlowDescription (at least 3 non-empty steps)
                var flow = model.FlowDescription ?? string.Empty;
                var steps = flow.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (steps.Length < 3)
                {
                    ModelState.AddModelError("FlowDescription", "To create an impactful flow, please add at least 3 steps and complete any additional fields, if provided.");
                }

                // Inside ContractController.Create (POST)
                if (model.TokenOffer < 0)
                {
                    ModelState.AddModelError("TokenOffer", "Your token amount isn’t valid. your offer can even start with 0 tokens.");
                    return View(model);
                }

                // Create a temporary contract object from the form data.
                var previewModel = new ContractCreationVM
                {
                    MessageId = model.MessageId,
                    OfferId = model.OfferId,
                    SenderUserId = model.SenderUserId,
                    ReceiverUserId = model.ReceiverUserId,
                    TokenOffer = model.TokenOffer,
                    OfferedSkill = model.OfferedSkill,
                    AdditionalTerms = model.AdditionalTerms,
                    FlowDescription = model.FlowDescription,
                    ContractDate = DateTime.Now,
                    AccountSenderName = model.AccountSenderName,
                    SenderAgreementAccepted = model.SenderAgreementAccepted,
                    ReceiverAgreementAccepted = model.ReceiverAgreementAccepted,
                    LearningDays = model.LearningDays
                };

                // Override ReceiverName and related fields.
                var receiverUser = await _context.TblUsers.FindAsync(model.ReceiverUserId);
                if (receiverUser != null)
                {
                    previewModel.ReceiverName = $"{receiverUser.FirstName} {receiverUser.LastName}";
                    previewModel.ReceiverAddress = receiverUser.Address;
                    previewModel.ReceiverEmail = receiverUser.Email;
                    previewModel.ReceiverUserName = receiverUser.UserName;
                }

                // For sender, derive profile URL using relative URL.
                var senderUser = await _context.TblUsers.FindAsync(model.SenderUserId);
                if (senderUser != null)
                {
                    previewModel.SenderName = $"{senderUser.FirstName} {senderUser.LastName}";
                    previewModel.SenderAddress = senderUser.Address;
                    previewModel.SenderEmail = senderUser.Email;
                    previewModel.SenderUserName = senderUser.UserName;
                }

                // Repopulate the OfferedSkills list.
                previewModel.SenderOfferedSkills = await _context.TblUserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == model.SenderUserId && us.IsOffering)
                    .Select(us => new SelectListItem
                    {
                        Text = us.Skill.SkillName,
                        Value = us.SkillId.ToString()
                    })
                    .ToListAsync();

                var offer = await _context.TblOffers.FindAsync(model.OfferId);
                if (offer != null)
                {
                    string offerOwnerSkillId = offer.SkillIdOfferOwner.ToString();
                    var receiverOfferedSkills = await _context.TblUserSkills
                        .Include(us => us.Skill)
                        .Where(us => us.UserId == model.ReceiverUserId && us.IsOffering)
                        .Select(us => new SelectListItem
                        {
                            Text = us.Skill.SkillName,
                            Value = us.SkillId.ToString()
                        })
                        .ToListAsync();
                    var foundSkill = receiverOfferedSkills.FirstOrDefault(x => x.Value == offerOwnerSkillId);
                    previewModel.OfferOwnerSkill = foundSkill != null ? foundSkill.Text : "N/A";
                }
                else
                {
                    previewModel.OfferOwnerSkill = "N/A";
                }

                // Validate acceptance dates if both parties have agreed.
                if (model.SenderAgreementAccepted && model.ReceiverAgreementAccepted)
                {
                    if (!model.SenderAcceptanceDate.HasValue || model.SenderAcceptanceDate.Value.Date != DateTime.Today)
                    {
                        ModelState.AddModelError("SenderAcceptanceDate", "Sender Acceptance Date must be today's date.");
                        return View(model);
                    }
                    if (!model.ReceiverAcceptanceDate.HasValue || model.ReceiverAcceptanceDate.Value.Date != DateTime.Today)
                    {
                        ModelState.AddModelError("ReceiverAcceptanceDate", "Receiver Acceptance Date must be today's date.");
                        return View(model);
                    }
                }

                // Set preview flag.
                previewModel.IsPreview = true;

                // Render the preview Razor view into HTML.
                string htmlContent = await RenderViewToStringAsync("PreviewContract", previewModel);

                // Download Chromium if not already available.
                await new BrowserFetcher().DownloadAsync();

                var launchOptions = new LaunchOptions
                {
                    Headless = true,
                    // Add "--no-sandbox" if needed (e.g., on Linux hosting environments)
                    Args = new[] { "--no-sandbox" }
                };

                using (var browser = await Puppeteer.LaunchAsync(launchOptions))
                {
                    using (var page = await browser.NewPageAsync())
                    {
                        // Set the HTML content of the page.
                        await page.SetContentAsync(htmlContent);

                        // Define PDF options as needed.
                        var pdfOptions = new PdfOptions
                        {
                            Format = PaperFormat.A4,
                            PrintBackground = true
                        };

                        // Generate the PDF.
                        var pdfBytes = await page.PdfDataAsync(pdfOptions);
                        await browser.CloseAsync();
                        return File(pdfBytes, "application/pdf", "ContractPreview.pdf");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview from form data.");
                return StatusCode(500, "An error occurred while generating the preview.");
            }
        }

        /// <summary>
        /// Renders a Razor view to an HTML string.
        /// </summary>
        private async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model)
        {
            try
            {
                ViewData.Model = model;
                using (var writer = new StringWriter())
                {
                    var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                    if (viewResult.View == null)
                    {
                        throw new InvalidOperationException($"View '{viewName}' not found.");
                    }

                    var viewContext = new ViewContext(
                        ControllerContext,
                        viewResult.View,
                        ViewData,
                        TempData,
                        writer,
                        new HtmlHelperOptions()
                    );
                    await viewResult.View.RenderAsync(viewContext);
                    return writer.GetStringBuilder().ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering view {ViewName} to string.", viewName);
                throw;
            }
        }

        // Helper method to get the current logged in user's ID from claims.
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }
    }
}