using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.WishlistVM;
using SkillSwap_Platform.Services.Wishlist;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class UserWishlistController : Controller
    {
        private readonly IWishlistService _wishlist;
        private readonly ILogger<UserWishlistController> _logger;
        private readonly IConfiguration _config;
        private readonly SkillSwapDbContext _dbcontext;

        public UserWishlistController(IWishlistService wishlist, ILogger<UserWishlistController> logger, IConfiguration config, SkillSwapDbContext dbContext)
        {
            _wishlist = wishlist;
            _logger = logger;
            _config = config;
            _dbcontext = dbContext;
        }

        #region Helpers
        private int GetCurrentUserId()
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
                throw new InvalidOperationException("User ID claim missing");
            return id;
        }
        #endregion

        #region List
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var vm = await _wishlist.GetUserWishlistAsync(GetCurrentUserId(), page, pageSize);
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to load wishlist for user {User}", User.Identity?.Name);
                TempData["ErrorMessage"] = "Could not load your wishlist.";
                // make sure Items is at least an empty list
                return View(new WishlistPagedVm
                {
                    Items = new List<OfferWishlistVm>(),
                    CurrentPage = 1,
                    TotalCount = 0,
                    PageSize = pageSize
                });
            }
        }
        #endregion

        #region Add / Remove
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int offerId)
        {
            await using var tx = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                await _wishlist.AddToWishlistAsync(GetCurrentUserId(), offerId);
                await tx.CommitAsync();
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId });
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "Failed to add to wishlist.";
                return RedirectToAction("EP500", "EP");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int offerId)
        {
            await using var tx = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                await _wishlist.RemoveFromWishlistAsync(GetCurrentUserId(), offerId);
                await tx.CommitAsync();
                return RedirectToAction("OfferDetails", "UserOfferDetails", new { offerId });
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "Failed to remove from wishlist.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion
    }
}