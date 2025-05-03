using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.Escrow.EscrowDashboard;
using SkillSwap_Platform.Services.DigitalToken;
using SkillSwap_Platform.Services.Email;
using System.Xml.Linq;

namespace SkillSwap_Platform.Services.AdminControls.Escrow
{
    public class EscrowService : IEscrowService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IDigitalTokenService _tokens;
        private readonly IEmailService _email;
        private readonly ILogger<EscrowService> _logger;

        public EscrowService(
            SkillSwapDbContext db,
            IDigitalTokenService tokens,
            IEmailService email,
            ILogger<EscrowService> logger)
        {
            _db = db;
            _tokens = tokens;
            _email = email;
            _logger = logger;
        }

        public async Task<PagedResult<TblEscrow>> GetAllAsync(int page, int pageSize)
        {
            var q = _db.TblEscrows
                       .Include(e => e.Buyer)
                       .Include(e => e.Seller)
                       .OrderByDescending(e => e.CreatedAt);
            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<TblEscrow>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
            return result;
        }

        public async Task<PagedResult<EscrowHistoryVm>> GetHistoryAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;

            try
            {
                // 1) base query with users joined
                var query = _db.TblTokenTransactions
                    .AsNoTracking()
                    .Include(tx => tx.FromUser)   // navigation property for FromUserId
                    .Include(tx => tx.ToUser)     // navigation property for ToUserId
                    .OrderByDescending(tx => tx.CreatedAt);

                // 2) total count
                var total = await query.CountAsync();

                // 3) page
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(tx => new EscrowHistoryVm
                    {
                        TransactionId = tx.TransactionId,
                        ExchangeId = tx.ExchangeId ?? 0,
                        FromUserName = tx.FromUser!.UserName,
                        ToUserName = tx.ToUser!.UserName,
                        Amount = tx.Amount,
                        TxType = tx.TxType,
                        CreatedAt = tx.CreatedAt,
                        Description = tx.Description,
                        IsReleased = tx.IsReleased
                    })
                    .ToListAsync();

                // 4) return
                return new PagedResult<EscrowHistoryVm>
                {
                    Items = items,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading escrow transaction history (page {Page})", page);
                throw;
            }
        }

