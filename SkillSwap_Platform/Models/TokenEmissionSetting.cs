using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TokenEmissionSetting
{
    public int Id { get; set; }

    public decimal TotalPool { get; set; }

    public DateTime StartDateUtc { get; set; }

    public int DripDays { get; set; }

    public int HalvingPeriodDays { get; set; }

    public decimal DailyCap { get; set; }

    public bool IsEnabled { get; set; }
}
