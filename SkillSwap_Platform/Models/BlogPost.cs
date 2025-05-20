using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class BlogPost
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int AuthorId { get; set; }
    public List<string> Tags { get; set; } = new();
    public string CoverImagePath { get; set; }

    public virtual TblUser Author { get; set; } = null!;
}
