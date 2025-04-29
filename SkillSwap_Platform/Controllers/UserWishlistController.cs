using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public UserWishlistController(IWishlistService wishlist, ILogger<UserWishlistController> logger)
        {
            _wishlist = wishlist;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int offerId)
        {
            try
            {
                await _wishlist.AddToWishlistAsync(GetCurrentUserId(), offerId);
                return RedirectToAction(
                    actionName: "OfferDetails",
                    controllerName: "UserOfferDetails",
                    routeValues: new { offerId });
            }
            catch
            {
                TempData["ErrorMessage"] = "Failed to add to wishlist.";
                return RedirectToAction("Index", "Offer");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int offerId)
        {
            try
            {
                await _wishlist.RemoveFromWishlistAsync(GetCurrentUserId(), offerId);
                return RedirectToAction(
                    actionName: "OfferDetails",
                    controllerName: "UserOfferDetails",
                    routeValues: new { offerId });
            }
            catch
            {
                TempData["ErrorMessage"] = "Failed to remove from wishlist.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}