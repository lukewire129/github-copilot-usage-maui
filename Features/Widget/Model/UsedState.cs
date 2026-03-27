using System;

namespace copilot_usage_maui.Features.Widget.Model;

public class UsedState
{
    public string IconFileName { get; set; }
    public double UsedPercent { get; set; }
    public double? SessionUsedPercent { get; set; } = 0.0;
    public double? WeeklyUsedPercent { get; set; } = 0.0;
    public string ResetTimeText { get; set; }
}
