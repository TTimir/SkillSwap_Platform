using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblBlogPost
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int AuthorId { get; set; }

    public string? CoverImagePath { get; set; }

    public string? Tags { get; set; }

    public virtual TblUser Author { get; set; } = null!;
}
