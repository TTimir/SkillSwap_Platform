using Newtonsoft.Json;
using SkillSwap_Platform.Models.ViewModels.WishlistVM;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.Services.Wishlist
{
    public class WishlistService : IWishlistService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<WishlistService> _logger;

        public WishlistService(SkillSwapDbContext db, ILogger<WishlistService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task AddToWishlistAsync(int userId, int offerId)
        {
            try
            {
                if (await _db.TblUserWishlists.AnyAsync(w => w.UserId == userId && w.OfferId == offerId))
                    return;

                _db.TblUserWishlists.Add(new TblUserWishlist
                {
                    UserId = userId,
                    OfferId = offerId,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add OfferId {OfferId} to wishlist of UserId {UserId}", offerId, userId);
                throw;
            }
        }

        public async Task RemoveFromWishlistAsync(int userId, int offerId)
        {
            try
            {
                var wish = await _db.TblUserWishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.OfferId == offerId);
                if (wish == null) return;

                _db.TblUserWishlists.Remove(wish);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove OfferId {OfferId} from wishlist of UserId {UserId}", offerId, userId);
                throw;
            }
        }

        public Task<bool> ExistsAsync(int userId, int offerId) =>
            _db.TblUserWishlists
               .AnyAsync(w => w.UserId == userId && w.OfferId == offerId);

        public async Task<WishlistPagedVm> GetUserWishlistAsync(int userId, int page, int pageSize)
        {
            try
            {
                // Load Wishlist → Offer → Offer.User
                var baseQuery = _db.TblUserWishlists
                    .AsNoTracking()
                    .Where(w => w.UserId == userId)
                    .Include(w => w.Offer)
                      .ThenInclude(o => o.User);

                var totalCount = await baseQuery.CountAsync();

                //var pageEntries = await _db.TblUserWishlists
                //    .Where(w => w.UserId == userId)
                //    .Include(w => w.Offer)
                //       .ThenInclude(o => o.User)
                //    .AsSingleQuery()      // ← absolutely no CTEs
                //    .OrderByDescending(w => w.CreatedAt)
                //    .Skip((page - 1) * pageSize)
                //    .Take(pageSize)
                //    .ToListAsync();

                var items = await baseQuery
                    .OrderByDescending(w => w.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => new OfferWishlistVm
                    {
                        OfferId = w.OfferId,
                        Title = w.Offer.Title,
                        ThumbnailUrl = !string.IsNullOrEmpty(w.Offer.Portfolio)
                            ? JsonConvert
                                .DeserializeObject<List<string>>(w.Offer.Portfolio)
                                .FirstOrDefault()!
                            : "/template_assets/images/default-offer.png",
                        CreatedAt = w.CreatedAt,

                        Category = w.Offer.Category,

                        // these become scalar subqueries in the SQL—no CTE!
                        ReviewCount = _db.TblReviews
                            .Where(r => r.OfferId == w.OfferId)
                            .Count(),

                        AverageRating = _db.TblReviews
                            .Where(r => r.OfferId == w.OfferId)
                            .Average(r => (double?)r.Rating) ?? 0,

                        OwnerUsername = w.Offer.User.UserName,
                        OwnerProfileImage = w.Offer.User.ProfileImageUrl
                                              ?? "/template_assets/images/No_Profile_img.png",
                        Location = w.Offer.User.City
                    })
                    .ToListAsync();

                return new WishlistPagedVm
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch wishlist for UserId {UserId}", userId);
                throw;
            }
        }
    }
}