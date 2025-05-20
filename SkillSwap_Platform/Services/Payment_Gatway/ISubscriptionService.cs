using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public interface ISubscriptionService
    {
        Task<Subscription> GetActiveAsync(int userId);
        Task CreateAsync(int userId, string planName, DateTime start, DateTime end);
        Task UpsertAsync(int userId, string planName, string billingCycle, DateTime start, DateTime end);
        Task RecordPaymentAsync(string orderId, string paymentId, decimal paidAmount, string desiredPlanName, string billingCycle);
        Task CancelAutoRenewAsync(int userId, string reason);
        Task<bool> IsInPlanAsync(int userId, string planName);
        Task<SubscriptionTier> GetTierAsync(int userId);
    }
}
