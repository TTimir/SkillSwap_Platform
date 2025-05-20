namespace SkillSwap_Platform.Models.ViewModels
{
    public class EmailOptions
    {
        public string SmtpHost { get; set; } = null!;
        public string SmtpPort { get; set; } = null!;
        public bool EnableSsl { get; set; }
        public string SmtpUser { get; set; } = null!;
        public string SmtpPass { get; set; } = null!;
        public string FromAddress { get; set; } = null!;
        public string FromName { get; set; } = null!;
    }
}
