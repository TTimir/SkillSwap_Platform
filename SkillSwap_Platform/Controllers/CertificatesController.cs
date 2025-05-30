using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IWebHostEnvironment _env;
        private const decimal CertificateCost = 1m;
        private const int SystemReserveUserId = 3;

        public CertificatesController(
        SkillSwapDbContext db,
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IWebHostEnvironment env           
    )
        {
            _db = db;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _env = env;           
        }

        // GET: /Certificates/SessionCompletePdf?exchangeId=123
        [HttpGet]
        public async Task<IActionResult> SessionCompletePdf(int exchangeId)
        {
            // 1) must be logged in
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized();

            var user = await _db.TblUsers
                        .Where(u => u.UserId == userId)
                        .Select(u => new { u.FirstName, u.LastName })
                        .FirstOrDefaultAsync();
            if (user == null) return Unauthorized();

            // 2) verify purchase
            var bought = await _db.TblCertificatePurchases
                                 .AnyAsync(c => c.ExchangeId == exchangeId && c.UserId == userId);
            if (!bought)
                return Forbid();

            // 3) load exchange + offer to fill your VM
            var ex = await _db.TblExchanges
                              .Include(e => e.Offer)
                              .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
            if (ex == null) return NotFound();

            string origin = $"{Request.Scheme}://{Request.Host}";
            var vm = new CertificateVM
            {
                RecipientName = $"{user.FirstName} {user.LastName}",
                SessionTitle = ex.Offer!.Title,
                CompletedAt = ex.CompletionDate ?? DateTime.UtcNow,
            };

            var logoPath = Path.Combine(_env.WebRootPath, "template_assets", "images", "header-logo-dark.png");
            var logoBytes = System.IO.File.ReadAllBytes(logoPath);
            vm.LogoUrl = $"data:image/png;base64,{Convert.ToBase64String(logoBytes)}";

            var sigPath = Path.Combine(_env.WebRootPath, "template_assets", "images", "signature.png");
            var sigBytes = System.IO.File.ReadAllBytes(sigPath);
            vm.SignatureUrl = $"data:image/png;base64,{Convert.ToBase64String(sigBytes)}";

            string html = await RenderViewToStringAsync("SessionCompletePdf", vm);


            byte[] pdfBytes = await GeneratePdfFromHtmlAsync(html);

            return File(pdfBytes,
                        contentType: "application/pdf",
                        fileDownloadName: $"certificate-{exchangeId}.pdf");
        }

        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            var actionContext = new ActionContext(
                HttpContext, RouteData, ControllerContext.ActionDescriptor, ModelState);

            var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);
            if (!viewResult.Success)
                throw new InvalidOperationException($"View '{viewName}' not found.");

            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), ModelState)
            {
                Model = model
            };
            var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);

            await using var sw = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewData,
                tempData,
                sw,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }

        // Uses PuppeteerSharp to generate PDF bytes from an HTML string
        private async Task<byte[]> GeneratePdfFromHtmlAsync(string html)
        {
            // Download Chromium if needed
            await new BrowserFetcher().DownloadAsync();

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            await using var page = await browser.NewPageAsync();
            // Set the HTML and wait until network idle (to load images/CSS)
            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
            });

            // Generate PDF
            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.Letter,
                PrintBackground = true
            });
        }

        public class PurchaseCertRequest
        {
            public int ExchangeId { get; set; }
        }

        // POST: /Certificates/PurchaseCertificate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurchaseCertificate([FromBody] PurchaseCertRequest req)
        {
            if (req == null || req.ExchangeId <= 0)
                return Json(new { success = false, message = "Invalid request." });

            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Json(new { success = false, message = "Not logged in." });

            // already?
            var already = await _db.TblCertificatePurchases
                                   .AnyAsync(c => c.ExchangeId == req.ExchangeId && c.UserId == userId);
            if (!already)
            {
                // debit buyer
                var buyer = await _db.TblUsers.FindAsync(userId)
                            ?? throw new Exception("User missing");
                if (buyer.DigitalTokenBalance < CertificateCost)
                    return Json(new { success = false, message = "Insufficient tokens." });
                buyer.DigitalTokenBalance -= CertificateCost;

                // credit system reserve
                var reserve = await _db.TblUsers
                                       .IgnoreQueryFilters()
                                       .SingleOrDefaultAsync(u => u.UserId == SystemReserveUserId && u.IsSystemReserveAccount)
                            ?? throw new Exception("Reserve account missing");
                reserve.DigitalTokenBalance += CertificateCost;

                // ledger row
                _db.TblTokenTransactions.Add(new TblTokenTransaction
                {
                    ExchangeId = req.ExchangeId,
                    FromUserId = userId,
                    ToUserId = reserve.UserId,
                    Amount = CertificateCost,
                    TxType = "CertificatePurchase",
                    Description = $"Cert for exchange #{req.ExchangeId}",
                    CreatedAt = DateTime.UtcNow
                });

                // mark purchase
                _db.TblCertificatePurchases.Add(new TblCertificatePurchase
                {
                    ExchangeId = req.ExchangeId,
                    UserId = userId,
                    PurchasedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
            }

            // return URL for real PDF
            var downloadUrl = Url.Action(nameof(SessionCompletePdf), new { exchangeId = req.ExchangeId });
            return Json(new { success = true, already, downloadUrl });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PreviewCertificate()
        {
            // hard-coded demo data
            var vm = new CertificateVM
            {
                RecipientName = "Jane Doe",
                SessionTitle = "Mastering C# in 24 Hours",
                CompletedAt = new DateTime(2025, 1, 15),
                LogoUrl = Url.Content("~/template_assets/images/header-logo-dark.png"),
                SignatureUrl = Url.Content("~/template_assets/images/signature.png")
            };

            // render the same CertificateView Razor page into PDF
            return View("SessionCompletePdf", vm);
        }
    }
}