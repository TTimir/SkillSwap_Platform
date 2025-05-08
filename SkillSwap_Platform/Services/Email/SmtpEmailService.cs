using System.Net.Mail;
using System.Net;

namespace SkillSwap_Platform.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(
            IConfiguration config,
            ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false, IEnumerable<EmailAttachment>? attachments = null)
        {
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "25");
            var smtpUser = _config["Email:SmtpUser"];
            var smtpPass = _config["Email:SmtpPass"];
            var fromAddr = _config["Email:FromAddress"];

            var message = new MailMessage(fromAddr, to, subject, body)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

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

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30_000
            };

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