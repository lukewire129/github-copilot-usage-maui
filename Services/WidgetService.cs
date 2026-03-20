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

    public void Update(WidgetData data)
    {
        Current = data;
        DataChanged?.Invoke(data);
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
