using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.NotificationTrack;
using System.Data;
using System.Security.Cryptography;

namespace SkillSwap_Platform.Services.DigitalToken
{
    public class DigitalTokenService : IDigitalTokenService
    {
        private const int EscrowUserId = 2;          // userId of escrow account
        private const int SystemReserveUserId = 3;   // userId of platform reserve (for bonuses)
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<DigitalTokenService> _logger;
        private readonly INotificationService _notif;
        private readonly IEmailService _email;

        public DigitalTokenService(
            SkillSwapDbContext db,
            ILogger<DigitalTokenService> logger,
            INotificationService notif,
            IEmailService email)
        {
            _db = db;
            _logger = logger;
            _notif = notif;
            _email = email;
        }

        public async Task HoldTokensAsync(int exchangeId, CancellationToken ct = default)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var ex = await _db.TblExchanges
                    .Include(e => e.Contract)
                    .Include(e => e.Offer).ThenInclude(o => o.User)
                    .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                if (ex == null) throw new KeyNotFoundException($"Exchange #{exchangeId} not found.");

                if (!ex.Contract.IsContractSigned)
                    throw new InvalidOperationException("Contract not fully signed.");

                var buyer = await _db.TblUsers.FindAsync(ex.OtherUserId)
                  ?? throw new KeyNotFoundException($"Buyer not found.");
                var seller = ex.Offer.User
                              ?? throw new KeyNotFoundException($"Offer owner not found.");

                var cost = ex.TokensPaid;
                if (cost <= 0) throw new InvalidOperationException("Nothing to hold.");
                if (buyer.DigitalTokenBalance < cost)
                    throw new InvalidOperationException("Insufficient balance.");

                // 1) debit buyer
                buyer.DigitalTokenBalance -= cost;
                _db.TblUsers.Update(buyer);

                // 2) credit escrow account
                var escrow = await GetEscrowUserAsync();
                escrow.DigitalTokenBalance += cost;
                _db.TblUsers.Update(escrow);

                // 3) ledger entries
                _db.TblTokenTransactions.AddRange(
                  new TblTokenTransaction
                  {
                      ExchangeId = exchangeId,
                      FromUserId = buyer.UserId,
                      ToUserId = escrow.UserId,
                      Amount = cost,
                      TxType = "Hold",
                      Description = $"Hold for exchange #{exchangeId} – \"{ex.Offer.Title}\"",
                      IsReleased = false,
                      CreatedAt = DateTime.UtcNow
                  }
                );

                ex.TokensHeld = true;    // new flag you can add
                ex.TokenHoldDate = DateTime.UtcNow;
                _db.TblExchanges.Update(ex);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                await _notif.AddAsync(new TblNotification
                {
                    UserId = buyer.UserId,
                    Title = "Tokens held in escrow",
                    Message = $"{cost} tokens have been placed in escrow for exchange #{exchangeId} – \"{ex.Offer.Title}\".",
                    Url = $"/UserDashboard/Exchanges/{exchangeId}"
                });

                // 2) In-app notification to seller
                await _notif.AddAsync(new TblNotification
                {
                    UserId = seller.UserId,
                    Title = "Tokens are pending",
                    Message = $"{cost} tokens have been held in escrow pending completion of exchange #{exchangeId} – \"{ex.Offer.Title}\".",
                    Url = $"/UserDashboard/Exchanges/{exchangeId}"
                });

                var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header -->
      <tr>
        <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
          swapo
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <p style=""margin:0 0 15px;"">Hi <strong>{buyer.UserName}</strong>,</p>
          <p style=""margin:0 0 15px;"">
            We’ve placed <strong>{cost}</strong> tokens in escrow for exchange 
            <strong>#{exchangeId} – {ex.Offer.Title}</strong>.
          </p>
          <p style=""margin:0 0 15px;"">
            You’ll see them returned if the exchange does not complete.
          </p>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Thanks for trusting swapo — stay swapping!  
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

                await _email.SendEmailAsync(
                    buyer.Email,
                    subject: "Your tokens have been held in escrow",
                    body: htmlBody,
                    isBodyHtml: true
                );

