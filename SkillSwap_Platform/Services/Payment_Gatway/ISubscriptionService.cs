using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public interface ISubscriptionService
    {
        Task<Subscription> GetActiveAsync(int userId);
        Task CreateAsync(int userId, string planName, DateTime start, DateTime end);
        Task UpsertAsync(int userId, string planName, string billingCycle, DateTime start, DateTime end);
        Task CancelAutoRenewAsync(int userId, string reason);
        Task<bool> IsInPlanAsync(int userId, string planName);
        Task<SubscriptionTier> GetTierAsync(int userId);
    }
}
