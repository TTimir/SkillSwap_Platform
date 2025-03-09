using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblSupportTicket
{
    public int TicketId { get; set; }

    public int UserId { get; set; }

    public int? ExchangeId { get; set; }

    public string Subject { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? ClosedDate { get; set; }

    public virtual TblExchange? Exchange { get; set; }

    public virtual TblUser User { get; set; } = null!;
}
