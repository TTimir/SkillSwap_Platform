﻿using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.AdminControls.AdminSearch;
using SkillSwap_Platform.Services.AdminControls;
using Microsoft.AspNetCore.Authorization;
using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;
using System.Security.Claims;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.AdminControl.AdminSearch;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator, Support Agent")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminSearchController : Controller
    {
        private readonly IAdminSearchService _searchService;
        private readonly ILogger<AdminSearchController> _logger;

        public AdminSearchController(
            IAdminSearchService searchService,
            ILogger<AdminSearchController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        #region Search Offers
        [HttpGet]
        public async Task<IActionResult> Offers(string term = "", int page = 1)
        {
            try
            {
                var criteria = new SearchCriteria
                {
                    Term = term,
                    Page = page,
                    PageSize = 10
                };

                var results = await _searchService.SearchOffersAsync(criteria);
                var vm = new OfferSearchVM
                {
                    Term = term,
                    Results = results
                };
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to search offers. Please try again.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Offer Details
        [HttpGet]
        public async Task<IActionResult> OfferDetails(int id)
        {
            if (id <= 0) return BadRequest();
            try
            {
                // 1) get current user ID from claims
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdString, out var currentUserId))
                {
                    TempData["ErrorMessage"] = "Invalid user context.";
                    return RedirectToAction("Offers");
                }

                // 2) pass both parameters
                var dto = await _searchService.GetOfferDetailAsync(id, currentUserId);
                return View(new OfferDetailVM(dto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load details for offer {OfferId}", id);
                TempData["ErrorMessage"] = "Unable to load offer details.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Search Users
        [HttpGet]
        public async Task<IActionResult> Users(string? term, int page = 1)
        {
            var criteria = new SearchCriteria { Term = term, Page = page };
            try
            {
                var result = await _searchService.SearchUsersAsync(criteria);
                ViewBag.Term = term;
                return View(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user search results");
                TempData["ErrorMessage"] = "Unable to load users. Please try again later.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region User Details
        [HttpGet]
        public async Task<IActionResult> UserDetails(int id)
        {
            if (id <= 0) return BadRequest();
            try
            {
                var dto = await _searchService.GetUserDetailAsync(id);
                return View(new UserDetailVM(dto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load details for user {UserId}", id);
                TempData["ErrorMessage"] = "Unable to load user details.";
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

    }
}