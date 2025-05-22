using Microsoft.EntityFrameworkCore;
using Skill_Swap.Models;
using SkillSwap_Platform.Models;
using System.Text;

namespace SkillSwap_Platform.HelperClass
{
    public static class SeedHelpers
    {
        public static async Task EnsureAdminUserAsync(this SkillSwapDbContext db)
        {
            const int AdminUserId = 1;

            // 1) Make sure the Admin **role** exists
            var adminRole = await db.TblRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleName == "Admin");

            if (adminRole == null)
            {
                adminRole = new TblRole { RoleName = "Admin" };
                db.TblRoles.Add(adminRole);
                await db.SaveChangesAsync();
            }

            // 2) Make sure the Admin **user** exists
            if (!await db.TblUsers.IgnoreQueryFilters().AnyAsync(u => u.UserId == AdminUserId))
            {
                // Admin!Pass2025 ← run once locally to generate real salt+hash per previous snippet:
                var salt = "u9f7h3Gj5KpQX1vZ3Tl2Yg==";
                var hash = "e8JLl8vR3sZrF2rQW7XGkqUhQ2cEK8Wd0Uu3d+2u7Z4=";

                var admin = new TblUser
                {
                    UserId = AdminUserId,
                    UserName = "Admin@timir",
                    Email = "work.timirbhingradiya18@gmail.com",
                    EmailConfirmed = true,
                    FirstName = "Timir",
                    LastName = "Bhingradiya",
                    ContactNo = "6352204102",
                    Designation = "Software Developer",
                    AboutMe = "Developed a website called Skillswap which helps users to swap their known skill with eachother - Founder of Swapo",
                    IsActive = true,
                    IsVerified = true,
                    IsHeld = false,
                    TotalHolds = 0,
                    IsOnboardingCompleted = true,
                    DigitalTokenBalance = 0m,
                    Role = "Admin",
                    Salt = salt,
                    PasswordHash = hash,
                    CreatedDate = DateTime.UtcNow,
                    SecurityStamp = "AA5A4BEE-074E-40AF-BFAE-2D32A9CF1176",
                    IsEscrowAccount = false,
                    IsSupportAgent = false,
                    IsSystemReserveAccount = false,
                    IsFlagged = false
                };

                await db.Database.OpenConnectionAsync();
                await using var tx1 = await db.Database.BeginTransactionAsync();
                try
                {
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.TblUsers ON");
                    db.TblUsers.Add(admin);
                    await db.SaveChangesAsync();
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.TblUsers OFF");
                    await tx1.CommitAsync();
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }
            }

            // 3) Make sure the mapping tblUserRoles exists
            var already = await db.TblUserRoles
                .AsNoTracking()
                .AnyAsync(ur => ur.UserId == AdminUserId && ur.RoleId == adminRole.RoleId);

            if (!already)
            {
                db.TblUserRoles.Add(new TblUserRole
                {
                    UserId = AdminUserId,
                    RoleId = adminRole.RoleId
                });
                await db.SaveChangesAsync();
            }
        }

        public static async Task EnsureEscrowUserAsync(this SkillSwapDbContext db)
        {
            const int EscrowUserId = 2;

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
                Designation = "Escrow Manager",
                FirstName = "System",
                LastName = "Escrow",
                ContactNo = String.Empty,
                IsHeld = false,
                IsActive = true,
                IsVerified = true,
                IsOnboardingCompleted = true,
                DigitalTokenBalance = 0m,
                IsEscrowAccount = true,
                IsSystemReserveAccount = false,
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


        public static async Task EnsureSystemReserveUserAsync(this SkillSwapDbContext db)
        {
            const int SystemReserveUserId = 3;

            // 1) If it already exists, do nothing
            if (await db.TblUsers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(u => u.UserId == SystemReserveUserId))
                return;

            // 2) Otherwise, build the reserve user
            var reserve = new TblUser
            {
                UserId = SystemReserveUserId,
                UserName = "system_reserve",
                Email = "reserve@skillswap.local",
                EmailConfirmed = true,
                Designation = "Finance Manager (Token)",
                FirstName = "System",
                LastName = "Reserve",
                ContactNo = string.Empty,
                IsHeld = false,
                IsActive = true,
                IsVerified = true,
                IsOnboardingCompleted = true,
                DigitalTokenBalance = 50000m,         // seed your initial bonus pool here
                IsEscrowAccount = false,           // differentiate from escrow
                IsSystemReserveAccount = true,
                CreatedDate = DateTime.UtcNow,
                Role = "SystemReserve",

                // pick a salt and hash your “password” (won’t actually be used)
                Salt = "C$eV^Bn5V&Fg8hu+=",
                PasswordHash = "i2q3ws4ed5rf6tg7yh8ujTDYuhbjYRDcvgjthu564weU="
            };

            // 3) Use SET IDENTITY_INSERT to force the reserved ID
            await db.Database.OpenConnectionAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.tblUsers ON");
                db.TblUsers.Add(reserve);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.tblUsers OFF");
                await tx.CommitAsync();
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
