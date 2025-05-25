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
            var subjectUpdate = $"Escrow {newStatus}: Exchange #{e.ExchangeId}";
            var bodyUpdate = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Info Blue -->
      <tr>
        <td style=""background:#0288D1;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          Swapo Escrow Update
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {{Party}},</h2>
          <p>Your escrow for <strong>Exchange #{e.ExchangeId}</strong> on the offer 
            <strong>{{ {{OfferTitle}} }}</strong>:</p> has been <strong>{newStatus}</strong> by our admin.</p>
          <p>
            <strong>Amount:</strong> {e.Amount.ToString("F2")} tokens<br/>
            <strong>On Date:</strong> {localNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm")} ({tzName})
          </p>
          <p>Notes from admin:<br/>{notes}</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          If you have any questions, contact <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">swapoorg360@gmail.com</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
            var offerTitle = e.Exchange?.Offer?.Title ?? "your offer";
            await NotifyPartiesAsync(
                e,
                subjectUpdate,
                bodyUpdate,
                offerTitle,
                isBodyHtml: true
            );
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

            var subjectReleased = $"Escrow Released: Exchange #{escrow.ExchangeId}";
            var bodyReleased = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Success Green -->
      <tr>
        <td style=""background:#388E3C;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          Swapo Escrow Released
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {{Party}},</h2>
          <p>Your escrow for <strong>Exchange #{escrow.ExchangeId}</strong> on the offer 
            <strong>{{ {{OfferTitle}} }}</strong>:</p> has just been <strong>released</strong>!</p>
          <ul style=""padding-left:1em;margin:0;"">
            <li><strong>Amount:</strong> {escrow.Amount.ToString("F2")} tokens</li>
            <li><strong>Released At:</strong> {localWhen.ToLocalTime().ToString("yyyy-MM-dd HH:mm")} ({tzName})</li>
            <li><strong>Admin notes:</strong> {notes}</li>
          </ul>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Questions? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
            var offerTitle = escrow.Exchange?.Offer?.Title ?? "your offer";
            await NotifyPartiesAsync(
                escrow,
                subjectReleased,
                bodyReleased,
                offerTitle,
                isBodyHtml: true
            );
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

            var subjectRefunded = $"Escrow Refunded: Exchange #{escrow.ExchangeId}";
            var bodyRefunded = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Success Green -->
      <tr>
        <td style=""background:#388E3C;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          Swapo Escrow Refunded
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {{Party}},</h2>
          <p>Your escrow for <strong>Exchange #{escrow.ExchangeId}</strong> on the offer 
            <strong>{{ {{OfferTitle}} }}</strong></p> has been <strong>refunded</strong>.</p>
          <ul style=""padding-left:1em;margin:0;"">
            <li><strong>Amount:</strong> {escrow.Amount.ToString("F2")} tokens</li>
            <li><strong>Refunded At:</strong> {localWhen.ToLocalTime().ToString("yyyy-MM-dd HH:mm")} ({tzName})</li>
            <li><strong>Admin notes:</strong> {notes}</li>
          </ul>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Need help? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
            var offerTitle = escrow.Exchange?.Offer?.Title ?? "your offer";
            await NotifyPartiesAsync(
                escrow,
                subjectRefunded,
                bodyRefunded,
                offerTitle,
                isBodyHtml: true
            );
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

            var subjectDisputed = $"Escrow Disputed: Exchange #{escrow.ExchangeId}";
            var bodyDisputed = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Warning Orange -->
      <tr>
        <td style=""background:#F57C00;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          Swapo Escrow Dispute
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {{Party}},</h2>
          <p>Our admin has <strong>opened a dispute</strong> on your escrow for <strong>Exchange #{escrow.ExchangeId}</strong> on the offer 
            <strong>{{ {{OfferTitle}} }}</strong>:</p>
          <p>We’re reviewing the case now and will be in touch shortly.</p>
          <ul style=""padding-left:1em;margin:0;"">
            <li><strong>On Date:</strong> {localWhen.ToLocalTime().ToString("yyyy-MM-dd HH:mm")} ({tzName})</li>
            <li><strong>Admin notes:</strong> {notes}</li>
          </ul>
          <p>If you have any supporting documents, please reply to this email.</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Thank you for your patience—<a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">contact support</a> if needed.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
            var offerTitle = escrow.Exchange?.Offer?.Title ?? "your offer";
            await NotifyPartiesAsync(
                escrow,
                subjectDisputed,
                bodyDisputed,
                offerTitle,
                isBodyHtml: true
            );
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
            var subjectCreated = $"Escrow Created: Exchange #{escrow.ExchangeId}";
            var bodyCreated = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Purple -->
      <tr>
        <td style=""background:#6A1B9A;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          Swapo Escrow Created
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {{Party}},</h2>
          <p>We’ve created an escrow for your <strong>Exchange #{escrow.ExchangeId}</strong> on the offer 
            <strong>{{ {{OfferTitle}} }}</strong>:</p>
          <ul style=""padding-left:1em;margin:0;"">
            <li><strong>Amount:</strong> {escrow.Amount.ToString("F2")} tokens</li>
            <li><strong>Status:</strong> Held on escrow A/C</li>
            <li><strong>On Date:</strong> {localWhen.ToLocalTime().ToString("yyyy-MM-dd HH:mm")} ({tzName})</li>
          </ul>
            <p>Your tokens are safely held in escrow until your exchange is complete—no action is required on your part. We’ll let you know as soon as the exchange completes and confirmed by both swapper.</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Any questions? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
            var offerTitle = escrow.Exchange?.Offer?.Title ?? "your offer";
            await NotifyPartiesAsync(
                escrow,
                subjectCreated,
                bodyCreated,
                offerTitle,
                isBodyHtml: true
            );
            return escrow;
        }

        private async Task NotifyPartiesAsync(TblEscrow e, string subject, string htmlTemplate, string htmlBody, bool isBodyHtml)
        {
            var offerTitle = e.Exchange?.Offer?.Title ?? "your offer";

            // Buyer
            var buyer = await _db.TblUsers.FindAsync(e.BuyerId);
            if (buyer != null)
            {
                var personalizedBody = htmlTemplate
                    .Replace("{PartyName}", buyer.UserName)
                    .Replace("{OfferTitle}", offerTitle);

                await _email.SendEmailAsync(
                    to: buyer.Email,
                    subject: subject,
                    body: personalizedBody,
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
                var personalizedBody = htmlTemplate
                    .Replace("{PartyName}", seller.UserName)
                    .Replace("{OfferTitle}", offerTitle);

                await _email.SendEmailAsync(
                    to: seller.Email,
                    subject: subject,
                    body: personalizedBody,
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
