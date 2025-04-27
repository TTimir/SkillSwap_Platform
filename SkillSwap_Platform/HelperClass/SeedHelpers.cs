using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.HelperClass
{
    public static class SeedHelpers
    {
        public static async Task EnsureEscrowUserAsync(this SkillSwapDbContext db)
        {
            const int EscrowUserId = 1;

            if (await db.TblUsers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(u => u.UserId == EscrowUserId))
                return;

            // 2) Otherwise, insert it
            var escrow = new TblUser
            {
                UserId = EscrowUserId,           // reserved ID
                UserName = "escrow",
                Email = "escrow@skillswap.local",
                EmailConfirmed = true,
                Designation = "Finance Manager",
                FirstName = "System",
                LastName = "Escrow",
                ContactNo = String.Empty,
                IsHeld = false,
                IsActive = true,
                IsVerified = true,
                IsOnboardingCompleted = true,
                DigitalTokenBalance = 0m,
                IsEscrowAccount = true,
                CreatedDate = DateTime.UtcNow,
                Role = "Escrow",

                // salt = Base64(UTF8("escrowSalt"))
                Salt = "ZXNjcm93U2FsdA==",

                // PBKDF2-HMACSHA256("EscrowDoesNotLogin!234", escrowSalt, 10k, 32 bytes) → Base64
                PasswordHash = "p6EMvlDfoMSTjnowsR481C74fuR1z7dlycNFSYSCW/U="
            };

            // Open the EF-core managed connection & start a transaction
            await db.Database.OpenConnectionAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                // Turn on IDENTITY_INSERT for this table *within* the EF transaction
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.tblUsers ON");

                // Insert your escrow row
                db.TblUsers.Add(escrow);
                await db.SaveChangesAsync();

                // Turn it off again
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.tblUsers OFF");

                // Commit the whole thing
                await transaction.CommitAsync();
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
