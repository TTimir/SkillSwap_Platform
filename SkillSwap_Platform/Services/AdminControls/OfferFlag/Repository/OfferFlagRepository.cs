using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository
{
    public class OfferFlagRepository : IOfferFlagRepository
    {
        private readonly SkillSwapDbContext _ctx;
        public OfferFlagRepository(SkillSwapDbContext ctx) => _ctx = ctx;

        public async Task AddAsync(TblOfferFlag flag)
        {
            _ctx.TblOfferFlags.Add(flag);
            await _ctx.SaveChangesAsync();
        }

        public Task<TblOfferFlag> GetByIdAsync(int flagId) =>
            _ctx.TblOfferFlags
                .Include(f => f.Offer)
                .Include(f => f.FlaggedByUser)
                .FirstOrDefaultAsync(f => f.OfferFlagId == flagId);

        public Task<IEnumerable<TblOfferFlag>> GetPendingFlagsAsync() =>
            _ctx.TblOfferFlags
                .Where(f => !f.Offer.IsDeleted)    // for example
                .OrderByDescending(f => f.FlaggedDate)
                .ToListAsync()
                .ContinueWith(t => (IEnumerable<TblOfferFlag>)t.Result);

        public async Task DeleteAsync(TblOfferFlag flag)
        {
            _ctx.TblOfferFlags.Remove(flag);
            await _ctx.SaveChangesAsync();
        }

        public Task<IEnumerable<TblOfferFlag>> GetByOfferIdAsync(int offerId) =>
            _ctx.TblOfferFlags
               .Where(f => f.OfferId == offerId)
               .ToListAsync()
               .ContinueWith(t => (IEnumerable<TblOfferFlag>)t.Result);
    }
}
