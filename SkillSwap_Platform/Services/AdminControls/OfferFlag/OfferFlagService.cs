using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag
{
    public class OfferFlagService : IOfferFlagService
    {
        private readonly IOfferFlagRepository _flagRepo;
        private readonly IOfferRepository _offerRepo;
        private readonly ILogger<OfferFlagService> _log;
        private readonly SkillSwapDbContext _ctx;

        public OfferFlagService(
            IOfferFlagRepository repo,
            IOfferRepository offerRepo,
            ILogger<OfferFlagService> log,
            SkillSwapDbContext ctx)
        {
            _flagRepo = repo;
            _offerRepo = offerRepo;
            _log = log;
            _ctx = ctx;
        }

        public async Task FlagOfferAsync(int offerId, int userId, string reason)
        {
            try
            {
                var flag = new TblOfferFlag
                {
                    OfferId = offerId,
                    FlaggedByUserId = userId,
                    FlaggedDate = DateTime.UtcNow,
                    Reason = reason
                };
                await _flagRepo.AddAsync(flag);

                // 2. Mark the offer itself as flagged
                var offer = await _offerRepo.GetByIdAsync(offerId);
                if (offer == null)
                    throw new KeyNotFoundException($"Swap offer {offerId} not found while flagging.");

                if (!offer.IsFlagged)   // avoid unnecessary updates
                {
                    offer.IsFlagged = true;
                    await _offerRepo.UpdateAsync(offer);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error flagging offer {OfferId}", offerId);
                throw;
            }
        }

        public async Task<IEnumerable<TblOfferFlag>> GetPendingFlagsAsync()
        {
            return await _ctx.TblOfferFlags
                .Include(f => f.Offer)            // <- load the Offer
                .Include(f => f.FlaggedByUser)    // <- load the User who flagged
                .Where(f => !f.Offer.IsDeleted)   // only non-deleted offers
                .OrderByDescending(f => f.FlaggedDate)
                .ToListAsync();
        }

        public async Task DismissFlagAsync(int flagId)
        {
            // 1) load and delete the individual flag
            var flag = await _flagRepo.GetByIdAsync(flagId);
            if (flag == null)
                throw new KeyNotFoundException($"Flag {flagId} not found.");

            int offerId = flag.OfferId;
            await _flagRepo.DeleteAsync(flag);

            // 2) see if any flags remain on this offer
            var remainingFlags = await _flagRepo.GetByOfferIdAsync(offerId);
            if (!remainingFlags.Any())
            {
                // 3) no more flags → clear the IsFlagged bit
                var offer = await _offerRepo.GetByIdAsync(offerId);
                if (offer is null)
                    throw new KeyNotFoundException($"Swap offer {offerId} not found.");

                if (offer.IsFlagged)
                {
                    offer.IsFlagged = false;
                    await _offerRepo.UpdateAsync(offer);
                }
            }
        }

        public async Task RemoveOfferAsync(int flagId)
        {
            var flag = await _flagRepo.GetByIdAsync(flagId);
            if (flag == null)
                throw new KeyNotFoundException($"Flag {flagId} not found.");

            var offer = await _offerRepo.GetByIdAsync(flag.OfferId);
            if (offer == null)
                throw new KeyNotFoundException($"Swap Offer {flag.OfferId} not found.");

            // mark it deleted
            offer.IsDeleted = true;
            offer.DeletedDate = DateTime.UtcNow;
            await _offerRepo.UpdateAsync(offer);

            // and clear all flags (including this one)
            var allFlags = await _flagRepo.GetByOfferIdAsync(flag.OfferId);
            foreach (var f in allFlags)
                await _flagRepo.DeleteAsync(f);
        }
    }
}
