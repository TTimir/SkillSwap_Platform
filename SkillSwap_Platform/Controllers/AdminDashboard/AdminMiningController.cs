using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.AdminControl.TokenMining;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminMiningController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<AdminMiningController> _logger;

        public AdminMiningController(SkillSwapDbContext db, ILogger<AdminMiningController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                var miningSummary = new UserMiningProgressVM
                {
                    TotalUsersMiningAllowed = _db.UserMiningProgresses.Count(x => x.IsMiningAllowed),
                    TotalUsersMiningBlocked = _db.UserMiningProgresses.Count(x => !x.IsMiningAllowed),
                    TotalTokensEmittedToday = _db.MiningLogs
                        .Where(x => x.EmittedUtc.Date == today)
                        .Sum(x => (decimal?)x.Amount) ?? 0
                };

                var currentEmissionSettings = _db.TokenEmissionSettings
                    .Where(x => x.IsEnabled)
                    .OrderByDescending(x => x.StartDateUtc)
                    .Select(x => new TokenEmissionSettingsVM
                    {
                        Id = x.Id,
                        TotalPool = x.TotalPool,
                        StartDateUtc = x.StartDateUtc,
                        DripDays = x.DripDays,
                        HalvingPeriodDays = x.HalvingPeriodDays,
                        DailyCap = x.DailyCap,
                        IsEnabled = x.IsEnabled
                    })
                    .FirstOrDefault();

                var recentTopMiners = new List<RecentMiningLogVM>();

                // Load miners only if mining settings are active
                if (currentEmissionSettings != null)
                {
                    recentTopMiners = _db.MiningLogs
                        .Where(x => x.EmittedUtc >= today)
                        .GroupBy(x => x.UserId)
                        .Select(g => new RecentMiningLogVM
                        {
                            UserId = g.Key,
                            TotalMinedAmount = g.Sum(x => x.Amount),
                            LastEmittedUtc = g.Max(x => x.EmittedUtc)
                        })
                        .OrderByDescending(x => x.TotalMinedAmount)
                        .Take(10)
                        .ToList();
                }

                var userMiningStatusList = _db.UserMiningProgresses
                        .Join(_db.TblUsers.Where(x => !x.IsEscrowAccount),
                              mining => mining.UserId,
                              user => user.UserId,
                              (mining, user) => new
                              {
                                  user.UserId,
                                  mining.IsMiningAllowed,
                                  user.FirstName,
                                  user.LastName,
                                  user.UserName,
                                  user.Email
                              })
                        .AsEnumerable() // 🔑 Force switch to client-side here
                        .Select(x => new UserMiningStatusVM
                        {
                            UserId = x.UserId,
                            IsMiningAllowed = x.IsMiningAllowed,
                            FullName = $"{(x.FirstName ?? "")} {(x.LastName ?? "")} ({x.UserName ?? "unknown"})",
                            Email = x.Email
                        })
                        .OrderBy(x => x.FullName)
                        .ToList();

                var viewModel = new AdminMiningDashboardVM
                {
                    MiningSummary = miningSummary,
                    CurrentEmissionSettings = currentEmissionSettings ?? new TokenEmissionSettingsVM(),
                    RecentTopMiners = recentTopMiners,
                    UserMiningStatusList = userMiningStatusList
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mining admin dashboard.");
                TempData["Error"] = "Error loading dashboard data.";

                // Properly initialize all properties here
                var safeViewModel = new AdminMiningDashboardVM
                {
                    MiningSummary = new UserMiningProgressVM(),
                    CurrentEmissionSettings = new TokenEmissionSettingsVM(),
                    RecentTopMiners = new List<RecentMiningLogVM>()
                };

                return View(safeViewModel);
            }
        }

        [HttpPost("update-emission-settings")]
        public IActionResult UpdateEmissionSettings(TokenEmissionSettingsVM model)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var settings = _db.TokenEmissionSettings.FirstOrDefault(x => x.Id == model.Id);
                if (settings == null)
                    return NotFound();

                settings.TotalPool = model.TotalPool;
                settings.DripDays = model.DripDays;
                settings.HalvingPeriodDays = model.HalvingPeriodDays;
                settings.DailyCap = model.DailyCap;
                settings.IsEnabled = model.IsEnabled;

                _db.SaveChanges();
                transaction.Commit();

                TempData["Success"] = "Emission settings updated successfully.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to update emission settings.");
                TempData["Error"] = "Failed to update emission settings.";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost("toggle-user-mining")]
        public IActionResult ToggleUserMining(int userId, bool allowMining)
        {
            try
            {
                var userMining = _db.UserMiningProgresses.FirstOrDefault(x => x.UserId == userId);
                if (userMining == null)
                    return NotFound();

                userMining.IsMiningAllowed = allowMining;
                _db.SaveChanges();

                TempData["Success"] = allowMining ? "User mining enabled." : "User mining blocked.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user mining.");
                TempData["Error"] = "Failed to update mining status.";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpGet("user-mining-logs/{userId}")]
        public IActionResult UserMiningLogs(int userId, int page = 1, int pageSize = 20)
        {
            var totalLogs = _db.MiningLogs.Count(x => x.UserId == userId);
            var totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            var user = _db.TblUsers.FirstOrDefault(u => u.UserId == userId);
            var userName = user?.UserName ?? "Unknown";

            var logs = _db.MiningLogs
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.EmittedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new MiningLogVM
                {
                    Id = (int)x.Id,
                    UserId = x.UserId,
                    Username = x.User.UserName,
                    Amount = x.Amount,
                    EmittedUtc = x.EmittedUtc
                })
                .ToList();

            ViewBag.UserId = userId;
            ViewBag.Username = userName;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(logs);
        }

        [HttpPost("toggle-mining-global")]
        public IActionResult ToggleMiningGlobal(bool enableMining)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var allSettings = _db.TokenEmissionSettings.ToList();

                foreach (var setting in allSettings)
                    setting.IsEnabled = enableMining;

                _db.SaveChanges();
                transaction.Commit();

                TempData["Success"] = enableMining ? "Mining globally enabled." : "Mining globally disabled.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error toggling global mining.");
                TempData["Error"] = "Failed to toggle global mining.";
            }

            return RedirectToAction("Dashboard");
        }
    }
}