using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class Question
{
    public int QuestionId { get; set; }

    public int AuthorUserId { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? AcceptedAnswerId { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual TblUser AuthorUser { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
