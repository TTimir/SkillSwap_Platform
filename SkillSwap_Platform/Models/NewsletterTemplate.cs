using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class NewsletterTemplate
{
    public int TemplateId { get; set; }

    public string Name { get; set; } = null!;

    public string HtmlContent { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
