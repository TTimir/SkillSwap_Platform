namespace SkillSwap_Platform.Models.ViewModels
{
    public class NewsletterTemplate
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = default!;
        public string HtmlContent { get; set; } = default!;
        public string CreatedBy { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
    }
}
