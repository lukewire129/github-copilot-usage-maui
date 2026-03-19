namespace copilot_usage_maui.Models;

record ClaudeUsageSnapshot(
    ClaudeRateWindow? SessionWindow,       // 5시간 세션
    ClaudeRateWindow? WeeklyWindow,        // 7일 롤링
    Dictionary<string, ClaudeRateWindow> ModelWindows,  // 모델별 제한
    string? Email,
    string? Plan,              // free/pro/team/max
    string? OrganizationName,
    DateTime UpdatedAt
)
{
    /// <summary>가장 제한적인 윈도우 (사용률이 높은 쪽).</summary>
    public ClaudeRateWindow? MostRestrictive
    {
        get
        {
            var windows = new[] { SessionWindow, WeeklyWindow }
                .Where(w => w is not null)
                .Cast<ClaudeRateWindow>()
                .ToList();
            return windows.Count == 0 ? null : windows.MaxBy(w => w.UsedPercent);
        }
    }
}
