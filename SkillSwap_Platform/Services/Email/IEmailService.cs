namespace SkillSwap_Platform.Services.Email
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email.
        /// </summary>
        /// <param name="to">Recipient email address</param>
        /// <param name="subject">Message subject</param>
        /// <param name="body">Message body (HTML or plain text)</param>
        Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false, IEnumerable<EmailAttachment>? attachments = null);
    }

    public class EmailAttachment
    {
        public Stream Content { get; }
        public string FileName { get; }
        public string ContentType { get; }

        public EmailAttachment(Stream content, string fileName, string contentType)
        {
            Content = content;
            FileName = fileName;
            ContentType = contentType;
        }
    }
}
