using SkillSwap_Platform.Models.ViewModels.PaymentGatway;

namespace SkillSwap_Platform.Services.Payment_Gatway.RazorPay
{
    public interface IRazorpayService
    {
        string Key { get; }
        Task<CreateOrderResult> CreateOrderAsync(string planName, string billingCycle);
        bool VerifySignature(string orderId, string paymentId, string signature);
    }
}
