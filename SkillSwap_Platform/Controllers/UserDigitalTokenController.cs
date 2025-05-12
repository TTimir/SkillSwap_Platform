using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.TokenStatementVM;

namespace SkillSwap_Platform.Controllers
{
    public class UserDigitalTokenController : Controller
    {
        private readonly SkillSwapDbContext _db;
        public UserDigitalTokenController(SkillSwapDbContext db)
        {
            _db = db;
        }

        // GET: /Token/Statements
        public async Task<IActionResult> DigitalTokenStatements(int page = 1, int pageSize = 10)
        {
            int currentUserId = GetCurrentUserId();

            // 1) load all transactions involving this user
            var allTx = await _db.TblTokenTransactions
                .AsNoTracking()
                .Where(t => t.FromUserId == currentUserId || t.ToUserId == currentUserId)
                .Include(t => t.FromUser)
                .Include(t => t.ToUser)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // 2) project into our view-model
            var txVms = allTx
                        .Select(t =>
                        {
                            // decide which side is the counterparty
                            var isOutgoing = t.FromUserId == currentUserId;
                            var counterparty = isOutgoing
                                ? t.ToUser           // make sure ToUser isn't null 
                                : t.FromUser;        // likewise for FromUser

                            return new TokenTransactionVm
                            {
                                Date = t.CreatedAt,
                                Type = t.TxType,
                                Detail = t.Description,
                                Amount = t.Amount,
                                Highlight = false,
                                CounterpartyId = isOutgoing
                                                      ? t.ToUserId.GetValueOrDefault()
                                                      : t.FromUserId.GetValueOrDefault(),
                                CounterpartyName = counterparty?.UserName ?? "—"
                            };
                        })
                        .ToList();

            // 1) Sum of everything ever credited to this user
            decimal totalReceived = await _db.TblTokenTransactions
                .Where(tx => tx.ToUserId == currentUserId)
                .SumAsync(tx => (decimal?)tx.Amount) ?? 0m;

            decimal futureReceived = await (
                from tx in _db.TblTokenTransactions
                join ex in _db.TblExchanges on tx.ExchangeId equals ex.ExchangeId
                where tx.TxType == "Hold"
                      && !tx.IsReleased
                      && ex.OfferOwnerId == currentUserId
                select (decimal?)tx.Amount
            ).SumAsync() ?? 0m;

            // 2) Sum of everything ever debited from this user (excl. puts back into their balance)
            decimal totalSpent = await _db.TblTokenTransactions
                .Where(tx => tx.FromUserId == currentUserId && tx.TxType != "Release")
                .SumAsync(tx => (decimal?)tx.Amount) ?? 0m;

            // 3) Sum of tokens currently held in escrow
            decimal totalHeld = await _db.TblTokenTransactions
                .Where(tx => tx.FromUserId == currentUserId
                          && tx.TxType == "Hold"
                          && !tx.IsReleased)
                .SumAsync(tx => (decimal?)tx.Amount) ?? 0m;

            decimal available = (await _db.TblUsers
                .AsNoTracking()
                .Where(u => u.UserId == currentUserId)
                .Select(u => (decimal?)u.DigitalTokenBalance)
                .SingleOrDefaultAsync()) ?? 0m;

            var baseQuery = _db.TblTokenTransactions
                .AsNoTracking()
                .Where(t => t.FromUserId == currentUserId || t.ToUserId == currentUserId);

            // total rows for pager
            int totalCount = await baseQuery.CountAsync();

            // fetch only the current page
            var query =
                from t in _db.TblTokenTransactions
                join fu in _db.TblUsers on t.FromUserId equals fu.UserId
                join tu in _db.TblUsers on t.ToUserId equals tu.UserId
                join ex in _db.TblExchanges on t.ExchangeId equals ex.ExchangeId into exg
                from ex in exg.DefaultIfEmpty()
                join owner in _db.TblUsers on ex.OfferOwnerId equals owner.UserId into owng
                from owner in owng.DefaultIfEmpty()
                where t.FromUserId == currentUserId || t.ToUserId == currentUserId
                orderby t.CreatedAt descending
                select new TokenTransactionVm
                {
                    Date = t.CreatedAt,
                    Type = t.TxType,
                    Detail = t.Description,
                    Amount = t.Amount,
                    Highlight = false,

                    CounterpartyId = (t.TxType == "Hold" || t.TxType == "Release")
                                        // cast the nullable to int
                                        ? ex.OfferOwnerId.Value
                                        : (t.FromUserId == currentUserId
                                            ? t.ToUserId.Value
                                            : t.FromUserId.Value),

                    CounterpartyName = (t.TxType == "Hold" || t.TxType == "Release")
                                       ? owner.UserName
                                       : (t.FromUserId == currentUserId ? tu.UserName : fu.UserName)
                };

            // apply paging
            var pagedTx = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 5) assemble the VM
            var model = new TokenStatementVM
            {
                NetTokensReceived = totalReceived,
                FutureReceivedCount = futureReceived,
                TokensSpent = totalSpent,
                TotalHeld = totalHeld,
                AvailableForWithdrawal = available,
                Transactions = pagedTx,

                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out var id))
                return id;
            throw new Exception("User not authenticated");
        }
    }
}