        public async Task<EscrowDashboardVm> GetDashboardAsync(int historyPage, int historyPageSize)
        {
            // ensure valid paging
            if (historyPage < 1) historyPage = 1;
            if (historyPageSize < 1) historyPageSize = 20;

            try
            {
                // 1) compute totals
                var totalEscrowed = await _db.TblEscrows.SumAsync(e => e.Amount);
                var totalReleased = await _db.TblEscrows
                                            .Where(e => e.Status == "Released")
                                            .SumAsync(e => e.Amount);
                var totalRefunded = await _db.TblEscrows
                                            .Where(e => e.Status == "Refunded")
                                            .SumAsync(e => e.Amount);
                var totalPending = await _db.TblEscrows
                                            .Where(e => e.Status == "Pending")
                                            .SumAsync(e => e.Amount);

                // 2) grab the most recent N transactions
                var transactions = await GetHistoryAsync(historyPage, historyPageSize);

                return new EscrowDashboardVm
                {
                    TotalEscrowed = totalEscrowed,
                    TotalReleased = totalReleased,
                    TotalRefunded = totalRefunded,
                    TotalPending = totalPending,
                    RecentTransactions = transactions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building escrow dashboard");
                throw;
            }
        }

        public Task<TblEscrow?> GetByIdAsync(int id)
            => _db.TblEscrows
                  .Include(e => e.Buyer)
                  .Include(e => e.Seller)
                  .Include(e => e.HandledByAdmin)
                  .Include(e => e.Exchange)                // ← load the Exchange
                     .ThenInclude(x => x.Offer)
                  .FirstOrDefaultAsync(e => e.EscrowId == id);

        private async Task _HandleAsync(int id, int adminId, string notes,
                                        string newStatus, Action<TblEscrow> timestampSetter)
        {
            var e = await _db.TblEscrows.FindAsync(id)
                ?? throw new KeyNotFoundException("Escrow not found");
            if (e.Status != "Pending")
                throw new InvalidOperationException("Only pending escrows can be modified");

            // 1) Update escrow record
            e.Status = newStatus;
            timestampSetter(e);
            e.HandledByAdminId = adminId;
            e.AdminNotes = notes;
            _db.TblEscrows.Update(e);
            await _db.SaveChangesAsync();

            // 2) Move the tokens
            if (newStatus == "Released")
                await _tokens.ReleaseTokensAsync(e.ExchangeId);
            else if (newStatus == "Refunded")
                await _tokens.RefundTokensAsync(e.ExchangeId);

            // 3) Log into your exchange-history table
            _db.TblExchangeHistories.Add(new TblExchangeHistory
            {
                ExchangeId = e.ExchangeId,
                ChangedStatus = newStatus,
                ChangedBy = adminId,
                ChangeDate = DateTime.UtcNow,
                Reason = notes
            });

            await _db.SaveChangesAsync();

            var localNow = DateTime.Now;
            var tzName = TimeZoneInfo.Local.IsDaylightSavingTime(localNow)
                            ? TimeZoneInfo.Local.DaylightName
                            : TimeZoneInfo.Local.StandardName;
            // build a subject & HTML body
            string subject = $"Escrow {newStatus}: Exchange #{e.ExchangeId}";
            string html = $@"
              <p>Hi {{Party}},</p>
              <p>Your escrow for <strong>Exchange #{e.ExchangeId}</strong> has been <strong>{newStatus}</strong> by our admin.</p>
              <p>
                <strong>Amount:</strong> {e.Amount:F2} tokens<br/>
                <strong>When:</strong> {localNow:yyyy-MM-dd HH:mm} ({tzName})
              </p>
              <p>Notes from admin:<br/>{notes}</p>
              <hr/>
             <p>If you have any questions, please contact our support team at 
                <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>
              <p>Thanks,<br/>The SkillSwap Team</p>
            ";

            // send both
            await NotifyPartiesAsync(e, subject, html);
        }

        public async Task ReleaseAsync(int id, int adminId, string notes)
        {
            // 1) Update database, move tokens, log history
            await _HandleAsync(id, adminId, notes,
                               "Released",
                               e => e.ReleasedAt = DateTime.UtcNow);

            // 2) Fetch the updated escrow so we have all the fields filled
            var escrow = await GetByIdAsync(id)
                         ?? throw new KeyNotFoundException("Escrow not found after release");

            // 3) Build & send your “released” email
            var localWhen = escrow.ReleasedAt!.Value.ToLocalTime();
            var tzName = TimeZoneInfo.Local.IsDaylightSavingTime(localWhen)
                               ? TimeZoneInfo.Local.DaylightName
                               : TimeZoneInfo.Local.StandardName;

            string subject = $"Escrow Released: Exchange #{escrow.ExchangeId}";
            string html = $@"
                <p>Hi {{Party}},</p>
                <p>Your escrow for <strong>Exchange #{escrow.ExchangeId}</strong> has just been <strong>released</strong>!</p>
                <ul>
                  <li><strong>Amount:</strong> {escrow.Amount:F2} tokens</li>
                  <li><strong>Released At:</strong> {localWhen:yyyy-MM-dd HH:mm} ({tzName})</li>
                  <li><strong>Admin notes:</strong> {notes}</li>
                </ul>
                <p>If you have any questions, please contact our support team at 
                    <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>                
                <hr/>
                <p>Thanks,<br/>The SkillSwap Team</p>";

            await NotifyPartiesAsync(escrow, subject, html);
        }

        public async Task RefundAsync(int id, int adminId, string notes)
        {
            await _HandleAsync(id, adminId, notes,
                               "Refunded",
                               e => e.RefundedAt = DateTime.UtcNow);

            var escrow = await GetByIdAsync(id)
                         ?? throw new KeyNotFoundException("Escrow not found after refund");

            var localWhen = escrow.RefundedAt!.Value.ToLocalTime();
            var tzName = TimeZoneInfo.Local.IsDaylightSavingTime(localWhen)
                               ? TimeZoneInfo.Local.DaylightName
                               : TimeZoneInfo.Local.StandardName;

            string subject = $"Escrow Refunded: Exchange #{escrow.ExchangeId}";
            string html = $@"
                <p>Hi {{Party}},</p>
                <p>Your escrow for <strong>Exchange #{escrow.ExchangeId}</strong> has been <strong>refunded</strong>.</p>
                <ul>
                  <li><strong>Amount:</strong> {escrow.Amount:F2} tokens</li>
                  <li><strong>Refunded At:</strong> {localWhen:yyyy-MM-dd HH:mm} ({tzName})</li>
                  <li><strong>Admin notes:</strong> {notes}</li>
                </ul>
                <p>If you have any questions, please contact our support team at 
                    <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>
                <hr/>
                <p>Thanks,<br/>The SkillSwap Team</p>";

            await NotifyPartiesAsync(escrow, subject, html);
        }

        public async Task DisputeAsync(int id, int adminId, string notes)
        {
            await _HandleAsync(id, adminId, notes,
                               "Disputed",
                               e => e.DisputedAt = DateTime.UtcNow);

            var escrow = await GetByIdAsync(id)
                         ?? throw new KeyNotFoundException("Escrow not found after dispute");

            var localWhen = escrow.DisputedAt!.Value.ToLocalTime();
            var tzName = TimeZoneInfo.Local.IsDaylightSavingTime(localWhen)
                               ? TimeZoneInfo.Local.DaylightName
                               : TimeZoneInfo.Local.StandardName;

            string subject = $"Escrow Disputed: Exchange #{escrow.ExchangeId}";
            string html = $@"
                <p>Hi {{Party}},</p>
                <p>Our admin has <strong>opened a dispute</strong> on your escrow for <strong>Exchange #{escrow.ExchangeId}</strong>.</p>
                <p>We’re reviewing the case now and will be in touch shortly.</p>
                <ul>
                  <li><strong>When:</strong> {localWhen:yyyy-MM-dd HH:mm} ({tzName})</li>
                  <li><strong>Admin notes:</strong> {notes}</li>
                </ul>
                <p>If you have any supporting documents, just reply to this email.</p>
                <hr/>
                <p>Thanks,<br/>The SkillSwap Team</p>";

            await NotifyPartiesAsync(escrow, subject, html);
        }

        // Services/AdminControls/Escrow/EscrowService.cs
        public async Task<TblEscrow> CreateAsync(int exchangeId, int buyerId, int sellerId, decimal amount)
        {
            var escrow = new TblEscrow
            {
                ExchangeId = exchangeId,
                BuyerId = buyerId,
                SellerId = sellerId,
                Amount = amount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.TblEscrows.Add(escrow);
            await _db.SaveChangesAsync();

            var localWhen = escrow.CreatedAt.ToLocalTime();
            var tzName = TimeZoneInfo.Local.IsDaylightSavingTime(localWhen)
                       ? TimeZoneInfo.Local.DaylightName
                       : TimeZoneInfo.Local.StandardName;
            // send a “we’ve locked your tokens” email:
            string subj = $"Escrow Created: Exchange #{escrow.ExchangeId}";
            string body = $@"
              <p>Hi {{Party}},</p>
              <p>We’ve created an escrow for your <strong>Exchange #{escrow.ExchangeId}</strong>:</p>
              <ul>
                <li><strong>Amount:</strong> {escrow.Amount:F2} tokens</li>
                <li><strong>Status:</strong> Pending</li>
                <li><strong>When:</strong> {localWhen:yyyy-MM-dd HH:mm} ({tzName}),</li>
              </ul>
              <p>We’ll let you know as soon as the admin releases or refunds these tokens.</p>
              <hr/>
              <p>Thanks,<br/>The SkillSwap Team</p>
            ";
            await NotifyPartiesAsync(escrow, subj, body);

            return escrow;

            return escrow;
        }

        private async Task NotifyPartiesAsync(TblEscrow e, string subject, string htmlBody)
        {
            // Buyer
            var buyer = await _db.TblUsers.FindAsync(e.BuyerId);
            if (buyer != null)
            {
                await _email.SendEmailAsync(
                    to: buyer.Email,
                    subject: subject,
                    body: htmlBody.Replace("{Party}", "Buyer"),
                    isBodyHtml: true
                );
            }
            else
            {
                _logger.LogWarning(
                    "EscrowService.NotifyPartiesAsync: buyer with ID {BuyerId} not found for Escrow {EscrowId}",
                    e.BuyerId, e.EscrowId);
            }

            // Seller
            var seller = await _db.TblUsers.FindAsync(e.SellerId);
            if (seller != null)
            {
                await _email.SendEmailAsync(
                    to: seller.Email,
                    subject: subject,
                    body: htmlBody.Replace("{Party}", "Seller"),
                    isBodyHtml: true
                );
            }
            else
            {
                _logger.LogWarning(
                    "EscrowService.NotifyPartiesAsync: seller with ID {SellerId} not found for Escrow {EscrowId}",
                    e.SellerId, e.EscrowId);
            }
        }

    }
}
