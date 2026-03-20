namespace copilot_usage_maui.Models;

public class WidgetData
{
    public string ProviderName { get; set; } = "";
    public string IconFileName { get; set; } = "";
    public double UsedPercent { get; set; }
    public string ResetTimeText { get; set; } = "";

    // Claude 5-hour session window (null for non-Claude providers)
    public double? SessionUsedPercent { get; set; }
    public string? SessionResetText { get; set; }
}
