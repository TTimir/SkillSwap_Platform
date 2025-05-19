using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Options;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly EmailOptions _opts;
        public SmtpEmailService(IOptions<EmailOptions> opts) => _opts = opts.Value;


        public SmtpEmailService(
            IOptions<EmailOptions> opts,
            ILogger<SmtpEmailService> logger)
        {
            _opts = opts.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false, IEnumerable<EmailAttachment>? attachments = null)
        {
            if (string.IsNullOrWhiteSpace(_opts.FromAddress) || string.IsNullOrWhiteSpace(_opts.FromName))
                throw new InvalidOperationException(
                        "EmailOptions.FromAddress or FormName is not configured—did you call " +
                        "services.Configure<EmailOptions>(Configuration.GetSection(\"Email\"))?"
                    );

            // 1) build mail message
            var fromAddress = new MailAddress(_opts.FromAddress, _opts.FromName);
            var toAddress = new MailAddress(to);

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            };

            // 2) attach any files
            if (attachments != null)
            {
                foreach(var attach in attachments)
                {
                    try
                    {
                        // rewind in case it's been read already
                        if (attach.Content.CanSeek)
                            attach.Content.Position = 0;

                        var mailAttach = new Attachment(
                            attach.Content,
                            attach.FileName,
                            attach.ContentType);
                        message.Attachments.Add(mailAttach);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to attach {File}", attach.FileName);
                    }
                }
            }

            // 3) configure SMTP client
            using var client = new SmtpClient(_opts.SmtpHost, int.Parse(_opts.SmtpPort))
            {
                Credentials = new NetworkCredential(_opts.SmtpUser, _opts.SmtpPass),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30_000
            };

            // 4) send and log
            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {Recipient}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send to {Recipient} failed", to);
                throw;
            }
        }
    }
}