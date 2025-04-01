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
using SkillSwap_Platform.Services.Contracts;
using SkillSwap_Platform.Services.PDF;
using System;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ContractController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ContractController> _logger;
        private readonly IContractPreparationService _contractPreparation;
        private readonly IContractHandlerService _contractHandler;
        private readonly IViewRenderService _viewRenderService;
        private readonly IPdfGenerator _pdfGenerator;

        public ContractController(
            IContractPreparationService contractPreparation,
            IContractHandlerService contractHandler,
            SkillSwapDbContext context,
            ILogger<ContractController> logger,
            IViewRenderService viewRenderService,
            IPdfGenerator pdfGenerator)
        {
            _contractPreparation = contractPreparation;
            _contractHandler = contractHandler;
            _context = context;
            _logger = logger;
            _viewRenderService = viewRenderService;
            _pdfGenerator = pdfGenerator;
        }

        #region Create Contract
        // GET: /Contract/Create?messageId=...
        [HttpGet]
        public async Task<IActionResult> Create(int messageId)
        {
            try
            {
                int userId = GetUserId();
                var viewModel = await _contractPreparation.PrepareViewModelAsync(messageId, userId, revealReceiverDetails: false);

                // Set server-controlled properties so they appear in the hidden fields.
                viewModel.Mode = "Create";
                viewModel.ActionContext = "CreateOnly";
                viewModel.SenderSignature = viewModel.SenderSignature ?? "";
                viewModel.SenderPlace = viewModel.SenderPlace ?? "";
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing contract creation for messageId={MessageId}", messageId);
                TempData["ErrorMessage"] = "An error occurred while loading the contract creation page.";
                return RedirectToAction("Conversation", "Messaging");
            }
        }

        // POST: /Contract/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContractCreationVM model)
        {

            if (!ModelState.IsValid)
            {
                LogModelErrors();
                return View(model);
            }

            var result = await _contractHandler.CreateContractAsync(model);

            if (!result.Success)
            {
                if (result.ErrorMessage.Contains("already been sent"))
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return RedirectToAction("Conversation", "Messaging", new { otherUserId = model.ReceiverUserId });
                }
                else
                {
                    // Otherwise, display the error on the same page.
                    ModelState.AddModelError("", result.ErrorMessage);
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = "Contract created successfully.";
            return RedirectToAction("Conversation", "Messaging", new { otherUserId = model.ReceiverUserId });
        }

        #endregion

        #region Preview Contract
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewFromForm(ContractCreationVM model)
        {
            try
            {
                // Remove server-controlled fields from validation.
                ModelState.Remove(nameof(model.SenderOfferedSkills));
                ModelState.Remove(nameof(model.FlowDescription));
                ModelState.Remove(nameof(model.SenderUserName));
                ModelState.Remove(nameof(model.ReceiverUserName));
                ModelState.Remove(nameof(model.ContractDate));
                ModelState.Remove(nameof(model.Mode));
                ModelState.Remove(nameof(model.ActionContext));
                ModelState.Remove(nameof(model.SenderPlace));

                model.ContractDate ??= DateTime.Now;

                // Fill missing usernames
                model.SenderUserName ??= (await _context.TblUsers.FindAsync(model.SenderUserId))?.UserName;
                model.ReceiverUserName ??= (await _context.TblUsers.FindAsync(model.ReceiverUserId))?.UserName;


                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    return BadRequest("Invalid contract data.");
                }

                System.Diagnostics.Debug.WriteLine("FlowDescription: " + model.FlowDescription);
                // Validate FlowDescription (at least 3 non-empty steps)
                var flow = model.FlowDescription ?? string.Empty;
                var steps = flow.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (steps.Length < 3)
                {
                    return BadRequest("Please include at least 3 steps in the process flow.");
                }

                // Inside ContractController.Create (POST)
                if (model.TokenOffer < 0)
                {
                    return BadRequest("Your token amount isn’t valid. Your offer can even start with 0 tokens.");
                }

                // Build preview model
                var previewModel = await BuildPreviewModelAsync(model);

                // Set preview flag.
                previewModel.IsPreview = true;

                // Render the preview Razor view into HTML.
                string html = await _viewRenderService.RenderViewToStringAsync("PreviewContract", previewModel, "Contract");

                var pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html);

                // Generate dynamic preview filename with version
                string skillPart = Sanitize(previewModel.OfferOwnerSkill ?? "Skill");
                string senderPart = Sanitize(previewModel.SenderName ?? "Sender");
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string version = $"v{previewModel.Version}";

                string previewFileName = $"{skillPart}-{senderPart}-{version}-{timestamp}-Preview.pdf";

                return File(pdfBytes, "application/pdf", previewFileName);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview from form data.");
                return StatusCode(500, "An error occurred while generating the preview.");
            }
        }
        #endregion

        [HttpGet]
        public async Task<IActionResult> ModifyContractAndSend(int contractId)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            int userId = GetUserId();
            if (contract.ReceiverUserId != userId) return Forbid();

            var vm = await PrepareViewModelForEdit(contract, "ModifyOnly"); // Editable view
            vm.Mode = "Edit";
            vm.ActionContext = "ModifyOnly";

            return View("PreviewContract", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifyContractAndSend(int contractId, [FromForm] string flowDescription, [FromForm] string additionalTerms, [FromForm] string receiverPlace, [FromForm] bool receiverAgreementAccepted, [FromForm] string receiverSignature, [FromForm] string receiverAcceptanceDate)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ModelState.Remove("flowDescription");
                _logger.LogInformation("FlowDescription used: {FlowDescription}", flowDescription);
                var postedFlow = Request.Form["flowDescription"].ToString();
                _logger.LogInformation("Posted FlowDescription: {FlowDescription}", postedFlow);

                var originalContract = await _context.TblContracts.FindAsync(contractId);
                if (originalContract == null)
                {
                    _logger.LogWarning("Contract {ContractId} not found.", contractId);
                    return NotFound();
                }

                if (!receiverAgreementAccepted)
                {
                    ModelState.AddModelError("ReceiverAgreementAccepted", "You must accept the agreement before submitting.");

                    originalContract = await _context.TblContracts.FindAsync(contractId);
                    var vm = await PrepareViewModelForEdit(originalContract, "Edit");
                    vm.ActionContext = "ModifyOnly";
                    return View("PreviewContract", vm);
                }

                int userId = GetUserId();
                if (originalContract.ReceiverUserId != userId)
                {
                    _logger.LogWarning("Unauthorized modify attempt for contract {ContractId} by user {UserId}.", contractId, userId);
                    return Forbid();
                }

                // Ensure FlowDescription has at least 3 steps
                flowDescription = string.IsNullOrWhiteSpace(flowDescription)
                    ? originalContract.FlowDescription ?? string.Empty
                    : flowDescription;
                _logger.LogInformation("Using flowDescription: '{FlowDescription}'", flowDescription);
                var steps = flowDescription.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (steps.Length < 3)
                {
                    ModelState.AddModelError("FlowDescription", "Please provide at least 3 steps in the process flow.");
                    return await ReloadModifyFormWithErrors(contractId, flowDescription, additionalTerms, receiverPlace, receiverSignature, receiverAcceptanceDate);
                }

                // Check for invalid signature value
                if (string.IsNullOrWhiteSpace(receiverSignature) || receiverSignature.Trim() == "[Sign Here]")
                {
                    ModelState.AddModelError("ReceiverSignature", "Please provide your actual signature.");
                    var vm = await PrepareViewModelForEdit(originalContract, "Edit");
                    vm.ActionContext = "ModifyOnly";
                    return View("PreviewContract", vm);
                }

                // Check for invalid place value
                if (string.IsNullOrWhiteSpace(receiverPlace) || receiverPlace.Trim() == "[Place of Sign]")
                {
                    ModelState.AddModelError("ReceiverPlace", "Please provide your actual place of signing.");
                    var vm = await PrepareViewModelForEdit(originalContract, "Edit");
                    vm.ActionContext = "ModifyOnly";
                    return View("PreviewContract", vm);
                }

                // Validate acceptance date is today's date
                var today = DateTime.UtcNow.Date;
                if (!DateTime.TryParseExact(receiverAcceptanceDate, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var acceptanceDate) ||
                    acceptanceDate.Date != today)
                {
                    ModelState.AddModelError("ReceiverAcceptanceDate", "Receiver Acceptance Date must be today’s date.");
                    var vm = await PrepareViewModelForEdit(originalContract, "Edit");
                    vm.ActionContext = "ModifyOnly";
                    return View("PreviewContract", vm);
                }

                if (!ModelState.IsValid)
                {
                    ModelState.Remove(nameof(ContractCreationVM.FlowDescription));
                    LogModelStateDetails();
                    LogModelErrors();
                    var vm = await PrepareViewModelForEdit(originalContract, "Edit");
                    vm.ActionContext = "ModifyOnly";
                    return View("PreviewContract", vm);
                }

                var offer = await _context.TblOffers.FirstOrDefaultAsync(o => o.OfferId == originalContract.OfferId);
                int learningDays = offer?.DeliveryTimeDays ?? 0;
                // Create a new contract version
                var newContract = new TblContract
                {
                    MessageId = originalContract.MessageId,
                    OfferId = originalContract.OfferId,
                    SenderUserId = originalContract.SenderUserId,
                    ReceiverUserId = originalContract.ReceiverUserId,
                    TokenOffer = originalContract.TokenOffer,
                    OfferedSkill = originalContract.SenderSkill,
                    AdditionalTerms = SerializeMultiline(additionalTerms),
                    FlowDescription = SerializeMultiline(flowDescription),
                    // Use the original ContractDate (or update it if needed)
                    CreatedDate = DateTime.Now,
                    CompletionDate = DateTime.Now.AddDays(learningDays + 1),
                    // For agreements and signatures, copy over existing data and update receiver fields.
                    SenderAgreementAccepted = false,
                    SenderAcceptanceDate = null,
                    SenderSignature = string.Empty,
                    SenderPlace = string.Empty,
                    SignedBySender = false,

                    // Set receiver's updated fields:
                    ReceiverAgreementAccepted = receiverAgreementAccepted,
                    ReceiverAcceptanceDate = receiverAgreementAccepted ? DateTime.Now : null,
                    ReceiverSignature = receiverSignature.Trim(),
                    ReceiverPlace = receiverPlace.Trim(),
                    SignedByReceiver = receiverAgreementAccepted,

                    // Set status and version details:
                    Status = "ModifiedByReceiver",
                    Version = originalContract.Version + 1,
                    // Use ParentContractId: if the original already had a parent, keep that; otherwise, use original.ContractId.
                    ParentContractId = originalContract.ParentContractId ?? originalContract.ContractId,

                    // Copy any additional fields as needed.
                    SenderSkill = originalContract.SenderSkill,
                    ReceiverSkill = originalContract.ReceiverSkill,
                    // You may want to preserve other fields such as names, emails, etc.
                    SenderName = originalContract.SenderName,
                    ReceiverName = originalContract.ReceiverName,
                };

                _context.TblContracts.Add(newContract);
                await _context.SaveChangesAsync();

                var previewModel = await PrepareViewModelForEdit(newContract, "Review");
                previewModel.IsPreview = true;

                string html = await _viewRenderService.RenderViewToStringAsync("PreviewContract", previewModel, "Contract");
                byte[] pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html);

                // Determine subfolder based on contract version and status.
                string baseFolder = Path.Combine("wwwroot", "contracts");
                string subFolder = "";
                if (newContract.Version == 1)
                {
                    subFolder = "Initial";
                }
                else if (newContract.Status.Equals("ModifiedByReceiver", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = "Modified";
                }
                else if (newContract.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = "Final";
                }
                else
                {
                    subFolder = "Others"; // Fallback subfolder if needed.
                }

                string folderPath = Path.Combine(baseFolder, subFolder);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string skillPart = Sanitize(previewModel.OfferOwnerSkill ?? "Skill");
                string senderPart = Sanitize(previewModel.SenderName ?? "Sender");
                string versionPart = $"v{newContract.Version}";
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string fileName = $"{skillPart}-{senderPart}-{versionPart}-{timestamp}.pdf";
                string filePath = Path.Combine(folderPath, fileName);

                // Write PDF to disk
                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                // Save relative path to database
                newContract.ContractDocument = $"/contracts/{subFolder}/{fileName}";
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Contract modified and sent back to sender.";
                return RedirectToAction("Conversation", "Messaging", new { otherUserId = originalContract.SenderUserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying contract for contractId={ContractId}", contractId);
                TempData["ErrorMessage"] = "An error occurred while modifying the contract.";
                return RedirectToAction("Review", new { contractId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignContract(int contractId, [FromForm] string receiverSignature, [FromForm] string receiverPlace)
        {
            try
            {
                var contract = await _context.TblContracts.FindAsync(contractId);
                if (contract == null) return NotFound();

                int userId = GetUserId();
                if (contract.ReceiverUserId != userId) return Forbid();

                contract.SignedByReceiver = true;
                contract.ReceiverAgreementAccepted = true;
                contract.ReceiverSignature = receiverSignature?.Trim();
                contract.ReceiverPlace = receiverPlace?.Trim();
                contract.ReceiverAcceptanceDate = DateTime.Now;
                contract.Status = "Accepted";
                contract.FinalizedDate = DateTime.Now;

                ViewBag.Mode = "Edit";
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "You have signed the contract.";
                return RedirectToAction("Conversation", "Messaging", new { otherUserId = contract.SenderUserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing contract for contractId={ContractId}", contractId);
                TempData["ErrorMessage"] = "An error occurred while signing the contract.";
                return RedirectToAction("Review", new { contractId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptContract(int contractId)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            contract.SignedByReceiver = true;
            contract.SignedByReceiver = true;
            contract.Status = "Accepted";
            contract.FinalizedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "You accepted the contract.";
            return RedirectToAction("Conversation", "Messaging", new { otherUserId = contract.SenderUserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineContract(int contractId)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            contract.Status = "Declined";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "You declined the contract.";
            return RedirectToAction("Conversation", "Messaging", new { otherUserId = contract.SenderUserId });
        }

        [HttpGet]
        public async Task<IActionResult> Review(int contractId)
        {
            try
            {
                var contract = await _context.TblContracts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ContractId == contractId);

                if (contract == null)
                    return NotFound();

                // Verify the logged-in user is the receiver
                int userId = GetUserId();
                if (contract.ReceiverUserId != userId)
                    return Forbid();

                var sender = await _context.TblUsers.FindAsync(contract.SenderUserId);
                var receiver = await _context.TblUsers.FindAsync(contract.ReceiverUserId);
                var offer = await _context.TblOffers.FindAsync(contract.OfferId);

                string offerOwnerSkillName = "N/A";
                if (offer != null)
                {
                    var receiverSkill = await _context.TblUserSkills
                        .Include(s => s.Skill)
                        .Where(s => s.UserId == contract.ReceiverUserId && s.SkillId.ToString() == offer.SkillIdOfferOwner && s.IsOffering)
                        .FirstOrDefaultAsync();

                    offerOwnerSkillName = receiverSkill?.Skill.SkillName ?? "N/A";
                }

                var senderSkills = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == contract.SenderUserId && s.IsOffering)
                    .Select(s => new SelectListItem
                    {
                        Text = s.Skill.SkillName,
                        Value = s.SkillId.ToString()
                    }).ToListAsync();

                // Determine the original created date:
                DateTime? originalDate = null;
                if (contract.ParentContractId == null)
                {
                    originalDate = contract.CreatedDate;
                }
                else
                {
                    // Retrieve the base contract. This assumes that the original contract has ParentContractId == null.
                    var baseContract = await _context.TblContracts.FindAsync(contract.ParentContractId);
                    originalDate = baseContract?.CreatedDate;
                }

                var viewModel = new ContractCreationVM
                {
                    MessageId = contract.MessageId,
                    OfferId = contract.OfferId,
                    ContractDate = contract.CreatedDate,
                    SenderUserId = contract.SenderUserId,
                    ReceiverUserId = contract.ReceiverUserId,
                    SenderName = contract.SenderName,
                    ReceiverName = contract.ReceiverName,
                    SenderUserName = sender?.UserName,
                    ReceiverUserName = receiver?.UserName,
                    SenderEmail = sender?.Email,
                    ReceiverEmail = receiver?.Email,
                    SenderAddress = sender?.Address,
                    ReceiverAddress = receiver?.Address,
                    OfferedSkill = contract.SenderSkill,
                    OfferOwnerSkill = offerOwnerSkillName,
                    LearningDays = offer?.DeliveryTimeDays ?? 0,
                    TokenOffer = contract.TokenOffer,
                    FlowDescription = contract.FlowDescription,
                    AdditionalTerms = contract.AdditionalTerms,
                    SenderAgreementAccepted = contract.SenderAgreementAccepted,
                    SenderAcceptanceDate = contract.SenderAcceptanceDate,
                    SenderSignature = contract.SenderSignature,
                    SenderPlace = contract.SenderPlace,
                    ReceiverPlace = contract.ReceiverPlace,
                    ReceiverAgreementAccepted = contract.SignedByReceiver,
                    ReceiverAcceptanceDate = contract.ReceiverAcceptanceDate,
                    ReceiverSignature = contract.ReceiverSignature,
                    Version = contract.Version,
                    Status = contract.Status,
                    SenderOfferedSkills = senderSkills,
                    OriginalCreatedDate = originalDate
                };
                viewModel.IsPreview = true;
                viewModel.Mode = "Review";
                return View("PreviewContract", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load contract for review.");
                TempData["ErrorMessage"] = "Something went wrong. Please try again later.";
                return RedirectToAction("Conversation", "Messaging");
            }
        }

        public async Task<IActionResult> VersionHistory(int messageId, int offerId)
        {
            var versions = await _context.TblContracts
                .Where(c => c.MessageId == messageId && c.OfferId == offerId)
                .OrderByDescending(c => c.Version)
                .ToListAsync();

            return View("VersionHistory", versions);
        }

        #region Helper Class

        // Helper method to get the current logged in user's ID from claims.
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }

        private async Task<ContractCreationVM> PrepareViewModelForEdit(TblContract contract, string mode)
        {
            var sender = await _context.TblUsers.FindAsync(contract.SenderUserId);
            var receiver = await _context.TblUsers.FindAsync(contract.ReceiverUserId);
            var offer = await _context.TblOffers.FindAsync(contract.OfferId);

            string offerOwnerSkillName = "N/A";
            if (offer != null)
            {
                var receiverSkill = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == contract.ReceiverUserId && s.SkillId.ToString() == offer.SkillIdOfferOwner && s.IsOffering)
                    .FirstOrDefaultAsync();
                offerOwnerSkillName = receiverSkill?.Skill.SkillName ?? "N/A";
            }

            var senderSkills = await _context.TblUserSkills
                .Include(s => s.Skill)
                .Where(s => s.UserId == contract.SenderUserId && s.IsOffering)
                .Select(s => new SelectListItem
                {
                    Text = s.Skill.SkillName,
                    Value = s.SkillId.ToString()
                }).ToListAsync();

            // Determine the original created date:
            DateTime? originalDate = null;
            if (contract.ParentContractId == null)
            {
                originalDate = contract.CreatedDate;
            }
            else
            {
                // Retrieve the base contract. This assumes that the original contract has ParentContractId == null.
                var baseContract = await _context.TblContracts.FindAsync(contract.ParentContractId);
                originalDate = baseContract?.CreatedDate;
            }
            bool hideSenderAcceptance = (mode == "ModifyOnly");
            return new ContractCreationVM
            {
                ContractId = contract.ContractId,
                MessageId = contract.MessageId,
                OfferId = contract.OfferId,
                SenderUserId = contract.SenderUserId,
                ReceiverUserId = contract.ReceiverUserId,
                SenderName = contract.SenderName,
                ReceiverName = contract.ReceiverName,
                SenderUserName = sender?.UserName,
                ReceiverUserName = receiver?.UserName,
                SenderEmail = sender?.Email,
                ReceiverEmail = receiver?.Email,
                SenderAddress = sender?.Address,
                ReceiverAddress = receiver?.Address,
                OfferedSkill = contract.SenderSkill,
                OfferOwnerSkill = offerOwnerSkillName,
                LearningDays = offer?.DeliveryTimeDays ?? 0,
                TokenOffer = contract.TokenOffer,
                FlowDescription = contract.FlowDescription,
                AdditionalTerms = contract.AdditionalTerms,

                // ✅ Preserve sender's acceptance
                SenderAgreementAccepted = contract.SenderAgreementAccepted,
                SenderAcceptanceDate = contract.SenderAcceptanceDate,
                SenderSignature = contract.SenderSignature,
                SenderPlace = contract.SenderPlace,

                // ✅ Also preserve receiver’s side so it doesn’t reset unintentionally
                ReceiverAgreementAccepted = contract.SignedByReceiver,
                ReceiverAcceptanceDate = contract.ReceiverAcceptanceDate,
                ReceiverSignature = contract.ReceiverSignature,
                ReceiverPlace = contract.ReceiverPlace,

                Version = contract.Version,
                Status = contract.Status,
                SenderOfferedSkills = senderSkills,
                IsPreview = true,
                Mode = mode,
                OriginalCreatedDate = originalDate,
                HideSenderAcceptance = hideSenderAcceptance,
            };
        }

        private async Task<ContractCreationVM> BuildPreviewModelAsync(ContractCreationVM model)
        {
            // Use the improved preparation logic and reveal full receiver details for preview
            var preparedModel = await _contractPreparation.PrepareViewModelAsync(
                model.MessageId,
                model.SenderUserId,
                revealReceiverDetails: true
            );

            // Override any editable fields submitted from form
            preparedModel.TokenOffer = model.TokenOffer;
            preparedModel.FlowDescription = model.FlowDescription;
            preparedModel.AdditionalTerms = model.AdditionalTerms;
            preparedModel.ContractDate = model.ContractDate;
            preparedModel.OfferedSkill = model.OfferedSkill;
            preparedModel.AccountSenderName = model.AccountSenderName;
            preparedModel.IsPreview = true;
            preparedModel.Status = "Preview";

            return preparedModel;
        }

        private async Task<IActionResult> ReloadModifyFormWithErrors(int contractId, string flowDescription, string additionalTerms, string receiverPlace, string receiverSignature, string receiverAcceptanceDate)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            var vm = await PrepareViewModelForEdit(contract, "Edit");
            vm.ActionContext = "ModifyOnly";

            // Re-apply user-entered values to avoid resetting them on reload
            vm.FlowDescription = flowDescription;
            vm.AdditionalTerms = additionalTerms;
            vm.ReceiverPlace = receiverPlace;
            vm.ReceiverSignature = receiverSignature;

            if (DateTime.TryParse(receiverAcceptanceDate, out var parsedDate))
            {
                vm.ReceiverAcceptanceDate = parsedDate;
            }

            return View("PreviewContract", vm);
        }
        private void LogModelStateDetails()
        {
            foreach (var kvp in ModelState)
            {
                _logger.LogError("Key: '{Key}', ValidationState: {ValidationState}, Error Count: {ErrorCount}",
                    kvp.Key, kvp.Value.ValidationState, kvp.Value.Errors.Count);
            }
        }

        private void LogModelErrors()
        {
            if (ModelState.ContainsKey(string.Empty))
            {
                _logger.LogError("Model-level errors count: {Count}", ModelState[string.Empty].Errors.Count);
                foreach (var error in ModelState[string.Empty].Errors)
                {
                    var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.Exception?.Message
                        : error.ErrorMessage;
                    _logger.LogError("Model-level error: {ErrorMessage}", message);
                }
            }
        }

        private string Sanitize(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("-", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries))
                         .Replace(" ", "-");
        }

        private string SerializeMultiline(string input)
        {
            var lines = input?.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        ?? Array.Empty<string>();
            return string.Join(Environment.NewLine, lines);
        }

        #endregion

    }
}