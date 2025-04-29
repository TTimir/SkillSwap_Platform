using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUserWishlist
{
    public int WishlistId { get; set; }

    public int UserId { get; set; }

    public int OfferId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual TblOffer Offer { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}
