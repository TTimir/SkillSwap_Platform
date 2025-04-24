using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using NuGet.Protocol.Plugins;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.NotificationTrack;
using System;
using System.Data;
using System.Security.Cryptography;

namespace SkillSwap_Platform.Services.DigitalToken
{
    public class DigitalTokenService : IDigitalTokenService
    {
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

        public async Task HoldTokensAsync(int exchangeId)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

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

                await _email.SendEmailAsync(
                buyer.Email,
                subject: $"Your tokens have been held in escrow",
                body: $@"
                    <p>Hi {buyer.UserName},</p>
                    <p>We’ve placed <strong>{cost}</strong> tokens in escrow for exchange <strong>#{exchangeId} – {ex.Offer.Title}</strong>.</p>
                    <p>You’ll see them returned if the exchange does not complete.</p>
                    <p>Thanks,<br/>The SkillSwap Team</p>"
            );

                // Email to seller
                await _email.SendEmailAsync(
                    seller.Email,
                    subject: $"Tokens held for your pending exchange",
                    body: $@"
                    <p>Hi {seller.UserName},</p>
                    <p><strong>{cost}</strong> tokens have been held in escrow awaiting completion of exchange <strong>#{exchangeId} – {ex.Offer.Title}</strong>.</p>
                    <p>We’ll let you know when they’re released.</p>
                    <p>Thanks,<br/>The SkillSwap Team</p>"
                );
            }
            catch (Exception exn)
            {
                await tx.RollbackAsync();
                _logger.LogError(exn, "Error holding tokens for exchange {ExchangeId}", exchangeId);
                throw;
            }
        }

        /// <summary>
        /// Called when the exchange actually completes: releases the held tokens from escrow to the seller.
        /// </summary>
        public async Task ReleaseTokensAsync(int exchangeId)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var ex = await _db.TblExchanges
                .Include(e => e.Offer).ThenInclude(o => o.User)
                .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
                if (ex == null) throw new KeyNotFoundException($"Exchange #{exchangeId} not found.");

                if (!ex.TokensHeld)
                    throw new InvalidOperationException("Tokens have not been held.");

                var buyer = await _db.TblUsers.FindAsync(ex.OtherUserId)
                  ?? throw new KeyNotFoundException($"Buyer not found.");
                var seller = ex.Offer.User
                              ?? throw new KeyNotFoundException($"Offer owner not found.");

                var holdTx = await _db.TblTokenTransactions
                    .FirstOrDefaultAsync(t => t.ExchangeId == exchangeId && t.TxType == "Hold" && !t.IsReleased);
                if (holdTx == null) throw new InvalidOperationException("No outstanding hold.");

                decimal cost = holdTx.Amount;

                // debit escrow, credit seller
                var escrow = await GetEscrowUserAsync();
                escrow.DigitalTokenBalance -= cost;
                _db.TblUsers.Update(escrow);

                // credit seller
                seller.DigitalTokenBalance += cost;
                _db.TblUsers.Update(seller);

                // mark hold as released
                holdTx.IsReleased = true;
                _db.TblTokenTransactions.Update(holdTx);

                ex.TokensHeld = false;
                ex.TokensSettled = true;
                ex.TokenReleaseDate = DateTime.UtcNow;
                _db.TblExchanges.Update(ex);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                await _notif.AddAsync(new TblNotification
                {
                    UserId = seller.UserId,
                    Title = "Tokens released from escrow",
                    Message = $"{cost} tokens have been released to your account for exchange #{exchangeId}.",
                    Url = $"/UserDashboard/Exchanges/{exchangeId}"
                });

                // 2) In-app notification to buyer
                await _notif.AddAsync(new TblNotification
                {
                    UserId = buyer.UserId,
                    Title = "Escrow released",
                    Message = $"Escrow of {cost} tokens for exchange #{exchangeId} has been released to the seller.",
                    Url = $"/UserDashboard/Exchanges/{exchangeId}"
                });

                await _email.SendEmailAsync(
                    seller.Email,
                    subject: $"Funds released for exchange #{exchangeId}",
                    body: $@"
                        <p>Hi {seller.UserName},</p>
                        <p>The <strong>{cost}</strong> tokens held in escrow for exchange <strong>#{exchangeId}</strong> have just been released to your account.</p>
                        <p>Thanks for using SkillSwap!</p>"
                );

                // Email to buyer
                await _email.SendEmailAsync(
                    buyer.Email,
                    subject: $"Your escrow has been released",
                    body: $@"
                    <p>Hi {buyer.UserName},</p>
                    <p>Your escrow of <strong>{cost}</strong> tokens for exchange <strong>#{exchangeId}</strong> has now been released..</p>
                    <p>Thanks,<br/>The SkillSwap Team</p>"
                );
            }
            catch (Exception exn)
            {
                await tx.RollbackAsync();
                _logger.LogError(exn, "Error releasing tokens for exchange {ExchangeId}", exchangeId);
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
            // 1) Try to find an existing escrow account
            var escrow = await _db.TblUsers
                                 .SingleOrDefaultAsync(u => u.IsEscrowAccount);
            if (escrow != null)
            {
                return escrow;
            }

            // 2) If none exists, build one *with every required field*
            if (escrow == null)
            {
                // none exists, so create one on the fly:
                escrow = new TblUser
                {
                    // identity fields
                    UserName = "escrow",
                    Email = "escrow@skillswap.local",
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString(),

                    // your custom columns
                    FirstName = "System",
                    LastName = "Escrow",
                    ContactNo = string.Empty,
                    IsHeld = false,
                    IsActive = true,
                    IsVerified = true,
                    Role = "Escrow",   // if you have a flat Role column
                    IsOnboardingCompleted = true,
                    DigitalTokenBalance = 0m,
                    IsEscrowAccount = true,

                    // audit
                    CreatedDate = DateTime.UtcNow
                };

                // 3) Generate a random “password” (never used for login)
                var randomPassword = Guid.NewGuid().ToString("N");

                // 4) Generate a 128-bit salt
                byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
                escrow.Salt = Convert.ToBase64String(saltBytes);

                // 5) Derive a 256-bit subkey (PBKDF2 with HMACSHA256, 10k iterations)
                byte[] hashBytes = KeyDerivation.Pbkdf2(
                    password: randomPassword,
                    salt: saltBytes,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 10_000,
                    numBytesRequested: 32
                );
                escrow.PasswordHash = Convert.ToBase64String(hashBytes);

                // 6) Save and commit
                _db.TblUsers.Add(escrow);
                await _db.SaveChangesAsync();
            }
            return escrow;
        }

    }
}