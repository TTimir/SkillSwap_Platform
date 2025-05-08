using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class SendNewsletterVm
    {
        [Display(Name = "Template")]
        public int? SelectedTemplateId { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        // new: optional attachments
        public List<IFormFile>? Attachments { get; set; }
    }


    public class NewsletterHistoryVm
    {
        public List<NewsletterLogVm> Logs { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        public string? FilterLabel { get; set; }

        public int TotalPages
            => (int)Math.Ceiling(TotalCount / (double)PageSize);

    }

    public class NewsletterLogVm
    {
        public int NewsletterLogId { get; set; }
        public string Subject { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public string SentByAdmin { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
    }


    // Models/ViewModels/Newsletter/SendToUserVm.cs
    public class SendToUserVm
    {
        [Display(Name = "Template")]
        public int? SelectedTemplateId { get; set; }

        [Required]
        public int UserId { get; set; }

        public string? UserName { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        // new: optional attachments
        public List<IFormFile>? Attachments { get; set; }

    }

}
