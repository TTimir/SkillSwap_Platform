using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class UserGoogleToken
{
    public int UserGoogleTokenId { get; set; }

    public int UserId { get; set; }

    public string AccessToken { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
}
