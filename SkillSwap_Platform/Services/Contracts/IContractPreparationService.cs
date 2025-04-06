using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Contracts
{
    public interface IContractPreparationService
    {
        Task<ContractCreationVM> PrepareViewModelAsync(int messageId, int currentUserId, bool revealReceiverDetails = false);
    }
}
