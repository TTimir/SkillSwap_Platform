using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Data;

namespace SkillSwap_Platform.Services
{
    public class TokenAdminService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger _logger;
        public TokenAdminService(SkillSwapDbContext db, ILogger<TokenAdminService> logger)
        {
            _db = db; _logger = logger;
        }

        public async Task<TblTokenTransaction> AdjustBalanceAsync(
            int targetUserId,
            decimal amount,
            string adjustmentType,
            string reason,
            int adminUserId,
            CancellationToken ct = default)
        {
            // 1) Load user
            var user = await _db.TblUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == targetUserId, ct)
                       ?? throw new KeyNotFoundException("User not found.");

            if (user == null)
                throw new KeyNotFoundException($"User #{targetUserId} not found.");

            // 2) Snapshot balances
            var oldBal = user.DigitalTokenBalance;
            var newBal = oldBal + amount;
            var requiresApproval = Math.Abs(amount) > 1000m;

            // 3) Build transaction record
            var tx = new TblTokenTransaction
            {
                ExchangeId = null,
                FromUserId = amount < 0 ? targetUserId : (int?)null,
                ToUserId = amount > 0 ? targetUserId : (int?)null,
                Amount = Math.Abs(amount),
                TxType = "AdminAdjustment",
                Description = $"{adjustmentType}: {reason}",
                CreatedAt = DateTime.UtcNow,
                AdminAdjustmentType = adjustmentType,
                AdminAdjustmentReason = reason,
                AdminUserId = adminUserId,
                OldBalance = oldBal,
                NewBalance = newBal,
                RequiresApproval = requiresApproval,
                IsApproved = true,
                IsReleased = true  // not used for escrow
            };

            // 4) Atomically update balance + insert the tx
            await using var scope = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct);

            user.DigitalTokenBalance = newBal;
            _db.TblUsers.Update(user);
            _db.TblTokenTransactions.Add(tx);

            await _db.SaveChangesAsync(ct);
            await scope.CommitAsync(ct);

            return tx;
        }

        public async Task<TblTokenTransaction?> GetTransactionAsync(int id, CancellationToken ct = default)
        {
            return await _db.TblTokenTransactions
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.TransactionId == id, ct);
        }

    }
}
