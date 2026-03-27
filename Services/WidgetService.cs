using copilot_usage_maui.Features.Widget.Pages;
using copilot_usage_maui.Models;
using MauiReactor.Integration;

namespace copilot_usage_maui.Services;

public class WidgetService
{
    public WidgetData? Current { get; private set; }

    public event Action<WidgetData>? DataChanged;

    /// <summary>
    /// Deskband에서 강제 새로고침 요청 시 발생
    /// </summary>
    public event Func<Task>? RefreshRequested;
    public Type? WidgetType { get; private set; } = typeof(DeskBandPage);
    public void Update(WidgetData data)
    {
        Current = data;
        DataChanged?.Invoke(data);
    }
    // 기존 이벤트 대신 단일 핸들러로 교체
    private Func<Task>? _refreshHandler;


    public void SetRefreshHandler(Func<Task>? handler)
    {
        _refreshHandler = handler;
    }
    /// <summary>
    /// Deskband Refresh 버튼 → 현재 활성 페이지에 강제 새로고침 요청
    /// </summary>
    public async Task RequestRefreshAsync()
    {
        if (_refreshHandler is not null)
            await _refreshHandler();
    }

    public void SetWidgetMode(int mode)
    {
        if (mode == 0)
        {
            WidgetType = typeof(DeskBandPage);
        }
        else if (mode == 1)
        {
            WidgetType = typeof(VerticalFloatingPage);
        }
        else if (mode == 2)
        {
            WidgetType = typeof(HorizontalFloatingPage);
        }
        else
        {
            WidgetType = null;
        }
    }
}
