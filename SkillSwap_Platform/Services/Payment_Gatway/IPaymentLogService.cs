namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public interface IPaymentLogService
    {
        Task<bool> HasProcessedAsync(string orderId);
        Task LogAsync(int userId, string orderId, string paymentId);
    }
}
