using Microsoft.AspNetCore.Mvc.Rendering;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.Services.Contracts
{
    public class ContractPreparationService : IContractPreparationService
    {
        private readonly SkillSwapDbContext _context;

        public ContractPreparationService(SkillSwapDbContext context)
        {
            _context = context;
        }

        public async Task<ContractCreationVM> PrepareViewModelAsync(int messageId, int currentUserId, bool revealReceiverDetails = false)
        {
            var message = await _context.TblMessages.FindAsync(messageId);
            if (message == null || !message.OfferId.HasValue)
                throw new Exception("Invalid message or no offer attached.");

            var receiverUserId = (message.SenderUserId == currentUserId)
                                 ? message.ReceiverUserId
                                 : message.SenderUserId;

            var senderUser = await _context.TblUsers.FindAsync(currentUserId)
                ?? throw new Exception("Sender not found.");

            var receiverUser = await _context.TblUsers.FindAsync(receiverUserId);

            var senderSkills = await _context.TblUserSkills
                .Include(x => x.Skill)
                .Where(x => x.UserId == currentUserId && x.IsOffering)
                .Select(x => new SelectListItem { Text = x.Skill.SkillName, Value = x.SkillId.ToString() })
                .ToListAsync();

            var offer = await _context.TblOffers.FindAsync(message.OfferId.Value);
            var learningDays = offer?.DeliveryTimeDays ?? 0;

            var receiverSkills = await _context.TblUserSkills
                .Include(x => x.Skill)
                .Where(x => x.UserId == receiverUserId && x.IsOffering)
                .Select(x => new SelectListItem { Text = x.Skill.SkillName, Value = x.SkillId.ToString() })
                .ToListAsync();

            string offerOwnerSkillName = "N/A";
            if (offer != null && !string.IsNullOrWhiteSpace(offer.SkillIdOfferOwner))
            {
                // Split the comma-separated skill IDs
                var skillIdStrings = offer.SkillIdOfferOwner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var skillNames = new List<string>();
                foreach (var skillIdStr in skillIdStrings)
                {
                    var trimmed = skillIdStr.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        // Look up each skill from the receiverSkills list (which is already loaded)
                        var skillItem = receiverSkills.FirstOrDefault(r => r.Value == trimmed);
                        if (skillItem != null)
                        {
                            skillNames.Add(skillItem.Text);
                        }
                    }
                }
                if (skillNames.Any())
                    offerOwnerSkillName = string.Join(", ", skillNames);
            }

            return new ContractCreationVM
            {
                MessageId = messageId,
                OfferId = message.OfferId.Value,
                SenderUserId = currentUserId,
                ReceiverUserId = receiverUserId,
                SenderUserName = senderUser.UserName,
                ReceiverUserName = receiverUser?.UserName ?? "",

                // Conditional masking
                ReceiverName = !string.IsNullOrWhiteSpace($"{receiverUser.FirstName} {receiverUser.LastName}".Trim())
                    ? $"{receiverUser.FirstName} {receiverUser.LastName}".Trim()
                    : receiverUser.UserName,
                ReceiverEmail = revealReceiverDetails ? receiverUser?.Email : "[counterparty@email.com]",
                ReceiverAddress = revealReceiverDetails
                    ? $"{receiverUser?.Address}, {receiverUser?.City}, {receiverUser?.Country}"
                    : "[Counterparty Address]",

                SenderName = $"{senderUser.FirstName} {senderUser.LastName}",
                SenderEmail = senderUser.Email,
                SenderAddress = $"{senderUser.Address}, {senderUser.City}, {senderUser.Country}",

                Category = offer.Category,
                LearningObjective = offer.ScopeOfWork ?? "N/A",
                OppositeExperienceLevel = offer.RequiredSkillLevel,
                ModeOfLearning = offer.CollaborationMethod,
                OfferOwnerAvailability = offer.FreelanceType,
                AssistanceRounds = offer.AssistanceRounds,

                ContractDate = DateTime.Now,
                TokenOffer = offer.TokenCost,
                LearningDays = learningDays,
                OfferedSkill = offerOwnerSkillName,
                OfferOwnerSkill = offerOwnerSkillName,
                SenderOfferedSkills = senderSkills,
                AdditionalTerms = "",
                SenderAgreementAccepted = false,
                ReceiverAgreementAccepted = false,
                AccountSenderName = $"{senderUser.FirstName} {senderUser.LastName}",
                ReceiverSignature = "",
                ReceiverPlace = ""
            };
        }
    }
}