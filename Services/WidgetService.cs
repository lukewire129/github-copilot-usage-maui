using copilot_usage_maui.Models;

namespace copilot_usage_maui.Services;

public class WidgetService
{
    public WidgetData? Current { get; private set; }

    public event Action<WidgetData>? DataChanged;

    /// <summary>
    /// Deskband에서 강제 새로고침 요청 시 발생
    /// </summary>
    public event Func<Task>? RefreshRequested;

    // ── Popup 데이터 채널 ──
    internal PopupData? PopupCurrent { get; private set; }
    internal event Action<PopupData>? PopupDataChanged;

    public void Update(WidgetData data)
    {
        Current = data;
        DataChanged?.Invoke(data);
    }

    /// <summary>
    /// 팝업에 표시할 전체 데이터 업데이트 (Copilot 또는 Claude 쪽에서 호출)
    /// </summary>
    internal void UpdatePopup(PopupData data)
    {
        PopupCurrent = data;
        PopupDataChanged?.Invoke(data);
    }

    /// <summary>
    /// Copilot 데이터만 갱신 (기존 Claude 데이터 유지)
    /// </summary>
    internal void UpdateCopilotPopup(UsageSummary summary)
    {
        var popup = PopupCurrent ?? new PopupData();
        popup.CopilotSummary = summary;
        UpdatePopup(popup);
    }

    /// <summary>
    /// Claude 데이터만 갱신 (기존 Copilot 데이터 유지)
    /// </summary>
    internal void UpdateClaudePopup(ClaudeUsageSnapshot snapshot)
    {
        var popup = PopupCurrent ?? new PopupData();
        popup.ClaudeSnapshot = snapshot;
        UpdatePopup(popup);
    }

    /// <summary>
    /// Deskband Refresh 버튼 → 현재 활성 페이지에 강제 새로고침 요청
    /// </summary>
    public async Task RequestRefreshAsync()
    {
        if (RefreshRequested is not null)
            await RefreshRequested.Invoke();
    }
}
