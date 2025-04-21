using SkillSwap_Platform.Models.ViewModels.WishlistVM;

namespace SkillSwap_Platform.Services.Wishlist
{
    public interface IWishlistService
    {
        Task AddToWishlistAsync(int userId, int offerId);
        Task RemoveFromWishlistAsync(int userId, int offerId);
        Task<bool> ExistsAsync(int userId, int offerId);
        Task<WishlistPagedVm> GetUserWishlistAsync(int userId, int page, int pageSize);
    }
}