                // Email to seller
                var htmlBody2 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header -->
      <tr>
        <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
          swapo
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <p style=""margin:0 0 15px;"">Hi <strong>{seller.UserName}</strong>,</p>
          <p style=""margin:0 0 15px;"">
            <strong>{cost}</strong> tokens have been held in escrow awaiting completion of exchange 
            <strong>#{exchangeId} – {ex.Offer.Title}</strong>.
          </p>
          <p style=""margin:0 0 15px;"">
            We’ll let you know when they’re released.
          </p>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Thanks for being part of swapo!  
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

                await _email.SendEmailAsync(
                    seller.Email,
                    subject: "Tokens held for your pending exchange",
                    body: htmlBody2,
                    isBodyHtml: true
                );
            }
            catch (Exception exn)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(exn, "Error holding tokens for exchange {ExchangeId}", exchangeId);
                throw;
            }
        }

        public async Task ReleaseTokensAsync(int exchangeId, CancellationToken ct = default)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var ex = await _db.TblExchanges
                    .Include(e => e.Offer).ThenInclude(o => o.User)
                    .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                if (ex == null) throw new KeyNotFoundException($"Exchange #{exchangeId} not found.");

                if (!ex.TokensHeld)
                    throw new InvalidOperationException("Tokens have not been held.");

                var seller = ex.Offer.User
                              ?? throw new KeyNotFoundException($"Offer owner not found.");

                var holdTx = await _db.TblTokenTransactions
                    .FirstOrDefaultAsync(t => t.ExchangeId == exchangeId && t.TxType == "Hold" && !t.IsReleased);
                if (holdTx == null) throw new InvalidOperationException("No outstanding hold.");

                decimal amount = holdTx.Amount;
                var escrow = await GetUserAsync(EscrowUserId, "Escrow", ct);

                // 1) release original tokens
                await ReleaseOriginalAsync(escrow, seller, holdTx, amount, exchangeId, ct);

                // 2) calculate & credit boost
                await ApplyBoostAsync(seller.UserId, amount, exchangeId, ct);

                // 3) finalize exchange record
                ex.TokensHeld = false;
                ex.TokensSettled = true;
                ex.TokenReleaseDate = DateTime.UtcNow;
                ex.Status = "Completed";
                _db.TblExchanges.Update(ex);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var subject = $"Funds released for exchange #{exchangeId}";
                var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI, sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr>
      <td align=""center"" style=""padding:20px;"">
        <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">
          
          <!-- Header -->
          <tr>
            <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
              swapo
            </td>
          </tr>
    
          <!-- Body -->
          <tr>
            <td style=""padding:20px;color:#333333;line-height:1.5;"">
              <p style=""margin:0 0 15px;"">Hi <strong>{seller.UserName}</strong>,</p>
              <p style=""margin:0 0 15px;"">
                The <strong>{amount}</strong> tokens held in escrow for exchange 
                <strong>#{exchangeId}</strong> have just been released to your account.
              </p>
              <p style=""margin:0;"">Thanks for using swapo!</p>
            </td>
          </tr>
    
          <!-- Footer -->
          <tr>
            <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
              Happy swapping — The swapo Team
            </td>
          </tr>
    
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
";

                await _email.SendEmailAsync(
                    seller.Email,
                    subject: subject,
                    body: htmlBody,
                    isBodyHtml: true
                );

                var buyer = await _db.TblUsers.FindAsync(ex.OtherUserId)
                  ?? throw new KeyNotFoundException($"Buyer not found.");

                // Email to buyer
                var htmlBody3 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header -->
      <tr>
        <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
          swapo
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <p style=""margin:0 0 15px;"">Hi <strong>{buyer.UserName}</strong>,</p>
          <p style=""margin:0 0 15px;"">
            Your escrow of <strong>{amount}</strong> tokens for exchange 
            <strong>#{exchangeId}</strong> has now been released.
          </p>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Happy swapping!  
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

                await _email.SendEmailAsync(
                    buyer.Email,
                    subject: "Your escrow has been released",
                    body: htmlBody3,
                    isBodyHtml: true
                );
            }
            catch (Exception exn)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(exn, "Error releasing tokens for exchange {ExchangeId}", exchangeId);
                throw;
            }
        }

        private async Task ReleaseOriginalAsync(
            TblUser escrow,
            TblUser seller,
            TblTokenTransaction holdTx,
            decimal amount,
            int exchangeId,
        CancellationToken ct)
        {
            if (escrow.DigitalTokenBalance < amount)
                throw new InvalidOperationException(
                    $"Escrow balance {escrow.DigitalTokenBalance} insufficient to release {amount}.");

            escrow.DigitalTokenBalance -= amount;
            seller.DigitalTokenBalance += amount;
            _db.TblUsers.UpdateRange(escrow, seller);

            // mark hold as released
            holdTx.IsReleased = true;
            _db.TblTokenTransactions.Update(holdTx);

            // record release transaction
            _db.TblTokenTransactions.Add(new TblTokenTransaction
            {
                ExchangeId = exchangeId,
                FromUserId = escrow.UserId,
                ToUserId = seller.UserId,
                Amount = amount,
                TxType = "Release",
                Description = $"Release for exchange #{exchangeId}",
                CreatedAt = DateTime.UtcNow
            });

            await _notif.AddAsync(new TblNotification
            {
                UserId = seller.UserId,
                Title = "Tokens Released",
                Message = $"{amount} tokens released to you for exchange #{exchangeId}.",
                Url = $"/ExchangeDashboard/Details/{exchangeId}"
            });
        }

        private async Task ApplyBoostAsync(
            int sellerUserId,
            decimal originalAmount,
            int exchangeId,
            CancellationToken ct)
        {
            var multiplier = await GetBoostMultiplierAsync(sellerUserId, ct);
            var bonusTotal = Math.Floor(originalAmount * (multiplier - 1));
            if (bonusTotal <= 0) return;

            var reserve = await GetUserAsync(SystemReserveUserId, "System reserve", ct);
            if (reserve.DigitalTokenBalance < bonusTotal)
                throw new InvalidOperationException(
                    $"System reserve {reserve.DigitalTokenBalance} insufficient to pay bonus {bonusTotal}.");

            var seller = await _db.TblUsers.FindAsync(new object[] { sellerUserId }, ct)
                         ?? throw new KeyNotFoundException("Seller not found.");

            reserve.DigitalTokenBalance -= bonusTotal;
            seller.DigitalTokenBalance += bonusTotal;
            _db.TblUsers.UpdateRange(reserve, seller);

            _db.TblTokenTransactions.Add(new TblTokenTransaction
            {
                ExchangeId = exchangeId,
                FromUserId = reserve.UserId,
                ToUserId = seller.UserId,
                Amount = bonusTotal,
                TxType = "BoostReward",
                Description = $"Membership boost for exchange #{exchangeId}",
                CreatedAt = DateTime.UtcNow
            });

            await _notif.AddAsync(new TblNotification
            {
                UserId = seller.UserId,
                Title = "Bonus Tokens Credited",
                Message = $"{bonusTotal} bonus tokens credited (membership boost).",
                Url = $"/ExchangeDashboard/Details/{exchangeId}"
            });
        }

        private async Task<decimal> GetBoostMultiplierAsync(int userId, CancellationToken ct)
        {
            var sub = await _db.Subscriptions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, ct);

            return sub?.PlanName switch
            {
                "Plus" => 1.25m,   // no extra
                "Pro" => 1.50m,   // 25% boost
                "Growth" => 2.00m,   // 50% boost
                _ => 1.00m
            };
        }

        private async Task<TblUser> GetUserAsync(int userId, string roleDescription, CancellationToken ct)
        {
            var u = await _db.TblUsers.FindAsync(new object[] { userId }, ct);
            if (u == null)
                throw new KeyNotFoundException($"{roleDescription} account (UserId={userId}) is missing.");
            return u;
        }

        public async Task RefundTokensAsync(int exchangeId, CancellationToken ct = default)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // 1) Find the original “hold” transaction
                var holdTx = await _db.TblTokenTransactions
                    .FirstOrDefaultAsync(t =>
                        t.ExchangeId == exchangeId &&
                        t.TxType == "Hold" &&
                        !t.IsReleased);

                if (holdTx == null)
                    throw new InvalidOperationException("No outstanding hold transaction found.");

                var cost = holdTx.Amount;
                var buyer = await _db.TblUsers.FindAsync(holdTx.FromUserId)
                             ?? throw new KeyNotFoundException("Buyer not found.");
                var escrow = await GetEscrowUserAsync();

                if (escrow.DigitalTokenBalance < cost)
                    throw new InvalidOperationException(
                        $"Escrow balance {escrow.DigitalTokenBalance} is insufficient to release {cost} tokens.");

                // 2) Reverse ledger: debit escrow, credit buyer
                escrow.DigitalTokenBalance -= cost;
                buyer.DigitalTokenBalance += cost;

                _db.TblUsers.Update(escrow);
                _db.TblUsers.Update(buyer);

                // 3) Mark the hold transaction “released” so it won’t be re‐used
                holdTx.IsReleased = true;
                _db.TblTokenTransactions.Update(holdTx);

                // 4) append a “Refund” transaction row
                _db.TblTokenTransactions.Add(new TblTokenTransaction
                {
                    ExchangeId = exchangeId,
                    FromUserId = escrow.UserId,
                    ToUserId = buyer.UserId,
                    Amount = cost,
                    TxType = "Refund",
                    Description = $"Refund for cancelled exchange #{exchangeId}",
                    CreatedAt = DateTime.UtcNow
                });

                // 5) Update the exchange record
                var exchange = await _db.TblExchanges.FindAsync(exchangeId)
                               ?? throw new KeyNotFoundException("Exchange not found.");
                exchange.Status = "Cancelled";
                exchange.IsCompleted = false;
                exchange.TokensHeld = false;
                exchange.TokensSettled = false;
                _db.TblExchanges.Update(exchange);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                await _notif.AddAsync(new TblNotification
                {
                    UserId = buyer.UserId,
                    Title = "Tokens Refunded",
                    Message = $"{cost} tokens have been returned to your account for cancelled exchange #{exchangeId}.",
                    Url = $"/UserDashboard/Exchanges/{exchangeId}"
                });
                var htmlBody4 = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header -->
      <tr>
        <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
          swapo
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <p style=""margin:0 0 15px;"">Hi <strong>{buyer.UserName}</strong>,</p>
          <p style=""margin:0 0 15px;"">
            Your <strong>{cost}</strong> tokens have been returned after cancelling exchange 
            <strong>#{exchangeId}</strong>.
          </p>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Thank you for swapping with us!  
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

                await _email.SendEmailAsync(
                    buyer.Email,
                    subject: "Your tokens have been refunded",
                    body: htmlBody4,
                    isBodyHtml: true
                );
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error refunding tokens for exchange {ExchangeId}", exchangeId);
                throw;
            }
        }
        public async Task<decimal> GetBalanceAsync(int userId)
        {
            var user = await _db.TblUsers
                        .AsNoTracking()
                        .SingleOrDefaultAsync(u => u.UserId == userId);
            return user?.DigitalTokenBalance ?? 0m;
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await GetBalanceAsync(userId) >= amount;

        public async Task RecordTransactionAsync(int? exchangeId, int fromUserId, int toUserId, decimal amount, string txType, string description)
        {
            var tx = new TblTokenTransaction
            {
                ExchangeId = exchangeId,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                Amount = amount,
                TxType = txType,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _db.TblTokenTransactions.Add(tx);
            await _db.SaveChangesAsync();
        }

        private async Task<TblUser> GetEscrowUserAsync()
        {
            var escrow = await _db.TblUsers
                         .IgnoreQueryFilters()
                         .SingleOrDefaultAsync(u =>
                             u.UserId == EscrowUserId &&
                             u.IsEscrowAccount);

            if (escrow == null)
                throw new InvalidOperationException(
                    $"Escrow account (UserId={EscrowUserId}) is missing!");
            return escrow;
        }
    }
}