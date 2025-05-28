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

            // 1) Load everything: your sent/received txns + only escrow txns for exchanges you own/requested
            var txWithUsers = await (
                from t in _db.TblTokenTransactions.AsNoTracking()

                    // bring in the exchange (if any)
                join ex in _db.TblExchanges on t.ExchangeId equals ex.ExchangeId into exG
                from exch in exG.DefaultIfEmpty()

                    // only include:
                    //  - any txn where you're the sender or receiver
                    //  OR
                    //  - escrow txns (Hold/Release) for exchanges you either own or requested
                where
                    t.FromUserId == currentUserId
                 || t.ToUserId == currentUserId
                 || ((t.TxType == "Hold" || t.TxType == "Release")
                     && exch != null
                     && (exch.OfferOwnerId == currentUserId
                      || exch.OtherUserId == currentUserId))

                // bring in the two “normal” users
                join fu in _db.TblUsers on t.FromUserId equals fu.UserId into fuG
                from fromUser in fuG.DefaultIfEmpty()

                join tu in _db.TblUsers on t.ToUserId equals tu.UserId into tuG
                from toUser in tuG.DefaultIfEmpty()

                    // bring in offer-owner & requester for escrow
                join ow in _db.TblUsers on exch.OfferOwnerId equals ow.UserId into owG
                from offerOwner in owG.DefaultIfEmpty()

                join rq in _db.TblUsers on exch.OtherUserId equals rq.UserId into rqG
                from requester in rqG.DefaultIfEmpty()

                orderby t.CreatedAt descending

                select new
                {
                    Tx = t,
                    FromUser = fromUser,
                    ToUser = toUser,
                    Exchange = exch,
                    OfferOwner = offerOwner,
                    Requester = requester
                }
            ).ToListAsync();

            // 2) Map into your VM, special-casing escrow
            var txVms = txWithUsers.Select(x =>
            {
                var t = x.Tx;
                var ex = x.Exchange;

                // Non-escrow: use normal from↔to
                if (t.TxType != "Hold" && t.TxType != "Release")
                {
                    bool outgoing = t.FromUserId == currentUserId;
                    var cp = outgoing ? x.ToUser : x.FromUser;
                    return new TokenTransactionVm
                    {
                        Date = t.CreatedAt,
                        Type = t.TxType,
                        Detail = t.Description,
                        Amount = t.Amount,
                        CounterpartyId = cp?.UserId ?? 0,
                        CounterpartyName = cp?.UserName ?? "—"
                    };
                }

                // Hold: always show the service-owner
                if (t.TxType == "Hold")
                {
                    return new TokenTransactionVm
                    {
                        Date = t.CreatedAt,
                        Type = t.TxType,
                        Detail = t.Description,
                        Amount = t.Amount,
                        CounterpartyId = x.OfferOwner?.UserId ?? 0,
                        CounterpartyName = x.OfferOwner?.UserName ?? "—"
                    };
                }

                // Release: owner sees outgoing → requester; requester sees incoming ← owner
                bool isOwner = ex != null && ex.OfferOwnerId == currentUserId;
                bool isRequester = ex != null && ex.OtherUserId == currentUserId;

                if (t.TxType == "Release" && isOwner)
                {
                    return new TokenTransactionVm
                    {
                        Date = t.CreatedAt,
                        Type = t.TxType,
                        Detail = t.Description,
                        Amount = t.Amount,
                        CounterpartyId = x.Requester?.UserId ?? 0,
                        CounterpartyName = x.Requester?.UserName ?? "—"
                    };
                }
                else if (t.TxType == "Release" && isRequester)
                {
                    return new TokenTransactionVm
                    {
                        Date = t.CreatedAt,
                        Type = t.TxType,
                        Detail = t.Description,
                        Amount = t.Amount,
                        CounterpartyId = x.OfferOwner?.UserId ?? 0,
                        CounterpartyName = x.OfferOwner?.UserName ?? "—"
                    };
                }

                // Fallback
                return new TokenTransactionVm
                {
                    Date = t.CreatedAt,
                    Type = t.TxType,
                    Detail = t.Description,
                    Amount = t.Amount,
                    CounterpartyId = 0,
                    CounterpartyName = "—"
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

            var totalCount = txVms.Count;
            var pagedTx = txVms
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

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
