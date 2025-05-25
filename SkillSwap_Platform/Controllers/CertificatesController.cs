using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services.DigitalToken;
using System.Numerics;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly SkillSwapDbContext _context;

        public CertificatesController(
            SkillSwapDbContext db)
        {
            _context = db;
        }

        [HttpGet]
        public async Task<IActionResult> SessionCompletePdf(int exchangeId)
        {
            // 1️⃣ Load the exchange (our “session”)
            var exchange = await _context.TblExchanges
                .Include(e => e.Offer)    // if you want the offer title
                .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);

            if (exchange == null)
                return NotFound();

            // 2️⃣ Figure out who the “recipient” is
            //    If the current user kicked off this request, they’re one party;
            //    we’ll award to the opposite.
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            int recipientUserId = (exchange.OfferOwnerId == currentUserId)
                                  ? exchange.OtherUserId!.Value
                                  : exchange.OfferOwnerId!.Value;

            // 3️⃣ Load that user’s name
            var recipient = await _context.TblUsers
                .FirstOrDefaultAsync(u => u.UserId == recipientUserId);
            if (recipient == null)
                return NotFound();

            // 4️⃣ Build your view-model
            var vm = new CertificateVM
            {
                RecipientName = recipient.UserName,    // or FirstName + LastName
                SessionTitle = exchange.Offer?.Title ?? "Skill Swap Session",
                CompletedAt = exchange.CompletionDate ?? exchange.ExchangeDate
            };

            // 5️⃣ Render as PDF via Rotativa
            return new ViewAsPdf("SessionComplete", vm)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                CustomSwitches = "--disable-smart-shrinking"
            };
        }

        public class PurchaseCertRequest { public int ExchangeId { get; set; } }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PurchaseCertificate([FromBody] PurchaseCertRequest req)
        {
            var userId = GetUserId();
            var exchangeId = req.ExchangeId;
            decimal fee = 1.2m;

            // 1) Check balance
            var user = await _context.TblUsers.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });
            if (user.DigitalTokenBalance < fee)
                return Json(new { success = false, message = "Insufficient tokens." });

            // 2) Deduct
            user.DigitalTokenBalance -= fee;

            // 3) Credit to system reserve (userId = 3)
            const int systemReserveUserId = 3;
            var reserve = await _context.TblUsers.FindAsync(systemReserveUserId);
            if (reserve != null)
            {
                reserve.DigitalTokenBalance += fee;
            }

            // 4) Record the transaction
            _context.TblTokenTransactions.Add(new TblTokenTransaction
            {
                FromUserId = userId,
                ToUserId = systemReserveUserId,
                Amount = fee,
                TxType = "Purchase-Certificate",
                CreatedAt = DateTime.UtcNow,
                Description = $"Unlock certificate for exchange #{exchangeId}"
            });
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }

    }
}
