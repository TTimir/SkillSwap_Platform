using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.PDF;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.Services.Contracts
{
    public class ContractHandlerService : IContractHandlerService
    {
        private readonly SkillSwapDbContext _context;
        private readonly IViewRenderService _viewRenderService;
        private readonly IPdfGenerator _pdfGenerator;
        private readonly ILogger<ContractHandlerService> _logger;

        public ContractHandlerService(
            SkillSwapDbContext context,
            IViewRenderService viewRenderService,
            IPdfGenerator pdfGenerator,
            ILogger<ContractHandlerService> logger)
        {
            _context = context;
            _viewRenderService = viewRenderService;
            _pdfGenerator = pdfGenerator;
            _logger = logger;
        }

        public async Task<(bool Success, string ErrorMessage)> CreateContractAsync(ContractCreationVM model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Set required fields
                model.ContractDate ??= DateTime.Now;
                // Ensure server-controlled fields have values in case they were not posted
                model.Mode = string.IsNullOrWhiteSpace(model.Mode) ? "Create" : model.Mode;
                model.ActionContext = string.IsNullOrWhiteSpace(model.ActionContext) ? "CreateOnly" : model.ActionContext;
                model.SenderSignature = model.SenderSignature ?? "";
                model.SenderPlace = model.SenderPlace ?? "";
                // Define regex patterns for the placeholders.
                var signatureRegex = new Regex(@"^\[?\s*sign\s+here\s*\]?$", RegexOptions.IgnoreCase);
                var placeRegex = new Regex(@"^\[?\s*place\s+of\s+sign\s*\]?$", RegexOptions.IgnoreCase);

                // Validate that the user has replaced the placeholder values.
                if (signatureRegex.IsMatch(model.SenderSignature.Trim()) || model.SenderSignature == null)
                {
                    return (false, "Please provide your actual signature.");
                }

                if (placeRegex.IsMatch(model.SenderPlace.Trim()) || model.SenderPlace == null)
                {
                    return (false, "Please provide your actual place of signing.");
                }

                var senderUser = await _context.TblUsers.FindAsync(model.SenderUserId);
                var receiverUser = await _context.TblUsers.FindAsync(model.ReceiverUserId);
                if (senderUser == null || receiverUser == null)
                    return (false, "Sender or Receiver user not found.");

                bool baseContractExists = await _context.TblContracts.AnyAsync(c =>
                    c.OfferId == model.OfferId
                    && (
                         // same two users, in either order
                         (c.SenderUserId == model.SenderUserId && c.ReceiverUserId == model.ReceiverUserId)
                      || (c.SenderUserId == model.ReceiverUserId && c.ReceiverUserId == model.SenderUserId)
                    )
                    // only consider “real” contracts (not declined, not children of other contracts)
                    && c.ParentContractId == null
                    // only if it’s still live or being reviewed
                    && c.Status != "Declined"
                    && c.Status != "Expired"
                );

                if (baseContractExists)
                    return (false, "A contract has already been sent for this offer and conversation.");

                // Use the user records to get full names.
                string senderFullName = $"{senderUser.FirstName} {senderUser.LastName}".Trim();
                string receiverFullName = $"{receiverUser.FirstName} {receiverUser.LastName}".Trim();

                model.SenderUserName ??= senderUser.UserName;
                model.ReceiverUserName ??= receiverUser.UserName;

                var offer = await _context.TblOffers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.OfferId == model.OfferId);

                if (offer == null)
                    return (false, "The related offer could not be found.");

                // Find max version for same MessageId + OfferId
                int latestVersion = await _context.TblContracts
                    .Where(c => c.MessageId == model.MessageId && c.OfferId == model.OfferId)
                    .MaxAsync(c => (int?)c.Version) ?? 0;
                int newVersion = latestVersion + 1;

                // If version 1 doesn't exist, this is the base
                int? baseContractId = null;
                if (latestVersion > 0)
                {
                    baseContractId = await _context.TblContracts
                        .Where(c => c.MessageId == model.MessageId && c.OfferId == model.OfferId && c.Version == 1)
                        .Select(c => (int?)c.ContractId)
                        .FirstOrDefaultAsync();
                }

                // Contract creation logic
                var contract = new TblContract
                {
                    MessageId = model.MessageId,
                    OfferId = model.OfferId,
                    SenderUserId = model.SenderUserId,
                    ReceiverUserId = model.ReceiverUserId,
                    TokenOffer = model.TokenOffer,
                    OfferedSkill = model.OfferedSkill,
                    SenderEmail = model.SenderEmail,
                    SenderAddress = model.SenderAddress,
                    SenderUserName = model.SenderUserName,
                    ReceiverEmail = model.ReceiverEmail,
                    ReceiverAddress = model.ReceiverAddress,
                    ReceiverUserName = model.ReceiverUserName,
                    Category = offer.Category,
                    LearningObjective = offer.ScopeOfWork ?? "N/A",
                    OppositeExperienceLevel = offer.RequiredSkillLevel,
                    ModeOfLearning = offer.CollaborationMethod,
                    OfferOwnerAvailability = offer.FreelanceType,
                    AssistanceRounds = offer.AssistanceRounds,
                    AdditionalTerms = SerializeMultiline(model.AdditionalTerms),
                    FlowDescription = SerializeMultiline(model.FlowDescription),
                    Status = "Pending",
                    CreatedDate = model.ContractDate.Value,
                    CompletionDate = model.ContractDate?.AddDays(model.LearningDays + 1),
                    SenderAgreementAccepted = model.SenderAgreementAccepted,
                    SenderAcceptanceDate = model.SenderAgreementAccepted ? DateTime.Now : null,
                    SenderSignature = model.SenderSignature,
                    ReceiverAgreementAccepted = model.ReceiverAgreementAccepted,
                    ReceiverAcceptanceDate = model.ReceiverAgreementAccepted ? DateTime.Now : null,
                    ReceiverSignature = model.ReceiverSignature,
                    SignedBySender = model.SenderAgreementAccepted,
                    SignedByReceiver = model.ReceiverAgreementAccepted,
                    Version = newVersion,
                    ParentContractId = baseContractId,
                    SenderSkill = model.OfferedSkill,
                    ReceiverSkill = offer?.SkillIdOfferOwner.ToString(),
                    SenderName = senderFullName,
                    ReceiverName = $"{receiverUser.FirstName} {receiverUser.LastName}".Trim(),
                    SenderPlace = model.SenderPlace,
                    ReceiverPlace = model.ReceiverPlace,
                    ContractUniqueId = model.ContractUniqueId
                };
                _context.TblContracts.Add(contract);
                await _context.SaveChangesAsync();

                var SenderOfferedSkills = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == model.SenderUserId && s.IsOffering)
                    .Select(s => new SelectListItem { Text = s.Skill.SkillName, Value = s.SkillId.ToString() })
                    .ToListAsync();

                // Prepare final model for PDF generation
                var finalContractModel = new ContractCreationVM
                {
                    MessageId = contract.MessageId,
                    OfferId = contract.OfferId,
                    SenderUserId = contract.SenderUserId,
                    ReceiverUserId = contract.ReceiverUserId,
                    TokenOffer = contract.TokenOffer,
                    AdditionalTerms = contract.AdditionalTerms,
                    FlowDescription = contract.FlowDescription,
                    ContractDate = contract.CreatedDate,
                    CompletionDate = contract.CompletionDate ?? contract.CreatedDate.AddDays(model.LearningDays + 1),
                    Version = contract.Version,
                    SenderName = senderFullName,
                    SenderUserName = model.SenderUserName ?? senderUser.UserName,
                    ReceiverName = $"{receiverUser.FirstName} {receiverUser.LastName}".Trim(),
                    ReceiverUserName = model.ReceiverUserName ?? "user_unknown",
                    SenderEmail = model.SenderEmail,
                    SenderAddress = model.SenderAddress,
                    ReceiverEmail = model.ReceiverEmail,
                    ReceiverAddress = model.ReceiverAddress,
                    Category = contract.Category,
                    LearningObjective = contract.LearningObjective,
                    OppositeExperienceLevel = contract.OppositeExperienceLevel,
                    ModeOfLearning = contract.ModeOfLearning,
                    OfferOwnerAvailability = contract.OfferOwnerAvailability,
                    AssistanceRounds = contract.AssistanceRounds,
                    LearningDays = model.LearningDays,
                    SenderAgreementAccepted = model.SenderAgreementAccepted,
                    SenderAcceptanceDate = model.SenderAgreementAccepted ? DateTime.Now : null,
                    SenderSignature = model.SenderSignature,
                    ReceiverAgreementAccepted = model.ReceiverAgreementAccepted,
                    ReceiverAcceptanceDate = model.ReceiverAgreementAccepted ? DateTime.Now : null,
                    ReceiverSignature = model.ReceiverSignature,
                    OfferedSkill = model.OfferedSkill,
                    OfferOwnerSkill = model.OfferOwnerSkill,
                    SenderPlace = model.SenderPlace,
                    ReceiverPlace = model.ReceiverPlace,
                    ContractUniqueId = model.ContractUniqueId
                };
                finalContractModel.Status = "Pending";
                // Populate sender skill options for resolving skill text in PDF
                finalContractModel.SenderOfferedSkills = await _context.TblUserSkills
                    .Include(s => s.Skill)
                    .Where(s => s.UserId == model.SenderUserId && s.IsOffering)
                    .Select(s => new SelectListItem
                    {
                        Text = s.Skill.SkillName,
                        Value = s.SkillId.ToString()
                    }).ToListAsync();


                var htmlContent = await _viewRenderService.RenderViewToStringAsync("PreviewContract", finalContractModel, "Contract");
                byte[] pdfBytes = await _pdfGenerator.GeneratePdfFromHtmlAsync(htmlContent, contract.Version);

                string baseFolder = Path.Combine("wwwroot", "contracts");
                string subFolder = "Initial";
                string folderPath = Path.Combine(baseFolder, subFolder);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Generate the filename (keeping it the same as before).
                string skillPart = Sanitize(finalContractModel.OfferOwnerSkill ?? "Skill");
                string senderPart = Sanitize(finalContractModel.SenderName ?? "Sender");
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string fileName = $"{skillPart}-{senderPart}-{timestamp}.pdf";
                string filePath = Path.Combine(folderPath, fileName);

                // Write the PDF bytes to disk.
                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                // Save the relative file path to the contract record.
                contract.ContractDocument = $"/contracts/{subFolder}/{fileName}";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating contract.");
                return (false, "An unexpected error occurred while creating the contract.");
            }
        }

        private string Sanitize(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("-", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
                .Replace(" ", "-");
        }

        // Updated to join lines with newline rather than JSON.
        private string SerializeMultiline(string input)
        {
            var lines = input?.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        ?? Array.Empty<string>();
            return string.Join(Environment.NewLine, lines);
        }

    }
}