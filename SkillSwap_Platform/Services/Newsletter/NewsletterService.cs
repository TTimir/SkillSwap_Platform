using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using System;

namespace SkillSwap_Platform.Services.Newsletter
{
    public class NewsletterService : INewsletterService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _email;
        private readonly ILogger<NewsletterService> _logger;
        private readonly IHttpContextAccessor _ctxAccessor;
        private readonly IWebHostEnvironment _env;    
        private readonly FileExtensionContentTypeProvider _mimeProvider;

        public NewsletterService(
            SkillSwapDbContext db,
            IEmailService email,
            ILogger<NewsletterService> logger,
            IHttpContextAccessor ctxAccessor,
            IWebHostEnvironment env)
        {
            _db = db;
            _email = email;
            _logger = logger;
            _ctxAccessor = ctxAccessor;
            _env = env;
            _mimeProvider = new FileExtensionContentTypeProvider();
        }

        public async Task SubscribeAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email must be provided", nameof(email));

            // Prevent duplicates
            bool exists = await _db.TblNewsletterSubscribers
                .AsNoTracking()
                .AnyAsync(n => n.Email == email, ct);

            if (exists) return;

            _db.TblNewsletterSubscribers.Add(new TblNewsletterSubscriber
            {
                Email = email,
                SubscribedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("New subscriber added: {Email}", email);
        }

        public async Task SendNewsletterAsync(string subject, string htmlContent, IEnumerable<IFormFile>? attachments = null, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var adminName = _ctxAccessor.HttpContext?.User?.Identity?.Name ?? "system";

            var savedPaths = await SaveAttachmentsAsync(attachments, ct);

            // 1) build a combined, de-duplicated recipient list:
            var recipients = await _db.TblNewsletterSubscribers
            .AsNoTracking().Select(n => n.Email)
            .Union(_db.TblUsers.AsNoTracking().Select(u => u.Email))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToListAsync(ct);

            // 2) remember the attachment names
            var emailAttachments = savedPaths
            .Select(relPath =>
            {
                var fullPath = Path.Combine(_env.WebRootPath, relPath);
                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                _mimeProvider.TryGetContentType(fullPath, out var mime);
                return new EmailAttachment(
                    stream,
                    Path.GetFileName(fullPath),
                    mime ?? "application/octet-stream"
                );
            })
            .ToList();

            // 3) send & log
            foreach (var to in recipients)
            {
                try
                {
                    await _email.SendEmailAsync(to, subject, htmlContent, isBodyHtml: true, attachments: emailAttachments);
                    _logger.LogInformation("Newsletter “{Subj}” sent to {Email}", subject, to);

                    _db.NewsletterLogs.Add(new NewsletterLog
                    {
                        Subject = subject,
                        Content = htmlContent,
                        RecipientEmail = to,
                        AttachmentNames = string.Join(";", savedPaths),
                        SentAtUtc = now,
                        SentByAdmin = adminName
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Newsletter to {Email} failed, continuing…", to);
                }
            }
            await _db.SaveChangesAsync(ct);
            // dispose all streams
            foreach (var att in emailAttachments)
                att.Content.Dispose();

        }

        public async Task SendToUserAsync(int userId, string subject, string htmlContent, IEnumerable<IFormFile>? attachments = null, CancellationToken ct = default)
        {
            // look up the email
            var user = await _db.TblUsers
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Email, u.UserName })
                .FirstOrDefaultAsync(ct);

            if (user == null || string.IsNullOrEmpty(user.Email))
                throw new InvalidOperationException($"User {userId} not found or has no email.");

            var now = DateTime.UtcNow;
            var adminName = _ctxAccessor.HttpContext?.User?.Identity?.Name ?? "system";
            // retain the file names

            var savedPaths = await SaveAttachmentsAsync(attachments, ct);

            // prepare attachments
            var emailAttachments = savedPaths
                .Select(relPath =>
                {
                    var fullPath = Path.Combine(_env.WebRootPath, relPath);
                    var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                    _mimeProvider.TryGetContentType(fullPath, out var mime);
                    return new EmailAttachment(
                        stream,
                        Path.GetFileName(fullPath),
                        mime ?? "application/octet-stream"
                    );
                })
                .ToList();

            try
            {
                await _email.SendEmailAsync(user.Email, subject, htmlContent, isBodyHtml: true, attachments: emailAttachments);

                // log it
                _db.NewsletterLogs.Add(new NewsletterLog
                {
                    Subject = subject,
                    Content = htmlContent,
                    RecipientEmail = user.Email,
                    AttachmentNames = string.Join(";", savedPaths),
                    SentAtUtc = now,
                    SentByAdmin = adminName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send individual message to user {UserId}", userId);
                throw;
            }
            finally
            {
                foreach (var att in emailAttachments)
                    att.Content.Dispose();
            }

            await _db.SaveChangesAsync(ct);
        }

        private async Task<List<string>> SaveAttachmentsAsync(
                IEnumerable<IFormFile>? files,
                CancellationToken ct)
        {
            var savedPaths = new List<string>();
            if (files == null) return savedPaths;

            var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "newsletters");
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                // create a unique file name
                var ext = Path.GetExtension(file.FileName);
                var uniqueName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadRoot, uniqueName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream, ct);

                // store the relative path (to use later for serving or reading)
                savedPaths.Add(Path.Combine("uploads", "newsletters", uniqueName));
            }

            return savedPaths;
        }

    }
}