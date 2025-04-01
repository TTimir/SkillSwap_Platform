using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Contracts
{
    public interface IContractHandlerService
    {
        Task<(bool Success, string ErrorMessage)> CreateContractAsync(ContractCreationVM model);
    }
}
