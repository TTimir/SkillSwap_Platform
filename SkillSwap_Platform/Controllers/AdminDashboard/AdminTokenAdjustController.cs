using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.TokenReserve;
using SkillSwap_Platform.Services;
using System.Drawing.Printing;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    public class AdminTokenAdjustController : Controller
    {
        private readonly TokenAdminService _svc;
        private readonly SkillSwapDbContext _db;
        private const int EscrowUserId = 1;
        private const int SystemReserveUserId = 2;

        public AdminTokenAdjustController(TokenAdminService svc, SkillSwapDbContext db) { _svc = svc; _db = db; }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            PopulateUserList();
            // build the base query
            var query = from t in _db.TblTokenTransactions
                        where t.TxType == "AdminAdjustment"
                        let uid = t.ToUserId ?? t.FromUserId
                        join u in _db.TblUsers on uid equals u.UserId
                        select new AdminAdjustListItem
                        {
                            TransactionId = t.TransactionId,
                            UserName = u.UserName,
                            Amount = t.Amount,
                            ToUserId = t.ToUserId,
                            FromUserId = t.FromUserId,
                            AdminAdjustmentType = t.AdminAdjustmentType,
                            AdminAdjustmentReason = t.AdminAdjustmentReason,
                            OldBalance = t.OldBalance.Value,
                            NewBalance = t.NewBalance.Value,
                            CreatedAt = t.CreatedAt,
                            RequiresApproval = t.RequiresApproval,
                            IsApproved = t.IsApproved
                        };

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new AdminAdjustListViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };

            return View(vm);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            PopulateUserList();
            return View(new AdminAdjustDto());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminAdjustDto dto)
        {
            if (!ModelState.IsValid)
            {
                PopulateUserList();
                return View(dto);
            }

            // dto: { UserId, Amount, Type, Reason }
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var tx = await _svc.AdjustBalanceAsync(dto.UserId, dto.Amount, dto.Type, dto.Reason, adminId);

            TempData["Success"] = "Token adjustment saved successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("/api/admin/token-adjust")]
        public async Task<IActionResult> CreateApi(AdminAdjustDto dto)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var tx = await _svc.AdjustBalanceAsync(
                dto.UserId, dto.Amount, dto.Type, dto.Reason, adminId
            );
            return CreatedAtAction(nameof(GetApi), new { id = tx.TransactionId }, tx);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetApi(int id)
        {
            var tx = await _svc.GetTransactionAsync(id);
            return tx == null ? NotFound() : Ok(tx);
        }

        private void PopulateUserList()
        {
            ViewBag.UserList = new SelectList(
                _db.TblUsers
                   .IgnoreQueryFilters()
                   .AsNoTracking()
                   .Where(u => u.Role.ToLower() != "user")
                   .Select(u => new {
                       u.UserId,
                       Display = u.UserName + " (" + u.Email + ") — " + u.Role
                   })
                   .ToList(),
                "UserId",
                "Display"
            );
        }
    }
}
