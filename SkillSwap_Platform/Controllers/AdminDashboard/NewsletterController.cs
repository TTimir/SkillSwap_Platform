using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Utilities;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Services.Newsletter;
using System.Net.Mail;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin, Moderator")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class NewsletterController : Controller
    {
        private readonly INewsletterService _newsletter;
        private readonly INewsletterTemplateService _newsletterTemp;
        private readonly SkillSwapDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<NewsletterController> _logger;

        public NewsletterController(
            INewsletterService newsletter,
            SkillSwapDbContext db,
            IWebHostEnvironment env,
            ILogger<NewsletterController> logger,
            INewsletterTemplateService newsletterTemp)
        {
            _newsletter = newsletter;
            _db = db;
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger;
            _newsletterTemp = newsletterTemp;
        }

        // GET: /newsletter/send
        [HttpGet]
        public async Task<IActionResult> SendNewsletter()
        {
            await PopulateTemplatesAsync();
            return View(new SendNewsletterVm());
        }

        // POST: /newsletter/send
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNewsletter(SendNewsletterVm vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateTemplatesAsync();
                return View(vm);
            }

            try
            {
                await _newsletter.SendNewsletterAsync(vm.Subject, vm.Content, vm.Attachments);
                TempData["Success"] = "Newsletter dispatch started.";
                return RedirectToAction(nameof(SendNewsletter));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send newsletter “{Subject}”", vm.Subject);
                ModelState.AddModelError("", "Unable to send newsletter right now.");
                var tpls = await _newsletterTemp.GetAllAsync();
                ViewBag.Templates = new SelectList(tpls, nameof(Models.NewsletterTemplate.TemplateId), nameof(Models.NewsletterTemplate.Name));
                ViewBag.TemplateContent = tpls.ToDictionary(t => t.TemplateId.ToString(), t => t.HtmlContent);
                return View(vm);
            }
        }

        // GET /Admin/Newsletter/history
        [HttpGet("history")]
        public async Task<IActionResult> History(int page = 1, int pageSize = 10)
        {
            var query = _db.NewsletterLogs
                   .AsNoTracking()
                   .Where(l => l.IsBroadcast)            // ← filter
                   .OrderByDescending(l => l.SentAtUtc);

            // 1) count all logs
            var total = await query.CountAsync();

            // 2) fetch only this page
            var rawLogs = await query
                   .Skip((page - 1) * pageSize)
                   .Take(pageSize)
                   .ToListAsync();

            // 3) build our VM
            var vm = new NewsletterHistoryVm
            {
                Logs = rawLogs.Select(l => new NewsletterLogVm
                {
                    NewsletterLogId = l.NewsletterLogId,
                    Subject = l.Subject,
                    SentAtUtc = l.SentAtUtc,
                    SentByAdmin = l.SentByAdmin,
                    RecipientEmail = l.RecipientEmail,
                    Attachments = l.AttachmentNames?
                                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .ToList()
                                ?? new List<string>()
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };

            return View(vm);
        }

        // GET /Admin/Newsletter/message
        [HttpGet("message")]
        public async Task<IActionResult> SendToUser(int? userId)
        {
            await PopulateTemplatesAsync();

            var vm = new SendToUserVm();
            if (userId.HasValue)
            {
                var user = await _db.TblUsers
                    .AsNoTracking()
                    .Where(u => u.UserId == userId.Value)
                    .Select(u => new { u.UserId, u.UserName })
                    .SingleOrDefaultAsync();
                
                if (user != null)
                {
                    vm.UserId = user.UserId;
                    vm.UserName = user.UserName;
                }
            }
            ViewBag.Users = await _db.TblUsers
                .AsNoTracking()
                .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
                .ToListAsync();

            return View(vm);
        }

        // POST /Admin/Newsletter/message
        [HttpPost("message"), ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToUser(SendToUserVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Users = await _db.TblUsers
                    .AsNoTracking()
                    .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
                    .ToListAsync();
                await PopulateTemplatesAsync();
                return View(vm);
            }

            try
            {
                // if they picked a template, override the Content
                if (vm.SelectedTemplateId.HasValue)
                {
                    var tpl = await _newsletterTemp
                        .GetAllAsync()
                        .ContinueWith(t => t.Result
                            .FirstOrDefault(x => x.TemplateId == vm.SelectedTemplateId.Value));
                    if (tpl != null) vm.Content = tpl.HtmlContent;
                }

                await _newsletter.SendToUserAsync(vm.UserId, vm.Subject, vm.Content, vm.Attachments);
                TempData["Success"] = $"Message sent to {vm.UserName}.";
                return RedirectToAction(
                        nameof(SendToUser),
                        new { userId = vm.UserId }
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to user {UserId}", vm.UserId);
                ModelState.AddModelError("", "Unable to send message right now.");
                ViewBag.Users = await _db.TblUsers
                    .AsNoTracking()
                    .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
                    .ToListAsync();
                var tpls = await _newsletterTemp.GetAllAsync();
                ViewBag.Templates = new SelectList(tpls, nameof(Models.NewsletterTemplate.TemplateId), nameof(Models.NewsletterTemplate.Name));
                ViewBag.TemplateContent = tpls.ToDictionary(t => t.TemplateId.ToString(), t => t.HtmlContent);

                return View(vm);
            }
        }

        // GET /Admin/Newsletter/UserHistory/{userId}
        [HttpGet("user-history")]
        public async Task<IActionResult> UserHistory(int? userId, int page = 1, int pageSize = 10)
        {
            // 1) build users dropdown
            ViewBag.Users = await _db.TblUsers
                .AsNoTracking()
                .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
                .ToListAsync();

            // 2) base VM
            var vm = new NewsletterHistoryVm
            {
                Page = page,
                PageSize = pageSize
            };

            // 3) no user selected -> empty list
            if (!userId.HasValue) return View(new NewsletterHistoryVm { Page = page, PageSize = pageSize });

            // 4) lookup user by ID
            var user = await _db.TblUsers
                .AsNoTracking()
                .Where(u => u.UserId == userId.Value)
                .Select(u => new { u.UserName, u.Email })
                .SingleOrDefaultAsync();

            if (user == null)
                return NotFound();

            vm.FilterLabel = user.UserName;

            // 5) build query for this user’s logs
            var query = _db.NewsletterLogs
                   .AsNoTracking()
                   .Where(l => !l.IsBroadcast             // ← only the 1:1 messages
                            && l.RecipientEmail == user.Email)
                   .OrderByDescending(l => l.SentAtUtc);

            // 6) count & fetch page
            vm.TotalCount = await query.CountAsync();
            var raw = await query.Skip((page - 1) * pageSize)
                   .Take(pageSize)
                   .ToListAsync();

            // 7) project into VM
            vm.Logs = raw
                .Select(l => new NewsletterLogVm
                {
                    NewsletterLogId = l.NewsletterLogId,
                    Subject = l.Subject,
                    SentAtUtc = l.SentAtUtc,
                    SentByAdmin = l.SentByAdmin,
                    RecipientEmail = l.RecipientEmail,
                    Attachments = l.AttachmentNames?
                                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .ToList()
                                ?? new List<string>()
                })
                .ToList();

            return View(vm);
        }


        [HttpPost("save-template")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTemplate([FromBody] SaveTemplateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TemplateName) || string.IsNullOrWhiteSpace(req.HtmlContent))
                return BadRequest(new { success = false, error = "Name and Content required" });

            var admin = User.Identity?.Name ?? "system";
            try
            {
                var created = await _newsletterTemp.CreateAsync(req.TemplateName, req.HtmlContent, admin);
                return Json(new { success = true, id = created.TemplateId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveTemplate failed");
                return StatusCode(500, new { success = false, error = "Could not save template" });
            }
        }

        // GET /Admin/Newsletter/ViewAttachment?file=uploads/newsletters/foo.png
        [HttpGet("~/Admin/Newsletter/view-attachment", Name = "ViewAttachmentRoute")]
        public async Task<IActionResult> ViewAttachment(int logId, string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                return BadRequest();

            // build full path under wwwroot
            var relative = file.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_env.WebRootPath, relative);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            // Determine MIME type
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
                contentType = "application/octet-stream";

            var memory = new MemoryStream();
            await using (var stream = System.IO.File.OpenRead(filePath))
                await stream.CopyToAsync(memory);
            memory.Position = 0;

            return File(memory, contentType, Path.GetFileName(relative), enableRangeProcessing: true);
        }

        #region Helper Class
        private async Task PopulateTemplatesAsync()
        {
            var tpls = await _newsletterTemp.GetAllAsync();
            ViewBag.Templates = new SelectList(tpls,
                nameof(Models.NewsletterTemplate.TemplateId),
                nameof(Models.NewsletterTemplate.Name));
            // for the live-preview script
            ViewBag.TemplateContent = tpls.ToDictionary(
                t => t.TemplateId.ToString(),
                t => t.HtmlContent
            );
        }

        public class SaveTemplateRequest
        {
            public string TemplateName { get; set; }
            public string HtmlContent { get; set; }
        }
        #endregion
    }
}