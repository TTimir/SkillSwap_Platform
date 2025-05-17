using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class PaymentLogService : IPaymentLogService
    {
        private readonly SkillSwapDbContext _db;
        public PaymentLogService(SkillSwapDbContext db) => _db = db;

        public Task<bool> HasProcessedAsync(string orderId) =>
            _db.PaymentLogs.AnyAsync(p => p.OrderId == orderId);

        public async Task LogAsync(int userId, string orderId, string paymentId)
        {
            _db.PaymentLogs.Add(new PaymentLog
            {
                UserId = userId,
                OrderId = orderId,
                PaymentId = paymentId,
                ProcessedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
