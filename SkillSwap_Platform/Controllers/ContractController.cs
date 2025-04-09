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
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.Diagnostics.Contracts;
using SkillSwap_Platform.HelperClass.Extensions;
using SkillSwap_Platform.HelperClass;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class ContractController : Controller
    {
        private const string MODE_CREATE = "Create";
        private const string MODE_EDIT = "Edit";
        private const string MODE_SIGN = "Sign";
        private const string ACTION_CREATEONLY = "CreateOnly";
        private const string ACTION_MODIFYONLY = "ModifyOnly";

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
                viewModel.Mode = MODE_CREATE;
                viewModel.ActionContext = ACTION_CREATEONLY;
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
            ModelState.Remove(nameof(model.ReceiverPlace));
            ModelState.Remove(nameof(model.ReceiverSignature));
            ModelState.Remove(nameof(model.ContractUniqueId));
            ModelState.Remove(nameof(model.LearningDays));

            if (!ModelState.IsValid)
            {
                LogModelErrors();
                return View(model);
            }

            var receiverUser = await _context.TblUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == model.ReceiverUserId);
            if (receiverUser != null)
            {
                // Override the placeholder values with the actual data.
                model.ReceiverEmail = receiverUser.Email;
                model.ReceiverAddress = $"{receiverUser.Address}, {receiverUser.City}, {receiverUser.Country}";
            }

            // Generate the unique contract identifier
            // Format: SkillSwap-CT-<yyyyMMddHHmmss>-<6charHexToken>
            if (string.IsNullOrWhiteSpace(model.ContractUniqueId))
            {
                string dateTimePart = DateTime.Now.ToString("yyyyMMddHHmmss");
                string token = UniqueIdGenerator.GenerateSixCharHexToken();
                model.ContractUniqueId = $"SkillSwap-CT-{dateTimePart}-{token}";
            }

            var result = await _contractHandler.CreateContractAsync(model);

            if (!result.Success)
            {
                if (result.ErrorMessage.Contains("already been sent"))
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return RedirectToAction("Conversation", "Messaging", new { otherUserId = model.ReceiverUserId });
                }
            }

            model.Mode = MODE_CREATE;
            model.ActionContext = ACTION_CREATEONLY;
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
                RemoveServerControlledFields();
                model.ContractDate ??= DateTime.Now;
                // Fill missing usernames
                model.SenderUserName ??= (await _context.TblUsers.FindAsync(model.SenderUserId))?.UserName;
                model.ReceiverUserName ??= (await _context.TblUsers.FindAsync(model.ReceiverUserId))?.UserName;


                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    return BadRequest("Invalid contract data.");
                }

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
                var pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html, model.Version);

                // Generate dynamic preview filename with version
                string filename = GeneratePreviewFilename(previewModel);
                return File(pdfBytes, "application/pdf", filename);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview from form data.");
                return StatusCode(500, "An error occurred while generating the preview.");
            }
        }
        #endregion

        #region Modify Contract (Sender or Receiver)
        [HttpGet]
        public async Task<IActionResult> ModifyContractAndSend(int contractId)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            int userId = GetUserId();
            if (contract.ReceiverUserId != userId && contract.SenderUserId != userId)
                return Forbid();

            // If contract is at final version (v3), do not allow further modification.
            if (contract.Version >= 3)
            {
                TempData["ErrorMessage"] = "This is the final version of the contract. No further modifications are allowed.";
                return RedirectToAction("Review", new { contractId });
            }

            var vm = await PrepareViewModelForEdit(contract, ACTION_MODIFYONLY);
            vm.Mode = MODE_EDIT;
            vm.ActionContext = ACTION_MODIFYONLY;

            return View("PreviewContract", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifyContractAndSend(int contractId, [FromForm] decimal tokenOffer, [FromForm] string flowDescription, [FromForm] string additionalTerms, [FromForm] string partyPlace, [FromForm] bool partyAgreementAccepted, [FromForm] string partySignature, [FromForm] string partyAcceptanceDate, [FromForm] bool finalModificationConfirmed = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ModelState.Remove("FlowDescription");

                var originalContract = await _context.TblContracts
                    .FirstOrDefaultAsync(c => c.ContractId == contractId);
                if (originalContract == null)
                {
                    return NotFound();
                }

                int currentUserId = GetUserId();
                bool isReceiver = currentUserId == originalContract.ReceiverUserId;
                bool isSender = currentUserId == originalContract.SenderUserId;
                if (!isReceiver && !isSender)
                    return Forbid();

                // Prevent further modifications if contract is at version 3
                if (originalContract.Version >= 3)
                {
                    TempData["ErrorMessage"] = "This contract is final (v3) and cannot be modified further.";
                    return RedirectToAction("Review", new { contractId });
                }

                if (!partyAgreementAccepted)
                {
                    ModelState.AddModelError("PartyAgreementAccepted", "You must accept the agreement before submitting.");

                    originalContract = await _context.TblContracts.FindAsync(contractId);
                    var vm = await PrepareViewModelForEdit(originalContract, ACTION_MODIFYONLY);
                    vm.ActionContext = ACTION_MODIFYONLY;
                    return View("PreviewContract", vm);
                }

                if (!HasMinimumFlowSteps(flowDescription))
                {
                    ModelState.AddModelError("FlowDescription", "Please provide at least 3 steps in the process flow.");
                    return await ReloadModifyFormWithErrors(contractId, flowDescription, additionalTerms, partyPlace, partySignature, partyAcceptanceDate);
                }

                // Check for invalid signature value
                if (string.IsNullOrWhiteSpace(partySignature) || partySignature.Trim() == "[Sign Here]")
                {
                    ModelState.AddModelError("partySignature", "Please provide your actual signature.");
                    var vm = await PrepareViewModelForEdit(originalContract, ACTION_MODIFYONLY);
                    vm.ActionContext = ACTION_MODIFYONLY;
                    return View("PreviewContract", vm);
                }

                // Check for invalid place value
                if (string.IsNullOrWhiteSpace(partyPlace) || partyPlace.Trim() == "[Place of Sign]")
                {
                    ModelState.AddModelError("PartyPlace", "Please provide your actual place of signing.");
                    var vm = await PrepareViewModelForEdit(originalContract, ACTION_MODIFYONLY);
                    vm.ActionContext = ACTION_MODIFYONLY;
                    return View("PreviewContract", vm);
                }

                // Validate acceptance date is today's date
                var today = DateTime.UtcNow.Date;
                if (!DateTime.TryParseExact(partyAcceptanceDate, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var acceptanceDate) ||
                    acceptanceDate.Date != today)
                {
                    ModelState.AddModelError("PartyAcceptanceDate", "The acceptance date must be today’s date.");
                    var vm = await PrepareViewModelForEdit(originalContract, ACTION_MODIFYONLY);
                    vm.ActionContext = ACTION_MODIFYONLY;
                    return View("PreviewContract", vm);
                }

                if (!ModelState.IsValid)
                {
                    LogModelStateDetails();
                    LogModelErrors();
                    var vm = await PrepareViewModelForEdit(originalContract, ACTION_MODIFYONLY);
                    vm.ActionContext = ACTION_MODIFYONLY; 
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
                    TokenOffer = tokenOffer,
                    OfferedSkill = originalContract.SenderSkill,
                    AdditionalTerms = SerializeMultiline(additionalTerms),
                    FlowDescription = SerializeMultiline(flowDescription),
                    SenderUserName = originalContract.SenderUserName,
                    ReceiverUserName = originalContract.ReceiverUserName,
                    SenderEmail = originalContract.SenderEmail,
                    ReceiverEmail = originalContract.ReceiverEmail,
                    SenderAddress = originalContract.SenderAddress,
                    ReceiverAddress = originalContract.ReceiverAddress,
                    Category = offer.Category,
                    LearningObjective = offer.ScopeOfWork ?? "N/A",
                    OppositeExperienceLevel = offer.RequiredSkillLevel,
                    ModeOfLearning = offer.CollaborationMethod,
                    OfferOwnerAvailability = offer.FreelanceType,
                    AssistanceRounds = offer.AssistanceRounds,
                    // Use the original ContractDate (or update it if needed)
                    CreatedDate = DateTime.Now,
                    CompletionDate = DateTime.Now.AddDays(learningDays + 1),

                    Version = originalContract.Version + 1,
                    ParentContractId = originalContract.ContractId,
                    BaseContractId = originalContract.BaseContractId ?? originalContract.ContractId,

                    // Copy any additional fields as needed.
                    SenderSkill = originalContract.SenderSkill,
                    ReceiverSkill = originalContract.ReceiverSkill,
                    // You may want to preserve other fields such as names, emails, etc.
                    SenderName = originalContract.SenderName,
                    ReceiverName = originalContract.ReceiverName,
                    ContractUniqueId = UpdateContractUniqueId(originalContract.ContractUniqueId)
                };

                // Now, update the acceptance fields based on the modifying role.
                if (isReceiver)
                {
                    // Receiver is modifying: clear sender's acceptance and update receiver's fields.
                    newContract.SenderAgreementAccepted = false;
                    newContract.SenderAcceptanceDate = null;
                    newContract.SenderSignature = string.Empty;
                    newContract.SenderPlace = string.Empty;
                    newContract.SignedBySender = false;

                    newContract.ReceiverAgreementAccepted = partyAgreementAccepted;
                    newContract.ReceiverAcceptanceDate = partyAgreementAccepted ? DateTime.Now : (DateTime?)null;
                    newContract.ReceiverSignature = partySignature.Trim();
                    newContract.ReceiverPlace = partyPlace.Trim();
                    newContract.SignedByReceiver = partyAgreementAccepted;
                }
                else if (isSender)
                {
                    // Sender is modifying: clear receiver's acceptance and update sender's fields.
                    newContract.ReceiverAgreementAccepted = false;
                    newContract.ReceiverAcceptanceDate = null;
                    newContract.ReceiverSignature = string.Empty;
                    newContract.ReceiverPlace = string.Empty;
                    newContract.SignedByReceiver = false;

                    newContract.SenderAgreementAccepted = partyAgreementAccepted;
                    newContract.SenderAcceptanceDate = partyAgreementAccepted ? DateTime.Now : (DateTime?)null;
                    newContract.SenderSignature = partySignature.Trim();
                    newContract.SenderPlace = partyPlace.Trim();
                    newContract.SignedBySender = partyAgreementAccepted;
                }

                // For status, set it based on the modifying role. (You could choose to use a single status,
                // but here we differentiate so later the UI can decide how to show actions.)
                newContract.Status = isReceiver ? "ModifiedByReceiver" : "ModifiedBySender";

                _context.TblContracts.Add(newContract);
                await _context.SaveChangesAsync();

                var previewModel = await PrepareViewModelForEdit(newContract, "Review");
                previewModel.IsPreview = true;

                string html = await _viewRenderService.RenderViewToStringAsync("PreviewContract", previewModel, "Contract");
                byte[] pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html, newContract.Version);

                // Determine subfolder based on contract version and status.
                string baseFolder = Path.Combine("wwwroot", "contracts");
                string subFolder = "";

                if (newContract.Status.Equals("ModifiedByReceiver", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = Path.Combine("Modified", "modifybyreceiver");
                }
                else if (newContract.Status.Equals("ModifiedBySender", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = Path.Combine("Modified", "modifybysender");
                }
                else if (newContract.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = "Final";
                }
                else
                {
                    subFolder = "Others"; // Fallback if needed.
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

                TempData["SuccessMessage"] = "Contract modified and sent back successfully.";
                int redirectUserId = isReceiver ? originalContract.SenderUserId : originalContract.ReceiverUserId;
                return RedirectToAction("Conversation", "Messaging", new { otherUserId = redirectUserId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error modifying contract for contractId={ContractId}", contractId);
                TempData["ErrorMessage"] = "An error occurred while modifying the contract.";
                return RedirectToAction("Review", new { contractId });
            }
        }
        #endregion

        #region Sign Contract

        [HttpGet]
        public async Task<IActionResult> SignContract(int contractId)
        {
            try
            {
                var contract = await _context.TblContracts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ContractId == contractId);

                if (contract == null)
                    return NotFound();

                int userId = GetUserId();
                if (contract.ReceiverUserId != userId && contract.SenderUserId != userId)
                    return Forbid();

                var sender = await _context.TblUsers.FindAsync(contract.SenderUserId);
                var receiver = await _context.TblUsers.FindAsync(contract.ReceiverUserId);
                var offer = await _context.TblOffers.FindAsync(contract.OfferId);

                DateTime? originalDate = contract.ParentContractId == null
                    ? contract.CreatedDate
                    : (await _context.TblContracts.FindAsync(contract.ParentContractId))?.CreatedDate;

                var senderSkills = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == contract.SenderUserId && s.IsOffering)
                    .Select(s => new SelectListItem
                    {
                        Text = s.Skill.SkillName,
                        Value = s.SkillId.ToString()
                    }).ToListAsync();

                var viewModel = new ContractCreationVM
                {
                    ContractUniqueId = contract.ContractUniqueId,
                    MessageId = contract.MessageId,
                    OfferId = contract.OfferId,
                    ContractDate = contract.CreatedDate,
                    OriginalCreatedDate = originalDate,
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
                    TokenOffer = contract.TokenOffer,
                    Category = contract.Category, // or fetch from offer if needed
                    LearningObjective = contract.LearningObjective,
                    OppositeExperienceLevel = contract.OppositeExperienceLevel,
                    ModeOfLearning = contract.ModeOfLearning,
                    OfferOwnerAvailability = contract.OfferOwnerAvailability,
                    AssistanceRounds = contract.AssistanceRounds,
                    LearningDays = offer?.DeliveryTimeDays ?? 0,
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
                    IsPreview = true,
                    // Set mode to "Sign" so that only receiver acceptance fields become editable
                    Mode = MODE_SIGN,
                    ActionContext = contract.ParentContractId == null ? ACTION_CREATEONLY : ACTION_MODIFYONLY
                };

                return View("PreviewContract", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contract for signing, contractId={ContractId}", contractId);
                TempData["ErrorMessage"] = "Something went wrong. Please try again later.";
                return RedirectToAction("Conversation", "Messaging");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignContract(int contractId, [FromForm] string partySignature, [FromForm] string partyPlace, [FromForm] string OfferedSkill, [FromForm] bool finalSignConfirmed = false)
        {
            try
            {
                var originalContract = await _context.TblContracts.FindAsync(contractId);
                if (originalContract == null)
                    return NotFound();

                int currentUserId = GetUserId();
                bool isReceiver = currentUserId == originalContract.ReceiverUserId;
                bool isSender = currentUserId == originalContract.SenderUserId;
                if (!isReceiver && !isSender)
                    return Forbid();

                // Block signing if contract is expired or modified beyond allowed versions.
                if (originalContract.Version > 3 || originalContract.Status == "Expired")
                {
                    TempData["ErrorMessage"] = "This contract is expired or has exceeded modifications; signing is no longer possible.";
                    int redirectUserId = isReceiver ? originalContract.SenderUserId : originalContract.ReceiverUserId;
                    return RedirectToAction("Conversation", "Messaging", new { otherUserId = redirectUserId });
                }

                // If contract is version 3 (final), force confirmation before signing.
                if (originalContract.Version == 3 && !finalSignConfirmed)
                {
                    TempData["WarningMessage"] = "You are signing the final version (v3). No further modifications will be possible after signing.";
                    return RedirectToAction("Review", new { contractId, mode = MODE_SIGN });
                }

                // Validate signature, place, etc., just like your normal sign logic
                if (string.IsNullOrWhiteSpace(partySignature) || partySignature.Trim() == "[Sign Here]")
                {
                    TempData["ErrorMessage"] = "Please provide your actual signature.";
                    return RedirectToAction("Review", new { contractId, mode = MODE_SIGN });
                }

                if (string.IsNullOrWhiteSpace(partyPlace) || partyPlace.Trim() == "[Place of Sign]")
                {
                    TempData["ErrorMessage"] = "Please provide your place of signing.";
                    return RedirectToAction("Review", new { contractId, mode = MODE_SIGN });
                }

                // Create a new contract version with updated receiver details.
                if (originalContract.Status == "ModifiedByReceiver"
                    || originalContract.Status == "ModifiedBySender"
                           || originalContract.Status == "Pending"
                           || originalContract.Status == "Review")
                {
                    var newFinalContract = new TblContract
                    {
                        // Basic identifiers and foreign keys.
                        MessageId = originalContract.MessageId,
                        OfferId = originalContract.OfferId,
                        SenderUserId = originalContract.SenderUserId,
                        ReceiverUserId = originalContract.ReceiverUserId,
                        TokenOffer = originalContract.TokenOffer,
                        SenderUserName = originalContract.SenderUserName,
                        ReceiverUserName = originalContract.ReceiverUserName,
                        SenderEmail = originalContract.SenderEmail,
                        ReceiverEmail = originalContract.ReceiverEmail,
                        SenderAddress = originalContract.SenderAddress,
                        ReceiverAddress = originalContract.ReceiverAddress,
                        Category = originalContract.Category, // or fetch from offer if needed
                        LearningObjective = originalContract.LearningObjective,
                        OppositeExperienceLevel = originalContract.OppositeExperienceLevel,
                        ModeOfLearning = originalContract.ModeOfLearning,
                        OfferOwnerAvailability = originalContract.OfferOwnerAvailability,
                        AssistanceRounds = originalContract.AssistanceRounds,

                        // Skills: copy both sender and receiver skill info.
                        OfferedSkill = OfferedSkill,
                        SenderSkill = OfferedSkill,
                        ReceiverSkill = originalContract.ReceiverSkill, // Make sure this is set in the original contract

                        // Names and contact details.
                        SenderName = originalContract.SenderName,
                        ReceiverName = originalContract.ReceiverName,

                        // Additional contract text.
                        AdditionalTerms = originalContract.AdditionalTerms,
                        FlowDescription = originalContract.FlowDescription,

                        // Dates.
                        CreatedDate = DateTime.Now, // New record creation date
                        CompletionDate = originalContract.CompletionDate, // Preserved from original

                        // Versioning.
                        Version = originalContract.Version + 1,
                        ParentContractId = originalContract.ContractId,
                        BaseContractId = originalContract.BaseContractId ?? originalContract.ContractId,
                        ContractUniqueId = UpdateContractUniqueId(originalContract.ContractUniqueId)
                    };

                    // Role-based assignment:
                    if (isReceiver)
                    {
                        // Receiver signing: preserve sender’s acceptance and update receiver’s.
                        newFinalContract.SenderAgreementAccepted = originalContract.SenderAgreementAccepted;
                        newFinalContract.SenderAcceptanceDate = originalContract.SenderAcceptanceDate;
                        newFinalContract.SenderSignature = originalContract.SenderSignature;
                        newFinalContract.SenderPlace = originalContract.SenderPlace;
                        newFinalContract.SignedBySender = originalContract.SignedBySender;

                        newFinalContract.ReceiverAgreementAccepted = true;
                        newFinalContract.ReceiverAcceptanceDate = DateTime.Now;
                        newFinalContract.ReceiverSignature = partySignature.Trim();
                        newFinalContract.ReceiverPlace = partyPlace.Trim();
                        newFinalContract.SignedByReceiver = true;
                    }
                    else if (isSender)
                    {
                        // Sender signing: preserve receiver’s acceptance and update sender’s.
                        newFinalContract.ReceiverAgreementAccepted = originalContract.SignedByReceiver;
                        newFinalContract.ReceiverAcceptanceDate = originalContract.ReceiverAcceptanceDate;
                        newFinalContract.ReceiverSignature = originalContract.ReceiverSignature;
                        newFinalContract.ReceiverPlace = originalContract.ReceiverPlace;
                        newFinalContract.SignedByReceiver = originalContract.SignedByReceiver;

                        newFinalContract.SenderAgreementAccepted = true;
                        newFinalContract.SenderAcceptanceDate = DateTime.Now;
                        newFinalContract.SenderSignature = partySignature.Trim();
                        newFinalContract.SenderPlace = partyPlace.Trim();
                        newFinalContract.SignedBySender = true;
                    }

                    newFinalContract.Status = "Accepted";
                    newFinalContract.FinalizedDate = DateTime.Now;

                    // This code would go after the final contract has been created and saved.
                    if (newFinalContract.Status == "Accepted")
                    {
                        int skillIdOfferOwner = 0;
                        int skillIdRequester = 0;

                        // Assuming the sender's skill (offer owner's skill) is stored in SenderSkill.
                        if (!int.TryParse(newFinalContract.SenderSkill, out skillIdRequester))
                        {
                            // Handle conversion error (you might want to log or set a default value)
                            skillIdRequester = 0;
                        }

                        // Assuming the receiver's skill (requester's skill) is stored in ReceiverSkill.
                        if (!int.TryParse(newFinalContract.ReceiverSkill, out skillIdOfferOwner))
                        {
                            // Handle conversion error (you might want to log or set a default value)
                            skillIdOfferOwner = 0;
                        }

                        var digitalTokenExchange = newFinalContract.TokenOffer;

                        // Create an exchange record.
                        var exchange = new TblExchange
                        {
                            OfferId = newFinalContract.OfferId,
                            // Determine the requester based on your business rules; for example:
                            OfferOwnerId = newFinalContract.ReceiverUserId,
                            OtherUserId = newFinalContract.SenderUserId,
                            ExchangeDate = DateTime.Now,
                            LastStatusChangeDate = DateTime.Now,
                            Status = "Finalized",
                            // Set the mode (e.g., "SkillSwap" or another applicable value)
                            ExchangeMode = newFinalContract.ModeOfLearning,
                            IsSkillSwap = true,  // or false if applicable
                            TokensPaid = newFinalContract.TokenOffer ?? 0,
                            // Map these to the appropriate skill IDs as per your application logic.
                            SkillIdRequester = skillIdRequester,
                            SkillIdOfferOwner = skillIdOfferOwner,
                            Description = "Exchange finalized after both parties signed the contract.",
                            LastStatusChangedBy = GetUserId(), // assuming current user is the one finalizing
                            StatusChangeReason = "Final contract signature from both parties",
                            DigitalTokenExchange = digitalTokenExchange,  // update as needed
                            IsSuccessful = true
                        };

                        _context.TblExchanges.Add(exchange);
                        await _context.SaveChangesAsync();

                        // Create an exchange history record to log this change.
                        var exchangeHistory = new TblExchangeHistory
                        {
                            ExchangeId = exchange.ExchangeId,
                            OfferId = newFinalContract.OfferId,
                            ChangedStatus = "Finalized",
                            ChangedBy = GetUserId(),
                            ChangeDate = DateTime.Now,
                            Reason = "Contract finalized and signed by both parties."
                        };

                        _context.TblExchangeHistories.Add(exchangeHistory);
                        await _context.SaveChangesAsync();
                    }

                    // (Assuming PDF generation is required, generate the PDF and save to disk)
                    var previewModel = await PrepareViewModelForEdit(newFinalContract, "Review");
                    previewModel.IsPreview = true;
                    string html = await _viewRenderService.RenderViewToStringAsync("PreviewContract", previewModel, "Contract");
                    byte[] pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html, newFinalContract.Version);

                    string baseFolder = Path.Combine("wwwroot", "contracts");
                    string subFolder = "Final";
                    string folderPath = Path.Combine(baseFolder, subFolder);
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    string skillPart = Sanitize(previewModel.OfferOwnerSkill ?? "Skill");
                    string senderPart = Sanitize(previewModel.SenderName ?? "Sender");
                    string versionPart = $"v{newFinalContract.Version}";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    string fileName = $"{skillPart}-{senderPart}-{versionPart}-{timestamp}.pdf";
                    string filePath = Path.Combine(folderPath, fileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                    newFinalContract.ContractDocument = $"/contracts/{subFolder}/{fileName}";

                    _context.TblContracts.Add(newFinalContract);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "You have signed the contract. A new version has been created.";
                    int redirectUserId = isReceiver ? originalContract.SenderUserId : originalContract.ReceiverUserId;
                    return RedirectToAction("Conversation", "Messaging", new { otherUserId = redirectUserId });
                }
                else
                {
                    // It's some other status => maybe we can't sign it again
                    TempData["ErrorMessage"] = "Cannot sign this contract in its current state.";
                    return RedirectToAction("Review", new { contractId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing contract for contractId={ContractId}", contractId);
                TempData["ErrorMessage"] = "An error occurred while signing the contract.";
                return RedirectToAction("Review", new { contractId });
            }
        }
        #endregion

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
        public async Task<IActionResult> Review(int contractId, string mode = "Review")
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

                var sender = await _context.TblUsers.FindAsync(contract.SenderUserId);
                var receiver = await _context.TblUsers.FindAsync(contract.ReceiverUserId);
                var offer = await _context.TblOffers.FindAsync(contract.OfferId);

                string offerOwnerSkillName = "N/A";
                if (offer != null && int.TryParse(offer.SkillIdOfferOwner, out int skillId))
                {
                    var receiverSkill = await _context.TblUserSkills
                        .Include(s => s.Skill)
                        .Where(s => s.UserId == contract.ReceiverUserId && s.SkillId == skillId && s.IsOffering)
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
                DateTime? originalDate = contract.ParentContractId == null
                    ? contract.CreatedDate
                    : (await _context.TblContracts.FindAsync(contract.ParentContractId))?.CreatedDate;

                var viewModel = new ContractCreationVM
                {
                    ContractId = contract.ContractId,
                    ContractUniqueId = contract.ContractUniqueId,
                    MessageId = contract.MessageId,
                    OfferId = contract.OfferId,
                    ContractDate = contract.CreatedDate,
                    OriginalCreatedDate = contract.ParentContractId == null
                        ? contract.CreatedDate
                        : (await _context.TblContracts.FindAsync(contract.ParentContractId))?.CreatedDate,
                    SenderUserId = contract.SenderUserId,
                    ReceiverUserId = contract.ReceiverUserId,
                    // Use the contract values (which are updated) for sender details:
                    SenderName = contract.SenderName,
                    SenderUserName = sender?.UserName,
                    SenderEmail = contract.SenderEmail,
                    SenderAddress = contract.SenderAddress,
                    // For receiver, use the stored contract values (or override if needed)
                    ReceiverName = contract.ReceiverName,
                    ReceiverUserName = receiver?.UserName,
                    ReceiverEmail = contract.ReceiverEmail,
                    ReceiverAddress = contract.ReceiverAddress,

                    OfferedSkill = contract.SenderSkill,
                    OfferOwnerSkill = offerOwnerSkillName,
                    TokenOffer = contract.TokenOffer,
                    Category = contract.Category,
                    LearningDays = offer?.DeliveryTimeDays ?? 0,
                    LearningObjective = contract.LearningObjective,
                    OppositeExperienceLevel = contract.OppositeExperienceLevel,
                    ModeOfLearning = contract.ModeOfLearning,
                    OfferOwnerAvailability = contract.OfferOwnerAvailability,
                    AssistanceRounds = contract.AssistanceRounds,
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
                    // Optionally, you can also build a sender offered skills list from the contract if needed.
                    SenderOfferedSkills = senderSkills,
                    IsPreview = true,
                    Mode = mode,
                    ActionContext = contract.ParentContractId == null ? ACTION_CREATEONLY : ACTION_MODIFYONLY
                };

                // If the contract is final (v3), set a flag (or status message) in the view model so that the UI shows only the "Sign" option.
                if (contract.Version >= 3)
                {
                    viewModel.Status = "Final";
                }

                return View("PreviewContract", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load contract for review.");
                TempData["ErrorMessage"] = "Something went wrong. Please try again later.";
                return RedirectToAction("Conversation", "Messaging");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int contractId)
        {
            // Retrieve the contract by its ID.
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null)
            {
                return NotFound();
            }

            // Prepare the view model for preview.
            // Here you can use your existing PrepareViewModelForEdit or another helper method
            // For example, if the PDF should look like the preview:
            var viewModel = await PrepareViewModelForEdit(contract, "Review");
            viewModel.IsPreview = false;
            viewModel.IsPdfDownload = true;

            // Render the HTML string using your view render service.
            string html = await _viewRenderService.RenderViewToStringAsync("PreviewContract", viewModel, "Contract");

            // Generate the PDF bytes from the HTML.
            byte[] pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(html, contract.Version);

            // Create a dynamic filename (for example, based on the sender's name, contract version, and timestamp).
            string skillPart = SanitizePDF(viewModel.OfferOwnerSkill ?? "Skill");
            string senderPart = SanitizePDF(viewModel.SenderName ?? "Sender");
            string versionPart = $"v{contract.Version}";
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string filename = $"{skillPart}-{senderPart}-{versionPart}-{timestamp}.pdf";

            // Return the PDF file.
            return File(pdfBytes, "application/pdf", filename);
        }

        public async Task<IActionResult> ViewFinalizedContractPdf(int contractId)
        {
            // Retrieve the contract record using contractId
            var contract = await _context.TblContracts.FirstOrDefaultAsync(c => c.ContractId == contractId);
            if (contract == null)
            {
                _logger.LogWarning("Contract with id {contractId} was not found.", contractId);
                return NotFound();
            }

            string relativeFilePath = contract.ContractDocument; // Adjust the property name if necessary.
            if (string.IsNullOrEmpty(relativeFilePath))
            {
                _logger.LogWarning("No finalized contract path found for contract id {contractId}.", contractId);
                return NotFound("Finalized contract file not found.");
            }
            string absoluteFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeFilePath.TrimStart('/'));

            if (!System.IO.File.Exists(absoluteFilePath))
            {
                _logger.LogWarning("File {absoluteFilePath} for contract id {contractId} does not exist.", absoluteFilePath, contractId);
                return NotFound("File not found.");
            }

            // Open a stream and return the file with the application/pdf mime type.
            var fileStream = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "application/pdf");
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

        /// <summary>
        /// Updates the contract unique id by replacing the timestamp portion while preserving the token.
        /// Expected format: "SkillSwap-CT-<timestamp>-<token>"
        /// </summary>
        private string UpdateContractUniqueId(string originalUniqueId)
        {
            // Expected format: "SkillSwap-CT-<timestamp>-<token>"
            var parts = originalUniqueId.Split('-');
            if (parts.Length >= 4)
            {
                // Use new timestamp in milliseconds for higher resolution
                string newTimestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                // Preserve the token (parts[3] and any extra parts, if any)
                string tokenPart = string.Join("-", parts.Skip(3));
                return $"SkillSwap-CT-{newTimestamp}-{tokenPart}";
            }
            else
            {
                // Fallback: if not in expected format, generate a new unique id.
                string newTimestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string token = UniqueIdGenerator.GenerateSixCharHexToken();
                return $"SkillSwap-CT-{newTimestamp}-{token}";
            }
        }

        private async Task<ContractCreationVM> PrepareViewModelForEdit(TblContract contract, string mode)
        {
            var sender = await _context.TblUsers.FindAsync(contract.SenderUserId);
            var receiver = await _context.TblUsers.FindAsync(contract.ReceiverUserId);
            var offer = await _context.TblOffers.FindAsync(contract.OfferId);

            string offerOwnerSkillName = "N/A";
            if (offer != null && int.TryParse(offer.SkillIdOfferOwner, out int skillId))
            {
                var receiverSkill = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == contract.ReceiverUserId && s.SkillId == skillId && s.IsOffering)
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

            var currentUserId = GetUserId();
            bool isSender = currentUserId == contract.SenderUserId;
            bool isReceiver = currentUserId == contract.ReceiverUserId;

            // In a ModifyOnly context, clear the acceptance for the party that is not modifying.
            bool hideSenderAcceptance = (mode == ACTION_MODIFYONLY) && !isSender;
            bool hideReceiverAcceptance = (mode == ACTION_MODIFYONLY) && !isReceiver;

            return new ContractCreationVM
            {
                ContractId = contract.ContractId,
                ContractUniqueId = contract.ContractUniqueId,
                MessageId = contract.MessageId,
                OfferId = contract.OfferId,
                SenderUserId = contract.SenderUserId,
                ReceiverUserId = contract.ReceiverUserId,
                SenderName = contract.SenderName,
                ReceiverName = contract.ReceiverName,
                SenderUserName = sender?.UserName,
                ReceiverUserName = receiver?.UserName,
                SenderEmail = contract.SenderEmail,
                ReceiverEmail = contract.ReceiverEmail,
                SenderAddress = contract.SenderAddress,
                ReceiverAddress = contract.ReceiverAddress,
                OfferedSkill = contract.SenderSkill,
                OfferOwnerSkill = offerOwnerSkillName,
                LearningDays = offer?.DeliveryTimeDays ?? 0,
                TokenOffer = contract.TokenOffer,
                Category = offer.Category,
                LearningObjective = offer.ScopeOfWork ?? "N/A",
                OppositeExperienceLevel = offer.RequiredSkillLevel,
                ModeOfLearning = offer.CollaborationMethod,
                OfferOwnerAvailability = offer.FreelanceType,
                AssistanceRounds = offer.AssistanceRounds,
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
                HideReceiverAcceptance = hideReceiverAcceptance,
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
            preparedModel.SenderName = model.SenderName;
            preparedModel.SenderAddress = model.SenderAddress;
            preparedModel.SenderEmail = model.SenderEmail;
            preparedModel.ReceiverAddress = model.ReceiverAddress;
            preparedModel.ReceiverEmail = model.ReceiverEmail;
            preparedModel.TokenOffer = model.TokenOffer;
            preparedModel.Category = model.Category;
            preparedModel.LearningObjective = model.LearningObjective;
            preparedModel.OppositeExperienceLevel = model.OppositeExperienceLevel;
            preparedModel.ModeOfLearning = model.ModeOfLearning;
            preparedModel.OfferOwnerAvailability = model.OfferOwnerAvailability;
            preparedModel.AssistanceRounds = model.AssistanceRounds;
            preparedModel.FlowDescription = model.FlowDescription;
            preparedModel.AdditionalTerms = model.AdditionalTerms;
            preparedModel.ContractDate = model.ContractDate;
            preparedModel.OfferedSkill = model.OfferedSkill;
            preparedModel.IsPreview = true;
            preparedModel.Status = "Preview";

            return preparedModel;
        }

        private async Task<IActionResult> ReloadModifyFormWithErrors(int contractId, string flowDescription, string additionalTerms, string receiverPlace, string receiverSignature, string receiverAcceptanceDate)
        {
            var contract = await _context.TblContracts.FindAsync(contractId);
            if (contract == null) return NotFound();

            var vm = await PrepareViewModelForEdit(contract, ACTION_MODIFYONLY);
            vm.ActionContext = ACTION_MODIFYONLY;

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
        private bool HasMinimumFlowSteps(string flowDescription)
        {
            if (string.IsNullOrWhiteSpace(flowDescription))
                return false;
            var steps = flowDescription.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return steps.Length >= 3;
        }

        private string GeneratePreviewFilename(ContractCreationVM model)
        {
            string skillPart = Sanitize(model.OfferOwnerSkill ?? "Skill");
            string senderPart = Sanitize(model.SenderName ?? "Sender");
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string version = $"v{model.Version}";
            return $"{skillPart}-{senderPart}-{version}-{timestamp}-Preview.pdf";
        }

        private void RemoveServerControlledFields()
        {
            ModelState.RemoveProperties(
                nameof(ContractCreationVM.ContractUniqueId),
                nameof(ContractCreationVM.SenderName),
                nameof(ContractCreationVM.SenderAddress),
                nameof(ContractCreationVM.SenderEmail),
                nameof(ContractCreationVM.SenderSignature),
                nameof(ContractCreationVM.SenderPlace),
                nameof(ContractCreationVM.ReceiverName),
                nameof(ContractCreationVM.ReceiverAddress),
                nameof(ContractCreationVM.ReceiverEmail),
                nameof(ContractCreationVM.ReceiverSignature),
                nameof(ContractCreationVM.ReceiverPlace),
                nameof(ContractCreationVM.SenderOfferedSkills),
                nameof(ContractCreationVM.FlowDescription),
                nameof(ContractCreationVM.SenderUserName),
                nameof(ContractCreationVM.ReceiverUserName),
                nameof(ContractCreationVM.ContractDate),
                nameof(ContractCreationVM.Mode),
                nameof(ContractCreationVM.ActionContext),
                nameof(ContractCreationVM.Category),
                nameof(ContractCreationVM.LearningDays),
                nameof(ContractCreationVM.OppositeExperienceLevel),
                nameof(ContractCreationVM.ModeOfLearning),
                nameof(ContractCreationVM.LearningObjective),
                nameof(ContractCreationVM.OfferOwnerAvailability),
                nameof(ContractCreationVM.AssistanceRounds)
            );
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

        private string SanitizePDF(string input)
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