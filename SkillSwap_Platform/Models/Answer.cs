using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class Answer
{
    public int AnswerId { get; set; }

    public int QuestionId { get; set; }

    public int AuthorUserId { get; set; }

    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TblUser AuthorUser { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}
