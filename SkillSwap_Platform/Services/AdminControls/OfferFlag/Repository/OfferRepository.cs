using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository
{
    public class OfferRepository : IOfferRepository
    {
        private readonly SkillSwapDbContext _ctx;
        public OfferRepository(SkillSwapDbContext ctx) => _ctx = ctx;

        public Task<TblOffer> GetByIdAsync(int id) =>
            _ctx.TblOffers.FirstOrDefaultAsync(o => o.OfferId == id);

        public async Task UpdateAsync(TblOffer offer)
        {
            _ctx.TblOffers.Update(offer);
            await _ctx.SaveChangesAsync();
        }
    }
}
