using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.Escrow
{
    public class EscrowService : IEscrowService
    {
        private readonly SkillSwapDbContext _db;
        public EscrowService(SkillSwapDbContext db) => _db = db;

        public async Task<PagedResult<TblEscrow>> GetAllAsync(int page, int pageSize)
        {
            var q = _db.TblEscrows
                       .Include(e => e.Buyer)
                       .Include(e => e.Seller)
                       .OrderByDescending(e => e.CreatedAt);
            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<TblEscrow>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
            return result;
        }

        public Task<TblEscrow?> GetByIdAsync(int id)
            => _db.TblEscrows
                  .Include(e => e.Buyer)
                  .Include(e => e.Seller)
                  .Include(e => e.HandledByAdmin)
                  .FirstOrDefaultAsync(e => e.EscrowId == id);

        private async Task _HandleAsync(int id, int adminId, string notes,
                                        string newStatus, Action<TblEscrow> timestampSetter)
        {
            var e = await _db.TblEscrows.FindAsync(id)
                ?? throw new KeyNotFoundException("Escrow not found");
            if (e.Status != "Pending")
                throw new InvalidOperationException("Only pending escrows can be modified");
            e.Status = newStatus;
            timestampSetter(e);
            e.HandledByAdminId = adminId;
            e.AdminNotes = notes;
            _db.TblEscrows.Update(e);
            await _db.SaveChangesAsync();
        }

        public Task ReleaseAsync(int id, int adminId, string notes)
            => _HandleAsync(id, adminId, notes, "Released", e => e.ReleasedAt = DateTime.UtcNow);

        public Task RefundAsync(int id, int adminId, string notes)
            => _HandleAsync(id, adminId, notes, "Refunded", e => e.RefundedAt = DateTime.UtcNow);

        public Task DisputeAsync(int id, int adminId, string notes)
            => _HandleAsync(id, adminId, notes, "Disputed", e => e.DisputedAt = DateTime.UtcNow);

        // Services/AdminControls/Escrow/EscrowService.cs
        public async Task<TblEscrow> CreateAsync(int exchangeId, int buyerId, int sellerId, decimal amount)
        {
            var escrow = new TblEscrow
            {
                ExchangeId = exchangeId,
                BuyerId = buyerId,
                SellerId = sellerId,
                Amount = amount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.TblEscrows.Add(escrow);
            await _db.SaveChangesAsync();

            return escrow;
        }

    }
}
