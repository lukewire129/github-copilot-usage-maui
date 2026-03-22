namespace copilot_usage_maui.Models;

/// <summary>
/// 팝업 윈도우에 표시할 데이터.
/// Copilot/Claude 양쪽 데이터를 모두 보유하며, 탭 전환 시 즉시 표시 가능.
/// </summary>
class PopupData
{
    public UsageSummary? CopilotSummary { get; set; }
    public ClaudeUsageSnapshot? ClaudeSnapshot { get; set; }

    /// <summary>현재 활성 탭 ("Copilot" 또는 "Claude")</summary>
    public string ActiveProvider { get; set; } = "Copilot";
}
